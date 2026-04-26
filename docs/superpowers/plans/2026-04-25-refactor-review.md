# DevHub — Prioritized Refactoring Review
**Date:** 2026-04-25  
**Reviewer:** Code Review Agent  
**Scope:** `src/DevHub/` · `tests/DevHub.U.Tests/`

---

## 1. Critical — bugs or patterns that will break things at scale

---

### C-1 · `RepoStateStore` — concurrency: race on `IsScanning`, `LastScanCompleted` and `OnStateChanged`

**File:** `src/DevHub/Services/RepoStateStore.cs` — entire file

`IsScanning` y `LastScanCompleted` son escritos por el thread del scanner y leídos por cada circuit de Blazor. El `lock` actual solo cubre `_repos`. El evento `OnStateChanged` es un delegate mutable que puede ser modificado por un circuit mientras el scanner está invocando, produciendo `ObjectDisposedException`.

**No usar `lock`.** Los locks en singletons compartidos entre circuits de Blazor Server introducen potencial de deadlock cuando el circuit dispatcher espera al lock y el lock espera al dispatcher. La estrategia correcta es **inmutabilidad + swap atómico**:

```csharp
public class RepoStateStore
{
    // ImmutableArray es thread-safe para lectura, el swap es atómico en x64
    private volatile ImmutableArray<RepoInfo> _repos = [];
    private volatile bool _isScanning;
    private volatile DateTime _lastScanCompleted;

    public IReadOnlyList<RepoInfo> Repos => _repos;
    public bool IsScanning => _isScanning;
    public DateTime LastScanCompleted => _lastScanCompleted;

    // event Action? es thread-safe para lectura de delegate en C# (referencia atómica)
    // El snapshot antes del invoke evita el ObjectDisposedException en circuit teardown
    public event Action? OnStateChanged;

    public void SetScanning(bool scanning)
    {
        _isScanning = scanning;
        OnStateChanged?.Invoke(); // delegate read es atómico, snapshot implícito
    }

    public void SetRepos(IReadOnlyList<RepoInfo> repos)
    {
        _repos = [..repos];           // ImmutableArray.CreateRange equivalente
        _isScanning = false;
        _lastScanCompleted = DateTime.UtcNow;
        OnStateChanged?.Invoke();
    }
}
```

`volatile` garantiza visibilidad entre threads sin bloquear. El read de `OnStateChanged?.Invoke()` captura un snapshot atómico del delegate — si un circuit unsubscribe en paralelo, el snapshot ya tiene o no tiene el handler, pero nunca rompe a mitad de la invocación.

Eliminar el campo `_lock` y la lista mutable existente. Eliminar el constructor que inicializa la lista.

---

### C-3 · `appsettings.json` contains plaintext SQL Server credentials committed to source

**File:** `src/DevHub/appsettings.json` — line 19

```json
"DefaultConnection": "Server=DESKTOP-SLPCU2M;Database=DevHub;User Id=sa;Password=hola123;..."
```

The `sa` account with a trivial password is committed in clear text. `appsettings.json` is included in the build output and therefore in any published package or installer. Even for a personal tool this is a bad habit that propagates to future projects.

**Fix:** Move credentials to `appsettings.Development.json` (which should be `.gitignore`d) or to user secrets. Ship `appsettings.json` with an empty or placeholder connection string:

```json
"DefaultConnection": ""
```

---

### C-4 · `GitCliService` — `RunGitAsync` usa un string único de argumentos que no funciona igual en Windows y Linux

**File:** `src/DevHub/Services/GitCliService.cs` — `RunGitAsync` y sus callers (`CheckoutAsync`, etc.)

El método actual construye los argumentos como string y los pasa a `ProcessStartInfo.Arguments`:

```csharp
// Actual — frágil, cross-platform incorrecto
var safeBranch = $"\"{branch.Replace("\"", "\\\"")}\"";
await RunGitAsync(repoPath, $"checkout {safeBranch}", ct);
```

En Linux, .NET pasa el `Arguments` string directamente a `execv` sin interpretación de shell. Las comillas dobles **no son procesadas** por el proceso receptor — `git` recibe literalmente `checkout "main"` con las comillas como parte del nombre de branch. En Windows funciona porque el runtime pasa por `CreateProcess` que sí interpreta comillas. El resultado es comportamiento diferente en cada OS.

**Fix — cambiar `RunGitAsync` para aceptar `params string[] args` y usar `ProcessStartInfo.ArgumentList`:**

```csharp
// GitCliService.cs — nuevo RunGitAsync
private async Task<(string StdOut, string StdErr, int ExitCode)> RunGitAsync(
    string repoPath, string[] args, CancellationToken ct)
{
    var psi = new ProcessStartInfo("git")
    {
        WorkingDirectory = repoPath,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };
    foreach (var arg in args)
        psi.ArgumentList.Add(arg); // .NET escapa correctamente en Windows y Linux
    // ... resto igual
}

// Callers — sin quoting manual, el branch se pasa como elemento discreto
private async Task<int> CheckoutAsync(string repoPath, string branch, CancellationToken ct)
{
    var (_, _, exitCode) = await RunGitAsync(repoPath, ["checkout", branch], ct);
    if (exitCode != 0)
        (_, _, exitCode) = await RunGitAsync(repoPath, ["checkout", "-b", branch], ct);
    return exitCode;
}
```

`ProcessStartInfo.ArgumentList` existe desde .NET 5 y maneja el escaping correcto en cada plataforma. No hay necesidad de sanitizar el branch name — cada elemento es un argumento discreto que va directo a `execv`/`CreateProcess` sin interpretación de shell.

---

### C-5 · `Home.razor` — `System.Timers.Timer` fires on a thread-pool thread, calling `InvokeAsync(StateHasChanged)` every second for every open circuit

**File:** `src/DevHub/Components/Pages/Home.razor` — lines 107, 236–244

The countdown timer calls `InvokeAsync(StateHasChanged)` once per second. Each circuit has its own timer. With N open tabs this is N thread-pool firings per second that each marshal back through the Blazor hub, even when there is no user interaction. At scale (CI monitor with 10 tabs open) this generates unnecessary SignalR traffic and defeats the purpose of Blazor's diff-based rendering.

The countdown value is purely cosmetic. There is no real need to push it to the browser with a server-side timer.

**Fix:** Use `PeriodicTimer` inside the circuit's `Task`-based loop (dispose-safe, avoids thread-pool callbacks), or switch the countdown to a JavaScript `setInterval` that reads `ScanIntervalSeconds` once on mount. The simplest server-side fix:

```csharp
private PeriodicTimer? _countdownTimer;

private async Task RunCountdownAsync(CancellationToken ct)
{
    _countdownTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
    while (await _countdownTimer.WaitForNextTickAsync(ct))
    {
        if (_secondsUntilNextScan > 0) _secondsUntilNextScan--;
        await InvokeAsync(StateHasChanged);
    }
}
```

The existing `Dispose()` already cancels via `_countdownTimer?.Dispose()`, so the pattern fits.

---

## 2. High — architectural violations, missing tests, SRP violations

---

### H-1 · `DevHubOptions` and `ServiceBusMapOptions` are declared inside `RepoScannerService.cs`

**File:** `src/DevHub/Services/RepoScannerService.cs` — lines 8–16 and 125–133

Options classes are configuration contracts for the whole application. Placing them inside a scanner file couples any consumer of those options to the scanner's compilation unit and violates the single-responsibility principle for the file. `ServiceBusMapOptions` has no logical relationship to repo scanning at all.

**Fix:** Move each options class to its own file:
- `src/DevHub/Services/DevHubOptions.cs`
- `src/DevHub/Services/ServiceBusMapOptions.cs`

---

### H-2 · `DetermineGroup` is a `public static` method on `RepoScannerService` — it is domain logic that belongs in a separate class

**File:** `src/DevHub/Services/RepoScannerService.cs` — lines 111–122

The method is already `public static`, which is a sign that the author recognised it does not need the scanner's state. However it is still attached to the scanner class. The tests in `When_DetermineGroup_is_called` call it as `RepoScannerService.DetermineGroup(...)`, coupling tests to the scanner's public surface for something that is purely a grouping rule.

Additionally, `TriggerScanAsync` performs the group resolution inline (lines 68–71) and then calls `DetermineGroup` again for the fallback — this is two slightly different code paths for the same operation that can easily diverge.

**Fix:** Extract a `RepoGroupResolver` class (or a pure static helper in `Models/`) that both `RepoScannerService` and any future consumer can use. Update the two tests to reference the new type.

---

### H-3 · `RepoScannerService.TriggerScanAsync` duplicates group-resolution logic

**File:** `src/DevHub/Services/RepoScannerService.cs` — lines 68–72

```csharp
var matchedRule = _options.Groups.FirstOrDefault(rule =>
    rule.Prefixes.Any(prefix => repoName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
var group = matchedRule?.Name ?? DetermineGroup(repoName, _options.Groups, _options.DefaultGroup);
var groupColor = matchedRule?.Color ?? "default";
```

`DetermineGroup` (line 111) already does the same prefix scan. The inline code is the same O(n) scan run twice, with the fallback color `"default"` hardcoded as a magic string. If `DetermineGroup` is extended to also return the color, this divergence becomes a bug.

**Fix:** Replace both paths with a single call that returns `(group, color)`:

```csharp
public static (string Group, string Color) ResolveGroup(
    string repoName, List<GroupRule> groups, string defaultGroup)
{
    foreach (var rule in groups)
        if (rule.Prefixes.Any(p => repoName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return (rule.Name, rule.Color);
    return (defaultGroup, "default");
}
```

---

### H-4 · `GitCliService` is not abstracted behind an interface — impossible to unit-test consumers without spawning real git processes

**File:** `src/DevHub/Services/GitCliService.cs` (entire file) and `Program.cs` line 39

`GitCliService` is registered as a concrete type. `RepoScannerService` injects it as a concrete type. `BulkActions.razor` also injects it directly. None of these can be tested without a real `git` binary and real temp repos, making all three effectively integration-test-only.

`IProcessRunner` exists and is correctly abstracted, but no equivalent interface exists for git operations.

**Fix:** Extract `IGitService` with the public async methods, register the concrete class against the interface:

```csharp
// Program.cs
builder.Services.AddSingleton<IGitService, GitCliService>();
```

This unblocks unit tests for `RepoScannerService` that verify group assignment, scan error handling, and parallel scheduling without real filesystem I/O.

---

### H-5 · `Home.razor` and `Settings.razor` duplicate all catalog mutation logic (Add, Import, Remove)

**File:** `src/DevHub/Components/Pages/Home.razor` — lines 170–219  
**File:** `src/DevHub/Components/Pages/Settings.razor` — lines 160–223

Both pages contain structurally identical `AddRepoAsync`, `ImportRootAsync`, and `RemoveRepoAsync` methods with identical `_catalogBusy` guard, identical try/catch/Snackbar patterns, and identical `CancellationToken.None` usage. Any bug fix or snackbar wording change must be applied in two places.

**Fix:** Extract a `CatalogMutationService` (or a shared Blazor service) that wraps `IRepoCatalogService` with the busy-guard and error reporting. Alternatively, if the duplication is accepted, at minimum extract the try/catch/Snackbar pattern into a shared helper method `ExecuteCatalogOperationAsync(Func<Task>, string successMessage)`.

---

### H-6 · `ParseColor` is duplicated between `RepoList.razor` and `Settings.razor`

**File:** `src/DevHub/Components/RepoList.razor` — lines 128–138  
**File:** `src/DevHub/Components/Pages/Settings.razor` — lines 143–153

Identical static switch from string to `MudBlazor.Color`. Any new color value added to `GroupRule` must be added in both places.

**Fix:** Extract to a `MudColorExtensions` static class or a shared Blazor `ColorHelper`:

```csharp
// src/DevHub/Components/ColorHelper.cs
public static class ColorHelper
{
    public static Color FromGroupColor(string color) => color.ToLowerInvariant() switch { ... };
}
```

---

### H-7 · `VersionService` performs file I/O in the constructor — breaks testability and violates DI conventions

**File:** `src/DevHub/Services/VersionService.cs` — lines 22–33

The constructor calls `Directory.CreateDirectory`, `File.Exists`, `File.ReadAllText`, and `File.WriteAllText`. DI constructors should be free of side effects. A test that instantiates `VersionService` will touch the real `AppData/DevHub` directory on the test machine. There is no equivalent `IFileSystem` abstraction here, unlike `SecretProfileService`.

**Fix:** Inject `IFileSystem` (already defined) and defer I/O to an `InitializeAsync` method, or at minimum record the real `AppData` path via an injected `IOptions<VersionServiceOptions>` so tests can redirect it.

---

### H-8 · Missing test scenarios for key behaviors

The following behaviors have zero test coverage:

| Missing scenario | Why it matters |
|---|---|
| `RepoScannerService` — excluded repos are filtered from scan | `ExcludedRepos` config is silently ignored if the filter logic changes |
| `RepoScannerService` — auto-import from `RootPath` when catalog is empty | The double-query pattern (lines 98–108) has a distinct code path with no test |
| `GitCliService.ScanRepoAsync` — non-git directory returns `IsGitRepo = false` | The early-exit branch (lines 138–148) is never exercised |
| `GitCliService.CheckoutAsync` — falls back to `-b` when branch does not exist | Silent branch creation is never verified |
| `RepoInfo.BuildPrUrl` — GitHub SSH URL conversion | The `git@github.com:` → `https://` replace is untested |
| `RepoInfo.BuildPrUrl` — Azure DevOps URL with embedded credentials is stripped | The regex replace at line 53 is untested |
| `EfRepoCatalogService.AddAsync` — duplicate paths are silently ignored | The `if (!exists)` guard (line 51) has no test |
| `FilterBar` / `Home.ApplyCriteria` — each status filter value | `"dirty"`, `"clean"`, `"behind"`, `"feature"` branches are untested |

The convention `When_<scenario>/Then_<result>.cs` is correctly followed everywhere that tests exist. The gap is breadth, not style.

---

### H-9 · `EfRepoCatalogService.EnsureInitializedAsync` uses `EnsureCreated` instead of migrations

**File:** `src/DevHub/Services/EfRepoCatalogService.cs` — line 13

`EnsureCreated` cannot evolve the schema. If a column is added to `RepoCatalogEntry`, existing databases will not be updated and the application will fail with a schema mismatch at runtime. Because the app supports both SQLite and SQL Server, this matters in production.

**Fix:** Add an EF Core migration and call `MigrateAsync` instead:

```csharp
await db.Database.MigrateAsync(ct);
```

This is safe with both providers and handles empty databases the same way `EnsureCreated` does.

---

### H-10 · `ServiceBusMap.razor` reads `File.Exists` directly in a Blazor component — bypasses testability and couples presentation to infrastructure

**File:** `src/DevHub/Components/Pages/ServiceBusMap.razor` — lines 72–73

```csharp
private void RefreshMapStatus()
{
    var path = Path.Combine(Env.WebRootPath, "maps", "servicebus-map.html");
    _mapExists = File.Exists(path);
```

Blazor components should not call `File.Exists` directly. The component is now coupled to the real filesystem and cannot be tested without setting up a physical file. `IWebHostEnvironment` is also a relatively heavyweight service to inject into a component for this purpose.

**Fix:** Add `MapExists` and `MapLastModifiedUtc` to `ServiceBusMapResult` (or to a new `ServiceBusMapStatus` query on `ServiceBusMapService`). Let the component ask the service, not the filesystem.

---

## 3. Medium — code quality, naming, duplication

---

### M-1 · Test method name `Execute` violates the When/Then naming convention

**Files:** every test class across `tests/DevHub.U.Tests/`  
**Example:** `tests/DevHub.U.Tests/Services/When_GitCliService_scans_clean_repo/Then_IsDirty_is_false.cs` — line 11

The project's convention (enforced by Roslyn analyzers TEST001–003) is `When_<scenario>` for the class and `Then_<result>` for the method. Every test method across the entire test suite is named `Execute` instead of `Then_<result>`. The intended convention is captured in the class name only.

Under the Roslyn analyzer rules the class name is the scenario (`Then_IsDirty_is_false`) and the method should mirror the result. Using `Execute` makes test-runner output harder to read and defeats the purpose of the convention.

**Fix:** Rename every `Execute` method to `Then_<result>` matching the class name:

```csharp
// Before
public class Then_IsDirty_is_false
{
    [Fact]
    public async Task Execute() { ... }
}

// After
public class Then_IsDirty_is_false
{
    [Fact]
    public async Task Then_dirty_flag_and_count_are_zero() { ... }
}
```

This is mechanical — a global rename within each file.

---

### M-2 · `RepoRow.razor` uses `ToRelativeTime` as a static method inside a component — should be extracted

**File:** `src/DevHub/Components/RepoRow.razor` — lines 146–162

`ToRelativeTime` and `BuildCommitTooltip` are pure functions that take a `DateTime` and return a string. They have no dependency on component state or Blazor. Keeping them inside the `.razor` file means they cannot be tested, reused, or independently changed. There is no corresponding test for the relative-time formatting edge cases (boundary at 30 days, `DateTime.MinValue` guard).

**Fix:** Extract to a `DateTimeDisplayHelper` static class in `DevHub/Components/` or `DevHub/Models/` and add unit tests covering: MinValue, under 1 minute, under 1 hour, under 1 day, 29 days, 30 days, and a past date in a previous month.

---

### M-3 · `RepoInfo.BuildPrUrl` is domain logic in a record, calling `Regex.Replace` on every property access

**File:** `src/DevHub/Models/RepoInfo.cs` — lines 21–58

`PrUrl` is a computed property with no backing store. Every access re-runs the URL normalisation logic including `Regex.Replace` (line 53). Because `RepoInfo` is immutable (`record` with `init`), the result never changes — it should be computed once.

`Regex.Replace` also uses an inline pattern string (`@"https?://[^@]+@"`) rather than a `[GeneratedRegex]` source-generated regex, which avoids the one-time compilation cost that `SecretProfileService` uses correctly.

**Fix:**

```csharp
// Compute once in the constructor area or use a Lazy
public string? PrUrl { get; } = BuildPrUrl(RemoteUrl, Branch); // init-only via factory
```

Since `record` does not allow `init` computed from other members easily, the cleanest fix is a `[GeneratedRegex]` in a companion static class and a backing field set in the record's body:

```csharp
private static readonly string? _prUrl;
public string? PrUrl => _prUrl ??= ComputePrUrl(); // thread-safe for immutable records
```

---

### M-4 · `BulkActions.razor` — `_busy` is set to `false` in the method body instead of `finally`, leaving the UI stuck on exception

**File:** `src/DevHub/Components/BulkActions.razor` — lines 124–137 and 176–192

```csharp
private async Task PullSelected()
{
    _busy = true;
    // ... loop ...
    _busy = false;         // not in finally
    await OnOperationCompleted.InvokeAsync();
}
```

If `GitService.PullAsync` throws an unhandled exception, `_busy` stays `true` and the Pull and Checkout buttons remain permanently disabled until the page is reloaded. The same pattern is in `CheckoutSelected`.

**Fix:**

```csharp
private async Task PullSelected()
{
    _busy = true;
    try { /* ... */ }
    finally { _busy = false; }
    await OnOperationCompleted.InvokeAsync();
}
```

---

### M-5 · `JsonViewerDialog.razor` contains a magic `Task.Delay(80)` with no explanation

**File:** `src/DevHub/Components/Pages/JsonViewerDialog.razor` — line 77  
**File:** `src/DevHub/Components/Pages/JsonDiffDialog.razor` — line 42

```csharp
private async Task OnInit()
{
    if (_editor is null) return;
    await Task.Delay(80);   // why 80ms?
    await _editor.Layout();
}
```

This is a timing hack to work around BlazorMonaco's layout not being ready when `OnDidInit` fires. The magic constant `80` will be silently wrong in a slower environment. Both files duplicate it.

**Fix:** Document why the delay exists with a brief comment, or replace with a more reliable callback if BlazorMonaco exposes one. If the delay is unavoidable, extract the constant:

```csharp
private const int MonacoLayoutSettleMs = 80; // BlazorMonaco needs one render cycle after init
```

---

### M-6 · `TempGitRepo` and `TempGitRepoAt` duplicate the `Run` method and the `git init` + config setup

**File:** `tests/DevHub.U.Tests/Helpers/TempGitRepo.cs` — lines 1–92

`TempGitRepo` and `TempGitRepoAt` are almost identical. The only difference is whether the path is caller-supplied or generated. `Run(string)` is duplicated verbatim, and the three git setup commands (`git init`, `git config user.email`, `git config user.name`, `git commit --allow-empty`) are duplicated identically.

`TempGitRepoAt` does not expose `CreateFile`, `StageAndCommit`, or `Dispose`-based cleanup (it delegates to `ForceDeleteDirectory` but not in a pattern consumers would expect).

**Fix:** Consolidate into a single `TempGitRepo` class with an optional path parameter:

```csharp
public sealed class TempGitRepo : IDisposable
{
    public string Path { get; }
    public TempGitRepo(string? path = null) { ... }
}
```

---

### M-7 · `RepoCatalogEntry` mezcla atributos de infraestructura EF con el modelo de dominio, y usa `Path` como PK

**File:** `src/DevHub/Models/RepoCatalogEntry.cs`

Dos problemas combinados:

1. La entidad tiene `[Table]`, `[Key]`, `[Column]`, `[MaxLength]` — atributos de infraestructura que ensucian el modelo de dominio. Toda la configuración de mapeo pertenece en `ApplicationDbContext` vía Fluent API.

2. Usar el path del filesystem como PK es frágil: mover o renombrar un directorio produce una fila nueva y deja la vieja como basura. En Linux las comparaciones son case-sensitive según el collation de la DB; en Windows son case-insensitive.

**Fix — entidad limpia + Fluent API en `ApplicationDbContext`:**

```csharp
// src/DevHub/Models/RepoCatalogEntry.cs — sin atributos EF
public class RepoCatalogEntry
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public DateTime AddedUtc { get; set; }

    public RepoCatalogEntry() { }
    public RepoCatalogEntry(string path, DateTime addedUtc) { Path = path; AddedUtc = addedUtc; }
}

// src/DevHub/Data/ApplicationDbContext.cs — toda la config acá
public DbSet<RepoCatalogEntry> RepoCatalogEntries => Set<RepoCatalogEntry>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<RepoCatalogEntry>(e =>
    {
        e.HasKey(r => r.Id);
        e.Property(r => r.Path).HasMaxLength(1024).IsRequired();
        e.HasIndex(r => r.Path).IsUnique();
        // AddedUtc — EF lo mapea por convención, sin columna explícita
    });
}
```

El nombre de tabla lo genera EF por convención desde el nombre del `DbSet` → `RepoCatalogEntries`. Sin `[Table]`, sin snake_case, sin underscores.

---

### M-8 · `EfRepoCatalogService` test setup is copied verbatim in every test class

**Files:** `tests/DevHub.U.Tests/Services/When_RepoCatalogService_is_used/Then_add_persists_repo_path.cs` (lines 15–29), `Then_add_normalizes_relative_and_quoted_paths.cs` (lines 16–29), `Then_import_from_root_adds_git_repos_only.cs` (lines 15–29), `Then_remove_deletes_persisted_repo_path.cs` (lines 15–29)

All four tests repeat the same 15-line SQLite in-memory setup block. This is copy-paste test infrastructure that belongs in a shared fixture.

**Fix:** Create a `When_RepoCatalogService_is_used/DbFixture.cs` that wraps the connection and factory setup and is shared via `IClassFixture<DbFixture>`:

```csharp
public class DbFixture : IDisposable
{
    public EfRepoCatalogService CreateSut() { ... }
    public void Dispose() { ... }
}
```

---

## 4. Low — style, minor improvements

---

### L-1 · `appsettings.json` uses Windows-only backslash paths in JSON values

**File:** `src/DevHub/appsettings.json` — lines 23, 28 (`ScriptPath`, `TemplateFile`, `OutputFile`, `ProfilesRoot`)

`Path.Combine` normalises separators at runtime, so this does not break anything on Windows. However it embeds platform assumptions in config and would fail parsing on non-Windows if the values were used in a context that does not go through `Path.Combine`.

**Fix:** Use forward slashes in JSON strings (`../../profiles`) which are valid path separators on all platforms when passed to `Path.Combine`.

---

### L-2 · `RepoRow.razor` — inline `Style` string contains duplicated CSS that is already in the stylesheet

**File:** `src/DevHub/Components/RepoRow.razor` — line 42

```razor
<MudText Style="@($"font-weight: {(Repo.IsDirty ? "600" : "400")}; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; display:block")">
```

`overflow:hidden; text-overflow:ellipsis; white-space:nowrap` already appears in the `.repo-name-cell` class at line 17. The inline style duplicates the class for the inner `MudText` element, creating a fragile double-definition.

**Fix:** Add a CSS class for the dirty/clean weight and remove the inline style string.

---

### L-3 · `Home.razor` references `Store.Repos` twice in the status footer without caching the snapshot

**File:** `src/DevHub/Components/Pages/Home.razor` — lines 85–86

```razor
<MudText>⚠ @Store.Repos.Count(r => r.IsDirty) dirty</MudText>
<MudText>↓ @Store.Repos.Count(r => r.BehindCount > 0) behind</MudText>
```

Each call to `Store.Repos` acquires the lock and returns the current snapshot. On a render with hundreds of repos this is two lock acquisitions and two full LINQ scans for display-only data that is already computed during `RefreshView`. The dirty/behind counts should be computed in `RefreshView` and stored in component fields.

---

### L-4 · `PromptDialog.razor` injects `IJSRuntime` but never uses it

**File:** `src/DevHub/Components/Pages/PromptDialog.razor` — line 1

```razor
@inject IJSRuntime JS
```

This injection is present but the component does not call `JS` anywhere. It is either a leftover from a previous iteration or a planned feature that was not implemented.

**Fix:** Remove the unused injection.

---

### L-5 · Mixed Spanish/English in UI strings — no consistent i18n strategy

**Files:** `Home.razor`, `BulkActions.razor`, `SecretProfiles.razor`, `RepoList.razor`, `RepoRow.razor`

The UI mixes English and Spanish across pages without a pattern: `Home.razor` uses Spanish for snackbar messages and button labels, `Settings.razor` uses English for the same operations. `RepoList.razor` hardcodes `"todos clean · master"`.

This is low severity for a personal tool, but if the app ever serves a team this will need a consistent strategy (resource files or a simple `IStringLocalizer` wrapper). The current state makes future localisation harder because strings are embedded in markup.

---

### L-6 · `DevHub.csproj` version is hardcoded to `1.0.0`

**File:** `src/DevHub/DevHub.csproj` — lines 4–5

```xml
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0</AssemblyVersion>
```

`VersionService` reads `AssemblyInformationalVersion` at runtime (line 13) to detect updates. Since the version never changes in the project file, `VersionService.IsUpdated` will always be `false` after the first run, making `UpdateBanner` effectively inert.

**Fix:** Use a build-time version injection via `GitVersion`, `MinVer`, or a CI pipeline `dotnet build /p:Version=x.y.z` convention. Alternatively add `<InformationalVersion>` to the project and bump it on each release.

---

*End of review. 5 Critical, 10 High, 8 Medium, 6 Low findings — 29 total.*
