# DevHub Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aplicar los refactors críticos y de alta prioridad del review `2026-04-25-refactor-review.md` para dejar DevHub con arquitectura limpia, sin race conditions, y con convenciones consistentes.

**Architecture:** Inmutabilidad + swap atómico para estado compartido; FluentAPI exclusivamente para mapeo EF; `ProcessStartInfo.ArgumentList` para todos los comandos git; extracción de responsabilidades en clases pequeñas focalizadas.

**Tech Stack:** .NET 10, Blazor Server, EF Core 9, MudBlazor 9.3, xUnit + Moq + Shouldly, LibGit2Sharp

---

## Task 1: RepoStateStore — `volatile` + `ImmutableArray` (C-1)

**Files:**
- Modify: `src/DevHub/Services/RepoStateStore.cs`
- Modify: `src/DevHub/Services/RepoScannerService.cs` (callers de `SetScanning`/`Update`)

### Contexto
`RepoStateStore` tiene un `lock` que cubre `_repos` pero no `IsScanning` ni `LastScanCompleted`. Además usa `List<RepoInfo>` mutable. La estrategia correcta para un singleton en Blazor Server es inmutabilidad + `volatile`.

- [ ] **Step 1: Leer el archivo actual**

```bash
cat src/DevHub/Services/RepoStateStore.cs
```

- [ ] **Step 2: Escribir test que verifica que `SetRepos` actualiza `Repos`, `IsScanning` y `LastScanCompleted`**

Crear `tests/DevHub.U.Tests/Services/When_RepoStateStore_is_updated/Then_repos_and_scan_state_are_visible.cs`:

```csharp
using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoStateStore_is_updated;

public class Then_repos_and_scan_state_are_visible
{
    [Fact]
    public void Then_state_reflects_repos_after_SetRepos()
    {
        var sut = new RepoStateStore();

        sut.SetScanning(true);
        sut.IsScanning.ShouldBeTrue();

        var repos = new List<RepoInfo> { new() { Path = "C:\\repo1", Name = "repo1" } };
        sut.SetRepos(repos);

        sut.IsScanning.ShouldBeFalse();
        sut.Repos.Count.ShouldBe(1);
        sut.LastScanCompleted.ShouldNotBe(default);
    }
}
```

- [ ] **Step 3: Ejecutar el test para verificar que falla (el método SetRepos no existe aún)**

```bash
dotnet test tests/DevHub.U.Tests --filter "When_RepoStateStore_is_updated"
```

Expected: FAIL — método no existe.

- [ ] **Step 4: Reemplazar `RepoStateStore.cs` con la implementación correcta**

```csharp
using System.Collections.Immutable;
using DevHub.Models;

namespace DevHub.Services;

public class RepoStateStore
{
    private volatile bool _isScanning;
    private volatile ImmutableArray<RepoInfo> _repos = [];

    private DateTime _lastScanCompleted;
    // DateTime no es volatile-safe en 32-bit, usamos Interlocked via long ticks
    private long _lastScanCompletedTicks;

    public bool IsScanning => _isScanning;
    public IReadOnlyList<RepoInfo> Repos => _repos;
    public DateTime LastScanCompleted => new DateTime(
        Interlocked.Read(ref _lastScanCompletedTicks), DateTimeKind.Utc);

    public event Action? OnStateChanged;

    public void SetScanning(bool scanning)
    {
        _isScanning = scanning;
        OnStateChanged?.Invoke();
    }

    public void SetRepos(IReadOnlyList<RepoInfo> repos)
    {
        _repos = [..repos];
        _isScanning = false;
        Interlocked.Exchange(ref _lastScanCompletedTicks, DateTime.UtcNow.Ticks);
        OnStateChanged?.Invoke();
    }
}
```

- [ ] **Step 5: Buscar todos los callers del store en `RepoScannerService.cs` y actualizarlos**

Reemplazar cualquier asignación directa a propiedades del store por las nuevas llamadas:
- `Store.IsScanning = true` → `Store.SetScanning(true)`
- `Store.IsScanning = false` → eliminado (lo hace `SetRepos`)
- `Store.Repos = ...` → `Store.SetRepos(...)`
- `Store.LastScanCompleted = ...` → eliminado (lo hace `SetRepos`)

```bash
grep -n "Store\." src/DevHub/Services/RepoScannerService.cs
```

- [ ] **Step 6: Ejecutar todos los tests**

```bash
dotnet test tests/DevHub.U.Tests
```

Expected: todos pasan.

- [ ] **Step 7: Commit**

```bash
git add src/DevHub/Services/RepoStateStore.cs src/DevHub/Services/RepoScannerService.cs tests/DevHub.U.Tests/Services/When_RepoStateStore_is_updated/
git commit -m "refactor: replace lock with volatile+ImmutableArray in RepoStateStore"
```

---

## Task 2: GitCliService — `ProcessStartInfo.ArgumentList` cross-platform (C-4)

**Files:**
- Modify: `src/DevHub/Services/GitCliService.cs`

### Contexto
`RunGitAsync` acepta un `string arguments` que se asigna a `ProcessStartInfo.Arguments`. En Linux las comillas dobles dentro de ese string **no son procesadas** — `execv` las pasa literalmente al proceso. En Windows sí funcionan porque `CreateProcess` las interpreta. El fix es cambiar a `ProcessStartInfo.ArgumentList` que hace el escaping correcto en cada plataforma.

- [ ] **Step 1: Leer `GitCliService.cs` completo**

```bash
cat src/DevHub/Services/GitCliService.cs
```

- [ ] **Step 2: Cambiar la firma de `RunGitAsync` para aceptar `string[]` en vez de `string`**

Localizar el método `RunGitAsync`. Cambiar:
```csharp
// ANTES
private async Task<(string StdOut, string StdErr, int ExitCode)> RunGitAsync(
    string repoPath, string arguments, CancellationToken ct)
{
    var psi = new ProcessStartInfo("git")
    {
        Arguments = arguments,
        WorkingDirectory = repoPath,
        ...
    };
```

```csharp
// DESPUÉS
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
        psi.ArgumentList.Add(arg);
    // resto del método igual — Process.Start(psi), ReadToEnd, etc.
```

- [ ] **Step 3: Actualizar todos los callers dentro de `GitCliService.cs`**

Cada call de la forma `RunGitAsync(path, "fetch --prune", ct)` debe convertirse en array:

| Antes | Después |
|-------|---------|
| `"fetch --prune"` | `["fetch", "--prune"]` |
| `"pull --ff-only"` | `["pull", "--ff-only"]` |
| `$"checkout {branch}"` | `["checkout", branch]` |
| `$"checkout -b {branch}"` | `["checkout", "-b", branch]` |
| `"status --porcelain"` | `["status", "--porcelain"]` |
| `"log -1 --format=%H\t%s\t%ai\t%an"` | `["log", "-1", "--format=%H\t%s\t%ai\t%an"]` |
| `"remote get-url origin"` | `["remote", "get-url", "origin"]` |
| `"rev-list --count @{u}..HEAD"` | `["rev-list", "--count", "@{u}..HEAD"]` |
| `"branch --show-current"` | `["branch", "--show-current"]` |

Buscar con:
```bash
grep -n "RunGitAsync" src/DevHub/Services/GitCliService.cs
```

Eliminar cualquier variable `safeBranch` o quoting manual que ya no sea necesario.

- [ ] **Step 4: Buildear**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores.

- [ ] **Step 5: Ejecutar tests**

```bash
dotnet test tests/DevHub.U.Tests
```

Expected: todos pasan.

- [ ] **Step 6: Commit**

```bash
git add src/DevHub/Services/GitCliService.cs
git commit -m "refactor: use ProcessStartInfo.ArgumentList for cross-platform git args"
```

---

## Task 3: PeriodicTimer para el countdown (C-5)

**Files:**
- Modify: `src/DevHub/Components/Pages/Home.razor`

### Contexto
`System.Timers.Timer` dispara en threads del thread-pool y llama `InvokeAsync(StateHasChanged)` cada segundo por cada circuit abierto. `PeriodicTimer` es la alternativa .NET moderna — es awaitable, cancelable y no usa callbacks de thread-pool.

- [ ] **Step 1: En `Home.razor`, localizar `StartCountdown()` y `_countdownTimer`**

```bash
grep -n "countdownTimer\|StartCountdown\|Timers.Timer" src/DevHub/Components/Pages/Home.razor
```

- [ ] **Step 2: Reemplazar el campo y el método**

Cambiar el campo:
```csharp
// ANTES
private System.Timers.Timer? _countdownTimer;

// DESPUÉS
private PeriodicTimer? _countdownTimer;
private CancellationTokenSource? _countdownCts;
```

Reemplazar `StartCountdown()`:
```csharp
// ANTES
private void StartCountdown()
{
    _countdownTimer = new System.Timers.Timer(1000);
    _countdownTimer.Elapsed += (_, _) =>
    {
        if (_secondsUntilNextScan > 0) _secondsUntilNextScan--;
        InvokeAsync(StateHasChanged);
    };
    _countdownTimer.Start();
}

// DESPUÉS
private void StartCountdown()
{
    _countdownCts = new CancellationTokenSource();
    _countdownTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
    _ = RunCountdownAsync(_countdownTimer, _countdownCts.Token);
}

private async Task RunCountdownAsync(PeriodicTimer timer, CancellationToken ct)
{
    try
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            if (_secondsUntilNextScan > 0) _secondsUntilNextScan--;
            await InvokeAsync(StateHasChanged);
        }
    }
    catch (OperationCanceledException) { }
}
```

- [ ] **Step 3: Actualizar `Dispose()` para cancelar el CTS**

```csharp
public void Dispose()
{
    Store.OnStateChanged -= OnStateChanged;
    _countdownCts?.Cancel();
    _countdownCts?.Dispose();
    _countdownTimer?.Dispose();
}
```

- [ ] **Step 4: Buildear y verificar**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores.

- [ ] **Step 5: Commit**

```bash
git add src/DevHub/Components/Pages/Home.razor
git commit -m "refactor: replace System.Timers.Timer with PeriodicTimer for countdown"
```

---

## Task 4: Mover `DevHubOptions` y `ServiceBusMapOptions` a sus propios archivos (H-1)

**Files:**
- Create: `src/DevHub/Services/DevHubOptions.cs`
- Create: `src/DevHub/Services/ServiceBusMapOptions.cs`
- Modify: `src/DevHub/Services/RepoScannerService.cs` (eliminar las clases inline)

### Contexto
Ambas clases de opciones están declaradas dentro de `RepoScannerService.cs`. Deben vivir en archivos propios con una responsabilidad clara.

- [ ] **Step 1: Leer el inicio de `RepoScannerService.cs` para ver las clases inline**

```bash
head -40 src/DevHub/Services/RepoScannerService.cs
```

- [ ] **Step 2: Crear `src/DevHub/Services/DevHubOptions.cs`**

```csharp
using DevHub.Models;

namespace DevHub.Services;

public class DevHubOptions
{
    public string RootPath { get; set; } = string.Empty;
    public int ScanIntervalSeconds { get; set; } = 60;
    public int ParallelScanDegree { get; set; } = 4;
    public string DefaultGroup { get; set; } = "Other";
    public List<GroupRule> Groups { get; set; } = [];
    public List<string> ExcludedRepos { get; set; } = [];
}
```

*(Copiar la estructura exacta que existe inline en `RepoScannerService.cs`)*

- [ ] **Step 3: Crear `src/DevHub/Services/ServiceBusMapOptions.cs`**

```csharp
namespace DevHub.Services;

public class ServiceBusMapOptions
{
    public string ScriptPath { get; set; } = string.Empty;
    public string TemplateFile { get; set; } = string.Empty;
    public string OutputFile { get; set; } = string.Empty;
}
```

*(Copiar la estructura exacta que existe inline)*

- [ ] **Step 4: Eliminar las clases inline de `RepoScannerService.cs`**

Borrar las declaraciones de `DevHubOptions` y `ServiceBusMapOptions` que estén dentro del archivo.

- [ ] **Step 5: Buildear**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores (las clases ya estaban siendo usadas por los mismos namespaces).

- [ ] **Step 6: Commit**

```bash
git add src/DevHub/Services/DevHubOptions.cs src/DevHub/Services/ServiceBusMapOptions.cs src/DevHub/Services/RepoScannerService.cs
git commit -m "refactor: extract DevHubOptions and ServiceBusMapOptions to own files"
```

---

## Task 5: Extraer `RepoGroupResolver` (H-2 + H-3)

**Files:**
- Create: `src/DevHub/Services/RepoGroupResolver.cs`
- Create: `tests/DevHub.U.Tests/Services/When_RepoGroupResolver_resolves/Then_prefix_match_returns_group_and_color.cs`
- Create: `tests/DevHub.U.Tests/Services/When_RepoGroupResolver_resolves/Then_no_match_returns_default_group.cs`
- Modify: `src/DevHub/Services/RepoScannerService.cs`

### Contexto
`DetermineGroup` es `public static` en `RepoScannerService` — señal de que no pertenece ahí. Además `TriggerScanAsync` duplica el prefix scan inline antes de llamar a `DetermineGroup`, con el color `"default"` hardcodeado como magic string. Extraer un `RepoGroupResolver` con un único método `Resolve(name) → (group, color)`.

- [ ] **Step 1: Escribir los tests**

`tests/DevHub.U.Tests/Services/When_RepoGroupResolver_resolves/Then_prefix_match_returns_group_and_color.cs`:

```csharp
using DevHub.Models;
using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoGroupResolver_resolves;

public class Then_prefix_match_returns_group_and_color
{
    [Fact]
    public void Then_matching_prefix_returns_configured_group_name_and_color()
    {
        var rules = new List<GroupRule>
        {
            new() { Name = "Core", Color = "primary", Prefixes = ["Core.", "DevHub."] }
        };

        var (group, color) = RepoGroupResolver.Resolve("Core.Api", rules, "Other");

        group.ShouldBe("Core");
        color.ShouldBe("primary");
    }
}
```

`tests/DevHub.U.Tests/Services/When_RepoGroupResolver_resolves/Then_no_match_returns_default_group.cs`:

```csharp
using DevHub.Models;
using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoGroupResolver_resolves;

public class Then_no_match_returns_default_group
{
    [Fact]
    public void Then_unmatched_name_returns_default_group_and_default_color()
    {
        var rules = new List<GroupRule>
        {
            new() { Name = "Core", Color = "primary", Prefixes = ["Core."] }
        };

        var (group, color) = RepoGroupResolver.Resolve("Unknown.Service", rules, "Other");

        group.ShouldBe("Other");
        color.ShouldBe("default");
    }
}
```

- [ ] **Step 2: Ejecutar tests para verificar que fallan**

```bash
dotnet test tests/DevHub.U.Tests --filter "When_RepoGroupResolver_resolves"
```

Expected: FAIL — tipo no existe.

- [ ] **Step 3: Crear `src/DevHub/Services/RepoGroupResolver.cs`**

```csharp
using DevHub.Models;

namespace DevHub.Services;

public static class RepoGroupResolver
{
    public static (string Group, string Color) Resolve(
        string repoName, List<GroupRule> rules, string defaultGroup)
    {
        foreach (var rule in rules)
            if (rule.Prefixes.Any(p => repoName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                return (rule.Name, rule.Color);
        return (defaultGroup, "default");
    }
}
```

- [ ] **Step 4: Ejecutar tests para verificar que pasan**

```bash
dotnet test tests/DevHub.U.Tests --filter "When_RepoGroupResolver_resolves"
```

Expected: PASS.

- [ ] **Step 5: Actualizar `RepoScannerService.cs`**

Buscar en `TriggerScanAsync` la lógica inline de group resolution (el `FirstOrDefault` + `DetermineGroup` + `"default"` hardcodeado) y reemplazar con:

```csharp
var (group, groupColor) = RepoGroupResolver.Resolve(
    repoName, _options.Groups, _options.DefaultGroup);
```

Eliminar el método `public static DetermineGroup(...)` de `RepoScannerService`. Si hay tests que lo referencian como `RepoScannerService.DetermineGroup(...)`, actualizarlos a `RepoGroupResolver.Resolve(...)`.

- [ ] **Step 6: Ejecutar todos los tests**

```bash
dotnet test tests/DevHub.U.Tests
```

Expected: todos pasan.

- [ ] **Step 7: Commit**

```bash
git add src/DevHub/Services/RepoGroupResolver.cs src/DevHub/Services/RepoScannerService.cs tests/DevHub.U.Tests/Services/When_RepoGroupResolver_resolves/
git commit -m "refactor: extract RepoGroupResolver, unify group+color resolution"
```

---

## Task 6: Extraer `IGitService` (H-4)

**Files:**
- Create: `src/DevHub/Services/IGitService.cs`
- Modify: `src/DevHub/Services/GitCliService.cs`
- Modify: `src/DevHub/Services/RepoScannerService.cs`
- Modify: `src/DevHub/Components/BulkActions.razor`
- Modify: `src/DevHub/Program.cs`

### Contexto
`GitCliService` es concreto en todos los lugares. `RepoScannerService` y `BulkActions.razor` no se pueden testear en aislamiento sin el binario `git`. Extraer `IGitService`.

- [ ] **Step 1: Leer `GitCliService.cs` para copiar las firmas públicas**

```bash
grep -n "public.*Task\|public.*bool\|public.*string" src/DevHub/Services/GitCliService.cs
```

- [ ] **Step 2: Crear `src/DevHub/Services/IGitService.cs`**

```csharp
using DevHub.Models;

namespace DevHub.Services;

public interface IGitService
{
    Task<RepoInfo?> ScanRepoAsync(string repoPath, CancellationToken ct = default);
    Task<bool> FetchAsync(string repoPath, CancellationToken ct = default);
    Task<bool> PullAsync(string repoPath, CancellationToken ct = default);
    Task<bool> CheckoutAsync(string repoPath, string branch, CancellationToken ct = default);
    bool IsGitRepo(string path);
}
```

*(Ajustar firmas exactas a lo que `GitCliService` expone hoy — leer el archivo antes de crear la interfaz)*

- [ ] **Step 3: Hacer que `GitCliService` implemente `IGitService`**

```csharp
public class GitCliService(IProcessRunner processRunner) : IGitService
```

- [ ] **Step 4: Actualizar `Program.cs`**

```csharp
// ANTES
builder.Services.AddSingleton<GitCliService>();

// DESPUÉS
builder.Services.AddSingleton<IGitService, GitCliService>();
```

- [ ] **Step 5: Actualizar `RepoScannerService.cs` — cambiar el tipo del campo inyectado**

```csharp
// ANTES
private readonly GitCliService _gitService;
public RepoScannerService(..., GitCliService gitService, ...)

// DESPUÉS
private readonly IGitService _gitService;
public RepoScannerService(..., IGitService gitService, ...)
```

- [ ] **Step 6: Actualizar `BulkActions.razor` — cambiar `@inject`**

```razor
@* ANTES *@
@inject GitCliService GitService

@* DESPUÉS *@
@inject IGitService GitService
```

- [ ] **Step 7: Buildear**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores.

- [ ] **Step 8: Ejecutar todos los tests**

```bash
dotnet test tests/DevHub.U.Tests
```

Expected: todos pasan.

- [ ] **Step 9: Commit**

```bash
git add src/DevHub/Services/IGitService.cs src/DevHub/Services/GitCliService.cs src/DevHub/Services/RepoScannerService.cs src/DevHub/Components/BulkActions.razor src/DevHub/Program.cs
git commit -m "refactor: extract IGitService interface, decouple consumers from GitCliService"
```

---

## Task 7: FluentAPI en `ApplicationDbContext` — entidad limpia sin atributos (M-7)

**Files:**
- Modify: `src/DevHub/Models/RepoCatalogEntry.cs`
- Modify: `src/DevHub/Data/ApplicationDbContext.cs`
- Modify: `src/DevHub/Services/EfRepoCatalogService.cs` (ajustar si usa `Id` o `Path` para lookups)
- Tests a actualizar: cualquier test que construya `RepoCatalogEntry` con el constructor de dos parámetros

### Contexto
`RepoCatalogEntry` tiene atributos `[Table]`, `[Key]`, `[Column]`, `[MaxLength]` — infraestructura en el dominio. Se agrega `int Id` como surrogate PK. La tabla se llama `RepoCatalogEntries` por convención del DbSet.

- [ ] **Step 1: Leer archivos actuales**

```bash
cat src/DevHub/Models/RepoCatalogEntry.cs
cat src/DevHub/Data/ApplicationDbContext.cs
```

- [ ] **Step 2: Reemplazar `RepoCatalogEntry.cs`**

```csharp
namespace DevHub.Models;

public class RepoCatalogEntry
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public DateTime AddedUtc { get; set; }

    public RepoCatalogEntry() { }

    public RepoCatalogEntry(string path, DateTime addedUtc)
    {
        Path = path;
        AddedUtc = addedUtc;
    }
}
```

- [ ] **Step 3: Actualizar `ApplicationDbContext.cs`**

```csharp
using DevHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHub.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<RepoCatalogEntry> RepoCatalogEntries => Set<RepoCatalogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RepoCatalogEntry>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Path).HasMaxLength(1024).IsRequired();
            e.HasIndex(r => r.Path).IsUnique();
        });
    }
}
```

- [ ] **Step 4: Actualizar `EfRepoCatalogService.cs` — el DbSet ahora se llama `RepoCatalogEntries`**

Buscar cualquier referencia a `db.RepoCatalog` y cambiarla a `db.RepoCatalogEntries`:

```bash
grep -n "RepoCatalog" src/DevHub/Services/EfRepoCatalogService.cs
```

```csharp
// ANTES
db.RepoCatalog.AsNoTracking()...

// DESPUÉS
db.RepoCatalogEntries.AsNoTracking()...
```

- [ ] **Step 5: Buildear**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores.

- [ ] **Step 6: Ejecutar tests**

```bash
dotnet test tests/DevHub.U.Tests
```

Si algún test falla por la nueva columna `Id`, es porque la DB en memoria se creó con el schema viejo. Los tests usan `EnsureCreated` con SQLite in-memory — la conexión en memoria es nueva por test, así que el schema se recrea. Si aun así hay errores, borrar cualquier archivo `.db` de test.

Expected: todos pasan.

- [ ] **Step 7: Commit**

```bash
git add src/DevHub/Models/RepoCatalogEntry.cs src/DevHub/Data/ApplicationDbContext.cs src/DevHub/Services/EfRepoCatalogService.cs
git commit -m "refactor: move EF mapping to FluentAPI, add surrogate Id PK to RepoCatalogEntry"
```

---

## Task 8: Extraer `ColorHelper` (H-6)

**Files:**
- Create: `src/DevHub/Components/ColorHelper.cs`
- Modify: `src/DevHub/Components/RepoList.razor`
- Modify: `src/DevHub/Components/Pages/Settings.razor`

### Contexto
`ParseColor(string)` está duplicada verbatim en `RepoList.razor` y `Settings.razor`.

- [ ] **Step 1: Crear `src/DevHub/Components/ColorHelper.cs`**

```csharp
using MudBlazor;

namespace DevHub.Components;

public static class ColorHelper
{
    public static Color FromGroupColor(string color) => color.ToLowerInvariant() switch
    {
        "primary"   => Color.Primary,
        "secondary" => Color.Secondary,
        "tertiary"  => Color.Tertiary,
        "info"      => Color.Info,
        "success"   => Color.Success,
        "warning"   => Color.Warning,
        "error"     => Color.Error,
        _           => Color.Default
    };
}
```

- [ ] **Step 2: En `RepoList.razor` — reemplazar el método `ParseColor` inline**

Borrar el método `private static Color ParseColor(string color)` del `@code` block y cambiar todos los calls:
```razor
@* ANTES *@
Color="@ParseColor(context.Color)"

@* DESPUÉS *@
Color="@ColorHelper.FromGroupColor(context.Color)"
```

- [ ] **Step 3: En `Settings.razor` — igual**

Borrar el método `private static Color ParseColor(string color)` del `@code` block y cambiar todos los calls de la misma forma.

- [ ] **Step 4: Buildear**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores.

- [ ] **Step 5: Commit**

```bash
git add src/DevHub/Components/ColorHelper.cs src/DevHub/Components/RepoList.razor src/DevHub/Components/Pages/Settings.razor
git commit -m "refactor: extract ColorHelper, remove duplicated ParseColor"
```

---

## Task 9: `BulkActions.razor` — agregar `finally` para `_busy` (M-4)

**Files:**
- Modify: `src/DevHub/Components/BulkActions.razor`

### Contexto
`_busy = false` en `PullSelected` y `CheckoutSelected` no está en `finally`. Si `GitService.PullAsync` o `CheckoutAsync` lanzan excepción, los botones quedan deshabilitados permanentemente.

- [ ] **Step 1: Leer `BulkActions.razor` y localizar los métodos**

```bash
grep -n "_busy\|PullSelected\|CheckoutSelected" src/DevHub/Components/BulkActions.razor
```

- [ ] **Step 2: Envolver el cuerpo de cada método en `try/finally`**

```csharp
// ANTES
private async Task PullSelected()
{
    _busy = true;
    // ... operaciones ...
    _busy = false;
    await OnOperationCompleted.InvokeAsync();
}

// DESPUÉS
private async Task PullSelected()
{
    _busy = true;
    try
    {
        // ... operaciones ...
    }
    finally
    {
        _busy = false;
    }
    await OnOperationCompleted.InvokeAsync();
}
```

Aplicar el mismo patrón a `CheckoutSelected`.

- [ ] **Step 3: Buildear**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores.

- [ ] **Step 4: Commit**

```bash
git add src/DevHub/Components/BulkActions.razor
git commit -m "fix: wrap BulkActions busy operations in try/finally to prevent stuck state"
```

---

## Task 10: Renombrar métodos `Execute` → `Then_*` en todos los tests (M-1)

**Files:**
- Modify: todos los archivos en `tests/DevHub.U.Tests/` que contengan `public async Task Execute()`

### Contexto
La convención del proyecto es `When_<scenario>` para la clase y `Then_<result>` para el método. Cada archivo de test tiene la clase con el nombre correcto pero el método se llama `Execute`. El método debe tener el mismo nombre que la clase que lo contiene.

- [ ] **Step 1: Listar todos los archivos afectados**

```bash
grep -rl "public async Task Execute()" tests/DevHub.U.Tests/
```

- [ ] **Step 2: Para cada archivo, renombrar `Execute` con el nombre de la clase del archivo**

Regla: si la clase es `Then_IsDirty_is_false`, el método debe ser `Then_IsDirty_is_false`.

Ejemplo:
```csharp
// ANTES
public class Then_IsDirty_is_false
{
    [Fact]
    public async Task Execute() { ... }
}

// DESPUÉS
public class Then_IsDirty_is_false
{
    [Fact]
    public async Task Then_IsDirty_is_false() { ... }
}
```

Aplicar a todos los archivos de la lista del Step 1.

- [ ] **Step 3: Ejecutar todos los tests**

```bash
dotnet test tests/DevHub.U.Tests
```

Expected: todos pasan (solo renombrado, sin cambio de lógica).

- [ ] **Step 4: Commit**

```bash
git add tests/DevHub.U.Tests/
git commit -m "test: rename Execute → Then_* to match When/Then convention"
```

---

## Task 11: Eliminar `@inject IJSRuntime` sin usar en `PromptDialog.razor` (L-4)

**Files:**
- Modify: `src/DevHub/Components/Pages/PromptDialog.razor`

- [ ] **Step 1: Verificar que `JS` no se usa**

```bash
grep -n "JS\b\|IJSRuntime" src/DevHub/Components/Pages/PromptDialog.razor
```

- [ ] **Step 2: Eliminar la línea `@inject IJSRuntime JS`**

- [ ] **Step 3: Buildear**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores.

- [ ] **Step 4: Commit**

```bash
git add src/DevHub/Components/Pages/PromptDialog.razor
git commit -m "cleanup: remove unused IJSRuntime injection from PromptDialog"
```

---

## Task 12: Mover credenciales a `appsettings.Development.json` (C-3)

**Files:**
- Modify: `src/DevHub/appsettings.json`
- Create: `src/DevHub/appsettings.Development.json`
- Modify: `.gitignore` (raíz del repo)

- [ ] **Step 1: Crear `src/DevHub/appsettings.Development.json`**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=DESKTOP-SLPCU2M;Database=DevHub;User Id=sa;Password=hola123;TrustServerCertificate=True;Encrypt=False"
  }
}
```

- [ ] **Step 2: En `src/DevHub/appsettings.json`, vaciar el connection string**

```json
"ConnectionStrings": {
  "DefaultConnection": ""
}
```

- [ ] **Step 3: Verificar que `.gitignore` en la raíz ignora `appsettings.Development.json`**

```bash
grep "appsettings.Development" .gitignore
```

Si no está, agregar:
```
appsettings.Development.json
```

- [ ] **Step 4: Ejecutar la app para verificar que sigue conectándose**

ASP.NET Core carga `appsettings.json` y luego `appsettings.{Environment}.json` — en Development el archivo de credenciales sobreescribe el connection string vacío automáticamente.

```bash
dotnet run --project src/DevHub/DevHub.csproj
```

Verificar que la página /settings carga sin errores de conexión.

- [ ] **Step 5: Commit solo `appsettings.json` y `.gitignore` — NO commitear `appsettings.Development.json`**

```bash
git add src/DevHub/appsettings.json .gitignore
git commit -m "security: move SQL Server credentials to appsettings.Development.json (gitignored)"
```

---

*12 tasks · 5 Critical + 4 High + 3 Medium/Low fixes. Ejecutar en orden — cada task buildea y pasa tests antes del commit.*
