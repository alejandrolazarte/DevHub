# Repo Orchestra Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir una app local Blazor Server que muestra el estado git de los ~77 repos de Woffu en tiempo real, con filtros y operaciones bulk (pull, checkout).

**Architecture:** Blazor Server (.NET 10) con un `IHostedService` que escanea repos cada 60s usando `git` CLI via `Process.Start`. El estado vive en un singleton `RepoStateStore` que dispara `OnStateChanged` para que la UI Blazor re-renderice automáticamente. Un botón Refresh permite trigger manual.

**Tech Stack:** .NET 10 · Blazor Server · MudBlazor (MIT) · xUnit + FluentAssertions · git CLI

---

## File Map

```
Woffu.Tools.RepoOrchestra/
  Models/
    RepoInfo.cs
    RepoGroup.cs
  Services/
    GitCliService.cs
    RepoStateStore.cs
    RepoScannerService.cs
  Components/
    FilterBar.razor
    BulkActions.razor
    RepoRow.razor
    RepoList.razor
  Pages/
    Home.razor
  Layout/
    MainLayout.razor
    MainLayout.razor.css
  Program.cs
  appsettings.json
  Woffu.Tools.RepoOrchestra.csproj

Woffu.Tools.RepoOrchestra.U.Tests/
  Services/
    When_GitCliService_scans_clean_repo/
      Then_IsDirty_is_false.cs
    When_GitCliService_scans_dirty_repo/
      Then_IsDirty_is_true.cs
    When_GitCliService_scans_repo_with_remote_ahead/
      Then_BehindCount_is_correct.cs
    When_RepoStateStore_is_updated/
      Then_OnStateChanged_fires.cs
    When_RepoStateStore_is_updated_concurrently/
      Then_state_is_consistent.cs
    When_RepoScannerService_starts/
      Then_repos_are_discovered.cs
  Helpers/
    TempGitRepo.cs
  Woffu.Tools.RepoOrchestra.U.Tests.csproj
```

---

## Task 1: Crear solución y proyectos

**Files:**
- Create: `Woffu.Tools.RepoOrchestra/Woffu.Tools.RepoOrchestra.csproj`
- Create: `Woffu.Tools.RepoOrchestra.U.Tests/Woffu.Tools.RepoOrchestra.U.Tests.csproj`
- Create: `RepoOrchestra.sln`

- [ ] **Step 1: Crear la solución y el proyecto Blazor Server**

```bash
cd C:/woffu-orchestra
dotnet new sln -n RepoOrchestra
dotnet new blazorserver -n Woffu.Tools.RepoOrchestra --framework net10.0
dotnet sln add Woffu.Tools.RepoOrchestra/Woffu.Tools.RepoOrchestra.csproj
```

- [ ] **Step 2: Añadir MudBlazor al proyecto**

```bash
cd Woffu.Tools.RepoOrchestra
dotnet add package MudBlazor
```

- [ ] **Step 3: Crear el proyecto de tests**

```bash
cd C:/woffu-orchestra
dotnet new xunit -n Woffu.Tools.RepoOrchestra.U.Tests --framework net10.0
dotnet sln add Woffu.Tools.RepoOrchestra.U.Tests/Woffu.Tools.RepoOrchestra.U.Tests.csproj
cd Woffu.Tools.RepoOrchestra.U.Tests
dotnet add package FluentAssertions
dotnet add reference ../Woffu.Tools.RepoOrchestra/Woffu.Tools.RepoOrchestra.csproj
```

- [ ] **Step 4: Limpiar archivos de ejemplo del template**

Eliminar del proyecto Blazor:
- `Pages/Counter.razor`
- `Pages/FetchData.razor`
- `Pages/Error.razor`
- `Shared/SurveyPrompt.razor`
- `Data/WeatherForecast.cs`
- `Data/WeatherForecastService.cs`

Eliminar del proyecto de tests:
- `UnitTest1.cs`

- [ ] **Step 5: Verificar que compila**

```bash
cd C:/woffu-orchestra
dotnet build RepoOrchestra.sln
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git -C C:/woffu-orchestra add RepoOrchestra.sln Woffu.Tools.RepoOrchestra/ Woffu.Tools.RepoOrchestra.U.Tests/
git -C C:/woffu-orchestra commit -m "feat: scaffold Repo Orchestra solution (Blazor Server + tests)"
```

---

## Task 2: Modelos

**Files:**
- Create: `Woffu.Tools.RepoOrchestra/Models/RepoInfo.cs`
- Create: `Woffu.Tools.RepoOrchestra/Models/RepoGroup.cs`

- [ ] **Step 1: Crear RepoInfo**

`Woffu.Tools.RepoOrchestra/Models/RepoInfo.cs`
```csharp
namespace Woffu.Tools.RepoOrchestra.Models;

public record RepoInfo
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string Group { get; init; }
    public string Branch { get; init; } = string.Empty;
    public bool IsDirty { get; init; }
    public int DirtyFileCount { get; init; }
    public int AheadCount { get; init; }
    public int BehindCount { get; init; }
    public string LastCommitMessage { get; init; } = string.Empty;
    public DateTime LastScanned { get; init; }
    public bool IsGitRepo { get; init; } = true;
    public string? ScanError { get; init; }
}
```

- [ ] **Step 2: Crear RepoGroup**

`Woffu.Tools.RepoOrchestra/Models/RepoGroup.cs`
```csharp
namespace Woffu.Tools.RepoOrchestra.Models;

public record RepoGroup(string Name, IReadOnlyList<RepoInfo> Repos)
{
    public bool AllClean => Repos.All(r => !r.IsDirty && r.BehindCount == 0);
    public bool AllOnMaster => Repos.All(r => r.Branch is "master" or "main");
    public bool AutoCollapse => AllClean && AllOnMaster;
}
```

- [ ] **Step 3: Verificar compilación**

```bash
cd C:/woffu-orchestra
dotnet build RepoOrchestra.sln
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git -C C:/woffu-orchestra add Woffu.Tools.RepoOrchestra/Models/
git -C C:/woffu-orchestra commit -m "feat: add RepoInfo and RepoGroup models"
```

---

## Task 3: GitCliService (TDD)

**Files:**
- Create: `Woffu.Tools.RepoOrchestra/Services/GitCliService.cs`
- Create: `Woffu.Tools.RepoOrchestra.U.Tests/Helpers/TempGitRepo.cs`
- Create: `Woffu.Tools.RepoOrchestra.U.Tests/Services/When_GitCliService_scans_clean_repo/Then_IsDirty_is_false.cs`
- Create: `Woffu.Tools.RepoOrchestra.U.Tests/Services/When_GitCliService_scans_dirty_repo/Then_IsDirty_is_true.cs`
- Create: `Woffu.Tools.RepoOrchestra.U.Tests/Services/When_GitCliService_scans_repo_with_remote_ahead/Then_BehindCount_is_correct.cs`

- [ ] **Step 1: Crear el helper TempGitRepo para los tests**

`Woffu.Tools.RepoOrchestra.U.Tests/Helpers/TempGitRepo.cs`
```csharp
namespace Woffu.Tools.RepoOrchestra.U.Tests.Helpers;

public sealed class TempGitRepo : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public TempGitRepo()
    {
        Directory.CreateDirectory(Path);
        Run("git init");
        Run("git config user.email test@test.com");
        Run("git config user.name Test");
        // Commit inicial vacío para que HEAD exista
        Run("git commit --allow-empty -m \"initial\"");
    }

    public void CreateFile(string name, string content = "content")
    {
        File.WriteAllText(System.IO.Path.Combine(Path, name), content);
    }

    public void StageAndCommit(string message = "commit")
    {
        Run("git add -A");
        Run($"git commit -m \"{message}\"");
    }

    private void Run(string command)
    {
        var parts = command.Split(' ', 2);
        using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = parts[0],
            Arguments = parts.Length > 1 ? parts[1] : string.Empty,
            WorkingDirectory = Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        p.WaitForExit();
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
```

- [ ] **Step 2: Escribir el test — repo limpio → IsDirty false**

`Woffu.Tools.RepoOrchestra.U.Tests/Services/When_GitCliService_scans_clean_repo/Then_IsDirty_is_false.cs`
```csharp
using FluentAssertions;
using Woffu.Tools.RepoOrchestra.Services;
using Woffu.Tools.RepoOrchestra.U.Tests.Helpers;

namespace Woffu.Tools.RepoOrchestra.U.Tests.Services.When_GitCliService_scans_clean_repo;

public class Then_IsDirty_is_false
{
    [Fact]
    public async Task Execute()
    {
        using var repo = new TempGitRepo();
        var sut = new GitCliService();

        var result = await sut.GetStatusAsync(repo.Path);

        result.IsDirty.Should().BeFalse();
        result.DirtyFileCount.Should().Be(0);
    }
}
```

- [ ] **Step 3: Ejecutar el test — debe fallar porque GitCliService no existe aún**

```bash
cd C:/woffu-orchestra
dotnet test Woffu.Tools.RepoOrchestra.U.Tests/ --filter "Then_IsDirty_is_false"
```
Expected: error de compilación o `FAILED` — `GitCliService` no existe.

- [ ] **Step 4: Implementar GitCliService**

`Woffu.Tools.RepoOrchestra/Services/GitCliService.cs`
```csharp
using System.Diagnostics;
using Woffu.Tools.RepoOrchestra.Models;

namespace Woffu.Tools.RepoOrchestra.Services;

public class GitCliService
{
    public async Task<(bool IsDirty, int DirtyFileCount)> GetStatusAsync(
        string repoPath, CancellationToken ct = default)
    {
        var (output, _, exitCode) = await RunGitAsync(repoPath, "status --porcelain", ct);
        if (exitCode != 0) return (false, 0);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return (lines.Length > 0, lines.Length);
    }

    public async Task<string> GetBranchAsync(
        string repoPath, CancellationToken ct = default)
    {
        var (output, _, exitCode) = await RunGitAsync(repoPath, "branch --show-current", ct);
        return exitCode == 0 ? output : string.Empty;
    }

    public async Task<(int Ahead, int Behind)> GetAheadBehindAsync(
        string repoPath, CancellationToken ct = default)
    {
        // Requiere que exista un remote tracking branch
        var (output, _, exitCode) = await RunGitAsync(
            repoPath, "rev-list --count --left-right HEAD...@{u}", ct);
        if (exitCode != 0) return (0, 0);
        var parts = output.Split('\t');
        if (parts.Length != 2) return (0, 0);
        return (int.TryParse(parts[0], out var ahead) ? ahead : 0,
                int.TryParse(parts[1], out var behind) ? behind : 0);
    }

    public async Task<string> GetLastCommitMessageAsync(
        string repoPath, CancellationToken ct = default)
    {
        var (output, _, exitCode) = await RunGitAsync(repoPath, "log -1 --pretty=%s", ct);
        return exitCode == 0 ? output : string.Empty;
    }

    public async Task<(bool Success, string Error)> PullAsync(
        string repoPath, CancellationToken ct = default)
    {
        var (_, error, exitCode) = await RunGitAsync(repoPath, "pull --ff-only", ct);
        return (exitCode == 0, error);
    }

    public async Task<(bool Success, string Error)> CheckoutAsync(
        string repoPath, string branch, CancellationToken ct = default)
    {
        // Intenta checkout normal; si falla, intenta crear la rama
        var (_, error, exitCode) = await RunGitAsync(repoPath, $"checkout {branch}", ct);
        if (exitCode == 0) return (true, string.Empty);

        var (_, error2, exitCode2) = await RunGitAsync(repoPath, $"checkout -b {branch}", ct);
        return (exitCode2 == 0, exitCode2 != 0 ? error2 : string.Empty);
    }

    public async Task<RepoInfo> ScanRepoAsync(
        string repoPath, string group, CancellationToken ct = default)
    {
        var name = Path.GetFileName(repoPath);
        var gitDir = Path.Combine(repoPath, ".git");

        if (!Directory.Exists(gitDir))
            return new RepoInfo
            {
                Name = name, Path = repoPath, Group = group,
                IsGitRepo = false, LastScanned = DateTime.Now
            };

        try
        {
            var statusTask = GetStatusAsync(repoPath, ct);
            var branchTask = GetBranchAsync(repoPath, ct);
            var aheadBehindTask = GetAheadBehindAsync(repoPath, ct);
            var lastCommitTask = GetLastCommitMessageAsync(repoPath, ct);

            await Task.WhenAll(statusTask, branchTask, aheadBehindTask, lastCommitTask);

            var (isDirty, dirtyCount) = await statusTask;
            var (ahead, behind) = await aheadBehindTask;

            return new RepoInfo
            {
                Name = name,
                Path = repoPath,
                Group = group,
                Branch = await branchTask,
                IsDirty = isDirty,
                DirtyFileCount = dirtyCount,
                AheadCount = ahead,
                BehindCount = behind,
                LastCommitMessage = await lastCommitTask,
                LastScanned = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            return new RepoInfo
            {
                Name = name, Path = repoPath, Group = group,
                ScanError = ex.Message, LastScanned = DateTime.Now
            };
        }
    }

    private static async Task<(string Output, string Error, int ExitCode)> RunGitAsync(
        string workingDir, string arguments, CancellationToken ct = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (output.Trim(), error.Trim(), process.ExitCode);
    }
}
```

- [ ] **Step 5: Ejecutar el test — debe pasar**

```bash
cd C:/woffu-orchestra
dotnet test Woffu.Tools.RepoOrchestra.U.Tests/ --filter "Then_IsDirty_is_false"
```
Expected: `Passed!`

- [ ] **Step 6: Escribir test — repo dirty → IsDirty true**

`Woffu.Tools.RepoOrchestra.U.Tests/Services/When_GitCliService_scans_dirty_repo/Then_IsDirty_is_true.cs`
```csharp
using FluentAssertions;
using Woffu.Tools.RepoOrchestra.Services;
using Woffu.Tools.RepoOrchestra.U.Tests.Helpers;

namespace Woffu.Tools.RepoOrchestra.U.Tests.Services.When_GitCliService_scans_dirty_repo;

public class Then_IsDirty_is_true
{
    [Fact]
    public async Task Execute()
    {
        using var repo = new TempGitRepo();
        repo.CreateFile("dirty.txt", "untracked content");
        var sut = new GitCliService();

        var result = await sut.GetStatusAsync(repo.Path);

        result.IsDirty.Should().BeTrue();
        result.DirtyFileCount.Should().Be(1);
    }
}
```

- [ ] **Step 7: Ejecutar el test — debe pasar**

```bash
cd C:/woffu-orchestra
dotnet test Woffu.Tools.RepoOrchestra.U.Tests/ --filter "Then_IsDirty_is_true"
```
Expected: `Passed!`

- [ ] **Step 8: Ejecutar todos los tests**

```bash
cd C:/woffu-orchestra
dotnet test Woffu.Tools.RepoOrchestra.U.Tests/
```
Expected: todos `Passed!`

- [ ] **Step 9: Commit**

```bash
git -C C:/woffu-orchestra add Woffu.Tools.RepoOrchestra/Services/GitCliService.cs Woffu.Tools.RepoOrchestra.U.Tests/
git -C C:/woffu-orchestra commit -m "feat: implement GitCliService with TDD (status, branch, ahead/behind, pull, checkout)"
```

---

## Task 4: RepoStateStore (TDD)

**Files:**
- Create: `Woffu.Tools.RepoOrchestra/Services/RepoStateStore.cs`
- Create: `Woffu.Tools.RepoOrchestra.U.Tests/Services/When_RepoStateStore_is_updated/Then_OnStateChanged_fires.cs`
- Create: `Woffu.Tools.RepoOrchestra.U.Tests/Services/When_RepoStateStore_is_updated_concurrently/Then_state_is_consistent.cs`

- [ ] **Step 1: Escribir test — OnStateChanged se dispara al actualizar**

`Woffu.Tools.RepoOrchestra.U.Tests/Services/When_RepoStateStore_is_updated/Then_OnStateChanged_fires.cs`
```csharp
using FluentAssertions;
using Woffu.Tools.RepoOrchestra.Models;
using Woffu.Tools.RepoOrchestra.Services;

namespace Woffu.Tools.RepoOrchestra.U.Tests.Services.When_RepoStateStore_is_updated;

public class Then_OnStateChanged_fires
{
    [Fact]
    public void Execute()
    {
        var sut = new RepoStateStore();
        var fired = false;
        sut.OnStateChanged += () => fired = true;

        sut.Update([new RepoInfo { Name = "A", Path = "/a", Group = "G" }]);

        fired.Should().BeTrue();
        sut.Repos.Should().HaveCount(1);
    }
}
```

- [ ] **Step 2: Ejecutar el test — debe fallar**

```bash
cd C:/woffu-orchestra
dotnet test Woffu.Tools.RepoOrchestra.U.Tests/ --filter "Then_OnStateChanged_fires"
```
Expected: error de compilación — `RepoStateStore` no existe.

- [ ] **Step 3: Implementar RepoStateStore**

`Woffu.Tools.RepoOrchestra/Services/RepoStateStore.cs`
```csharp
using Woffu.Tools.RepoOrchestra.Models;

namespace Woffu.Tools.RepoOrchestra.Services;

public class RepoStateStore
{
    private readonly object _lock = new();
    private IReadOnlyList<RepoInfo> _repos = [];

    public IReadOnlyList<RepoInfo> Repos
    {
        get { lock (_lock) return _repos; }
    }

    public bool IsScanning { get; private set; }
    public DateTime LastScanCompleted { get; private set; }

    public event Action? OnStateChanged;

    public void Update(IReadOnlyList<RepoInfo> repos)
    {
        lock (_lock) _repos = repos;
        LastScanCompleted = DateTime.Now;
        OnStateChanged?.Invoke();
    }

    public void SetScanning(bool isScanning)
    {
        IsScanning = isScanning;
        OnStateChanged?.Invoke();
    }
}
```

- [ ] **Step 4: Ejecutar el test — debe pasar**

```bash
cd C:/woffu-orchestra
dotnet test Woffu.Tools.RepoOrchestra.U.Tests/ --filter "Then_OnStateChanged_fires"
```
Expected: `Passed!`

- [ ] **Step 5: Escribir test — actualizaciones concurrentes no corrompen el estado**

`Woffu.Tools.RepoOrchestra.U.Tests/Services/When_RepoStateStore_is_updated_concurrently/Then_state_is_consistent.cs`
```csharp
using FluentAssertions;
using Woffu.Tools.RepoOrchestra.Models;
using Woffu.Tools.RepoOrchestra.Services;

namespace Woffu.Tools.RepoOrchestra.U.Tests.Services.When_RepoStateStore_is_updated_concurrently;

public class Then_state_is_consistent
{
    [Fact]
    public async Task Execute()
    {
        var sut = new RepoStateStore();

        await Parallel.ForEachAsync(Enumerable.Range(0, 20), async (i, _) =>
        {
            await Task.Yield();
            sut.Update([new RepoInfo { Name = $"Repo{i}", Path = $"/r{i}", Group = "G" }]);
        });

        // No debe lanzar excepción y el estado debe ser una lista válida
        sut.Repos.Should().NotBeNull();
        sut.Repos.Count.Should().Be(1); // siempre reemplaza la lista completa
    }
}
```

- [ ] **Step 6: Ejecutar todos los tests**

```bash
cd C:/woffu-orchestra
dotnet test Woffu.Tools.RepoOrchestra.U.Tests/
```
Expected: todos `Passed!`

- [ ] **Step 7: Commit**

```bash
git -C C:/woffu-orchestra add Woffu.Tools.RepoOrchestra/Services/RepoStateStore.cs Woffu.Tools.RepoOrchestra.U.Tests/
git -C C:/woffu-orchestra commit -m "feat: implement RepoStateStore with thread-safe state and OnStateChanged event"
```

---

## Task 5: RepoScannerService (TDD)

**Files:**
- Create: `Woffu.Tools.RepoOrchestra/Services/RepoScannerService.cs`
- Create: `Woffu.Tools.RepoOrchestra.U.Tests/Services/When_RepoScannerService_starts/Then_repos_are_discovered.cs`

- [ ] **Step 1: Escribir test — al arrancar descubre repos con .git/**

`Woffu.Tools.RepoOrchestra.U.Tests/Services/When_RepoScannerService_starts/Then_repos_are_discovered.cs`
```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Woffu.Tools.RepoOrchestra.Services;
using Woffu.Tools.RepoOrchestra.U.Tests.Helpers;

namespace Woffu.Tools.RepoOrchestra.U.Tests.Services.When_RepoScannerService_starts;

public class Then_repos_are_discovered
{
    [Fact]
    public async Task Execute()
    {
        // Crear dos repos temporales dentro de un directorio raíz
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        using var repo1 = new TempGitRepoAt(Path.Combine(rootPath, "RepoA"));
        using var repo2 = new TempGitRepoAt(Path.Combine(rootPath, "RepoB"));
        // Directorio sin .git (debe ignorarse)
        Directory.CreateDirectory(Path.Combine(rootPath, "NotARepo"));

        var store = new RepoStateStore();
        var gitService = new GitCliService();
        var options = Options.Create(new RepoOrchestraOptions
        {
            RootPath = rootPath,
            ScanIntervalSeconds = 3600 // no auto-scan en el test
        });

        var sut = new RepoScannerService(gitService, store, options,
            NullLogger<RepoScannerService>.Instance);

        await sut.TriggerScanAsync(CancellationToken.None);

        store.Repos.Should().HaveCount(2);
        store.Repos.Select(r => r.Name).Should().Contain(["RepoA", "RepoB"]);
        store.Repos.Should().OnlyContain(r => r.IsGitRepo);

        Directory.Delete(rootPath, recursive: true);
    }
}
```

> **Nota:** Este test requiere un helper adicional `TempGitRepoAt` que crea el repo en un path fijo en lugar de en un GUID de temp. Añádelo en el siguiente step.

- [ ] **Step 2: Añadir TempGitRepoAt al helper**

Abre `Woffu.Tools.RepoOrchestra.U.Tests/Helpers/TempGitRepo.cs` y añade al final del archivo (misma clase, no nuevo archivo):

```csharp
// Al final del namespace, clase separada
public sealed class TempGitRepoAt : IDisposable
{
    public string Path { get; }

    public TempGitRepoAt(string path)
    {
        Path = path;
        Directory.CreateDirectory(path);
        Run("git init");
        Run("git config user.email test@test.com");
        Run("git config user.name Test");
        Run("git commit --allow-empty -m \"initial\"");
    }

    private void Run(string command)
    {
        var parts = command.Split(' ', 2);
        using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = parts[0],
            Arguments = parts.Length > 1 ? parts[1] : string.Empty,
            WorkingDirectory = Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        p.WaitForExit();
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
```

- [ ] **Step 3: Ejecutar el test — debe fallar**

```bash
cd C:/woffu-orchestra
dotnet test Woffu.Tools.RepoOrchestra.U.Tests/ --filter "Then_repos_are_discovered"
```
Expected: error de compilación — `RepoScannerService` y `RepoOrchestraOptions` no existen.

- [ ] **Step 4: Crear RepoOrchestraOptions**

`Woffu.Tools.RepoOrchestra/Services/RepoScannerService.cs` — al principio del archivo, antes de la clase `RepoScannerService`:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Woffu.Tools.RepoOrchestra.Models;

namespace Woffu.Tools.RepoOrchestra.Services;

public class RepoOrchestraOptions
{
    public string RootPath { get; set; } = @"C:\woffu-orchestra";
    public int ScanIntervalSeconds { get; set; } = 60;
    public int ParallelScanDegree { get; set; } = 8;
    public List<string> ExcludedRepos { get; set; } = [];
}

public class RepoScannerService(
    GitCliService gitService,
    RepoStateStore store,
    IOptions<RepoOrchestraOptions> options,
    ILogger<RepoScannerService> logger) : BackgroundService
{
    private readonly RepoOrchestraOptions _options = options.Value;
    private readonly SemaphoreSlim _triggerSemaphore = new(0, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TriggerScanAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.WhenAny(
                    Task.Delay(TimeSpan.FromSeconds(_options.ScanIntervalSeconds), stoppingToken),
                    _triggerSemaphore.WaitAsync(stoppingToken).ContinueWith(_ => { }, stoppingToken));

                if (!stoppingToken.IsCancellationRequested)
                    await TriggerScanAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task TriggerScanAsync(CancellationToken ct = default)
    {
        store.SetScanning(true);
        try
        {
            var repoPaths = DiscoverRepos();
            logger.LogInformation("Scanning {Count} repos", repoPaths.Count);

            var results = new RepoInfo[repoPaths.Count];
            await Parallel.ForEachAsync(
                repoPaths.Select((path, i) => (path, i)),
                new ParallelOptions { MaxDegreeOfParallelism = _options.ParallelScanDegree, CancellationToken = ct },
                async (item, token) =>
                {
                    var group = DetermineGroup(Path.GetFileName(item.path));
                    results[item.i] = await gitService.ScanRepoAsync(item.path, group, token);
                });

            store.Update(results);
            logger.LogInformation("Scan complete");
        }
        finally
        {
            store.SetScanning(false);
        }
    }

    public void RequestManualRefresh()
    {
        try { _triggerSemaphore.Release(); }
        catch (SemaphoreFullException) { /* ya hay un trigger pendiente */ }
    }

    private List<string> DiscoverRepos() =>
        Directory.GetDirectories(_options.RootPath)
            .Where(d => Directory.Exists(Path.Combine(d, ".git")))
            .Where(d => !_options.ExcludedRepos.Contains(Path.GetFileName(d)))
            .OrderBy(d => d)
            .ToList();

    private static string DetermineGroup(string repoName) => repoName switch
    {
        var n when n.StartsWith("Woffu.Services.") => "Services",
        var n when n.StartsWith("Woffu.Library.") => "Libraries",
        var n when n.StartsWith("Woffu.Functions.") => "Functions",
        var n when n.StartsWith("Woffu.Frontend.") => "Frontend",
        var n when n.StartsWith("Devops.") => "DevOps",
        var n when n.StartsWith("Woffu.Tools.") || n.StartsWith("Woffu.Utils.") => "Tools",
        _ => "Other"
    };
}
```

- [ ] **Step 5: Ejecutar el test — debe pasar**

```bash
cd C:/woffu-orchestra
dotnet test Woffu.Tools.RepoOrchestra.U.Tests/ --filter "Then_repos_are_discovered"
```
Expected: `Passed!`

- [ ] **Step 6: Ejecutar todos los tests**

```bash
cd C:/woffu-orchestra
dotnet test Woffu.Tools.RepoOrchestra.U.Tests/
```
Expected: todos `Passed!`

- [ ] **Step 7: Commit**

```bash
git -C C:/woffu-orchestra add Woffu.Tools.RepoOrchestra/Services/RepoScannerService.cs Woffu.Tools.RepoOrchestra.U.Tests/
git -C C:/woffu-orchestra commit -m "feat: implement RepoScannerService with discovery, parallel scan, and manual trigger"
```

---

## Task 6: DI, configuración y Program.cs

**Files:**
- Modify: `Woffu.Tools.RepoOrchestra/Program.cs`
- Modify: `Woffu.Tools.RepoOrchestra/appsettings.json`

- [ ] **Step 1: Actualizar appsettings.json**

`Woffu.Tools.RepoOrchestra/appsettings.json`
```json
{
  "RepoOrchestra": {
    "RootPath": "C:\\_O",
    "ScanIntervalSeconds": 60,
    "ParallelScanDegree": 8,
    "ExcludedRepos": []
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Urls": "http://localhost:5200"
}
```

- [ ] **Step 2: Actualizar Program.cs**

`Woffu.Tools.RepoOrchestra/Program.cs`
```csharp
using MudBlazor.Services;
using Woffu.Tools.RepoOrchestra.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

builder.Services.Configure<RepoOrchestraOptions>(
    builder.Configuration.GetSection("RepoOrchestra"));

builder.Services.AddSingleton<GitCliService>();
builder.Services.AddSingleton<RepoStateStore>();
builder.Services.AddHostedService<RepoScannerService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
```

- [ ] **Step 3: Verificar compilación**

```bash
cd C:/woffu-orchestra
dotnet build Woffu.Tools.RepoOrchestra/
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git -C C:/woffu-orchestra add Woffu.Tools.RepoOrchestra/Program.cs Woffu.Tools.RepoOrchestra/appsettings.json
git -C C:/woffu-orchestra commit -m "feat: configure DI, MudBlazor, and RepoOrchestra settings"
```

---

## Task 7: MudBlazor setup y layout

**Files:**
- Modify: `Woffu.Tools.RepoOrchestra/Pages/_Host.cshtml` (o `App.razor` según template)
- Modify: `Woffu.Tools.RepoOrchestra/Layout/MainLayout.razor`
- Modify: `Woffu.Tools.RepoOrchestra/wwwroot/index.html` o `_Imports.razor`

- [ ] **Step 1: Añadir MudBlazor a _Imports.razor**

`Woffu.Tools.RepoOrchestra/_Imports.razor` — añadir al final:
```razor
@using MudBlazor
```

- [ ] **Step 2: Añadir referencias CSS/JS de MudBlazor en _Host.cshtml**

Abrir `Pages/_Host.cshtml`. En el `<head>`, añadir:
```html
<link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
```
Justo antes de `</body>`, añadir:
```html
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

- [ ] **Step 3: Actualizar MainLayout.razor**

`Woffu.Tools.RepoOrchestra/Layout/MainLayout.razor`
```razor
@inherits LayoutComponentBase

<MudThemeProvider Theme="_theme" IsDarkMode="true" />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudMainContent>
        @Body
    </MudMainContent>
</MudLayout>

@code {
    private MudTheme _theme = new()
    {
        PaletteLight = new PaletteLight { Primary = "#c084fc" },
        PaletteDark = new PaletteDark
        {
            Primary = "#c084fc",
            Background = "#0f0f14",
            Surface = "#16161f",
            AppbarBackground = "#16161f",
            DrawerBackground = "#12121a"
        }
    };
}
```

- [ ] **Step 4: Verificar que arranca**

```bash
cd C:/woffu-orchestra/Woffu.Tools.RepoOrchestra
dotnet run
```
Abrir `http://localhost:5200`. Debe mostrar la página por defecto sin errores en consola.
Parar con `Ctrl+C`.

- [ ] **Step 5: Commit**

```bash
git -C C:/woffu-orchestra add Woffu.Tools.RepoOrchestra/
git -C C:/woffu-orchestra commit -m "feat: setup MudBlazor dark theme layout"
```

---

## Task 8: Componente FilterBar

**Files:**
- Create: `Woffu.Tools.RepoOrchestra/Components/FilterBar.razor`

- [ ] **Step 1: Crear FilterBar.razor**

`Woffu.Tools.RepoOrchestra/Components/FilterBar.razor`
```razor
<MudPaper Class="pa-2 mb-2" Elevation="0" Style="background: var(--mud-palette-surface)">
    <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2" Wrap="Wrap.Wrap">

        <MudTextField @bind-Value="SearchText"
                      Placeholder="Filtrar repos..."
                      Adornment="Adornment.Start"
                      AdornmentIcon="@Icons.Material.Filled.Search"
                      Variant="Variant.Outlined"
                      Margin="Margin.Dense"
                      Style="width:200px"
                      Immediate="true"
                      ValueChanged="OnFilterChanged" />

        <MudSelect T="string"
                   @bind-Value="SelectedGroup"
                   Variant="Variant.Outlined"
                   Margin="Margin.Dense"
                   Style="width:160px"
                   ValueChanged="OnFilterChanged">
            <MudSelectItem Value="@("")">Todos los grupos</MudSelectItem>
            @foreach (var group in Groups)
            {
                <MudSelectItem Value="@group">@group</MudSelectItem>
            }
        </MudSelect>

        <MudSelect T="string"
                   @bind-Value="SelectedStatus"
                   Variant="Variant.Outlined"
                   Margin="Margin.Dense"
                   Style="width:160px"
                   ValueChanged="OnFilterChanged">
            <MudSelectItem Value="@("")">Todos los estados</MudSelectItem>
            <MudSelectItem Value="dirty">⚠ Dirty</MudSelectItem>
            <MudSelectItem Value="clean">✓ Clean</MudSelectItem>
            <MudSelectItem Value="behind">↓ Behind</MudSelectItem>
            <MudSelectItem Value="feature">⎇ Feature branch</MudSelectItem>
        </MudSelect>

    </MudStack>
</MudPaper>

@code {
    [Parameter] public IReadOnlyList<string> Groups { get; set; } = [];
    [Parameter] public EventCallback<FilterCriteria> OnFilterChanged { get; set; }

    private string SearchText { get; set; } = string.Empty;
    private string SelectedGroup { get; set; } = string.Empty;
    private string SelectedStatus { get; set; } = string.Empty;

    private void OnFilterChanged(string _) =>
        OnFilterChanged.InvokeAsync(new FilterCriteria(SearchText, SelectedGroup, SelectedStatus));
}

public record FilterCriteria(string Search, string Group, string Status);
```

- [ ] **Step 2: Verificar compilación**

```bash
cd C:/woffu-orchestra
dotnet build Woffu.Tools.RepoOrchestra/
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git -C C:/woffu-orchestra add Woffu.Tools.RepoOrchestra/Components/FilterBar.razor
git -C C:/woffu-orchestra commit -m "feat: add FilterBar component (search, group, status filters)"
```

---

## Task 9: Componente RepoRow

**Files:**
- Create: `Woffu.Tools.RepoOrchestra/Components/RepoRow.razor`

- [ ] **Step 1: Crear RepoRow.razor**

`Woffu.Tools.RepoOrchestra/Components/RepoRow.razor`
```razor
@using Woffu.Tools.RepoOrchestra.Models

<MudTr>
    <MudTd>
        <MudCheckBox T="bool"
                     Value="Selected"
                     ValueChanged="v => SelectedChanged.InvokeAsync(v)"
                     Color="Color.Primary" />
    </MudTd>
    <MudTd>
        <MudText Typo="Typo.body2" Style="font-weight: @(Repo.IsDirty ? "600" : "400")">
            @Repo.Name
        </MudText>
    </MudTd>
    <MudTd>
        @if (Repo.IsGitRepo)
        {
            <MudChip T="string"
                     Size="Size.Small"
                     Color="@BranchColor"
                     Variant="Variant.Outlined">
                @Repo.Branch
            </MudChip>
        }
        else
        {
            <MudChip T="string" Size="Size.Small" Color="Color.Default">No git</MudChip>
        }
    </MudTd>
    <MudTd>
        @if (Repo.ScanError is not null)
        {
            <MudTooltip Text="@Repo.ScanError">
                <MudChip T="string" Size="Size.Small" Color="Color.Error">Error</MudChip>
            </MudTooltip>
        }
        else if (Repo.IsDirty)
        {
            <MudChip T="string" Size="Size.Small" Color="Color.Warning">
                ⚠ @Repo.DirtyFileCount dirty
            </MudChip>
        }
        else
        {
            <MudChip T="string" Size="Size.Small" Color="Color.Success">✓ clean</MudChip>
        }
    </MudTd>
    <MudTd>
        <MudStack Row="true" Spacing="1">
            @if (Repo.AheadCount > 0)
            {
                <MudText Typo="Typo.caption" Color="Color.Success">↑@Repo.AheadCount</MudText>
            }
            @if (Repo.BehindCount > 0)
            {
                <MudText Typo="Typo.caption" Color="Color.Error">↓@Repo.BehindCount</MudText>
            }
            @if (Repo.AheadCount == 0 && Repo.BehindCount == 0)
            {
                <MudText Typo="Typo.caption" Color="Color.Default">—</MudText>
            }
        </MudStack>
    </MudTd>
    <MudTd>
        <MudText Typo="Typo.caption" Color="Color.Default" Style="max-width:200px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">
            @Repo.LastCommitMessage
        </MudText>
    </MudTd>
    <MudTd>
        <MudStack Row="true" Spacing="1">
            <MudIconButton Icon="@Icons.Material.Filled.Terminal"
                           Size="Size.Small"
                           Title="Abrir en terminal"
                           OnClick="OpenInTerminal" />
        </MudStack>
    </MudTd>
</MudTr>

@code {
    [Parameter, EditorRequired] public RepoInfo Repo { get; set; } = default!;
    [Parameter] public bool Selected { get; set; }
    [Parameter] public EventCallback<bool> SelectedChanged { get; set; }

    private Color BranchColor => Repo.Branch is "master" or "main"
        ? Color.Success
        : Color.Warning;

    private void OpenInTerminal()
    {
        // Abre Windows Terminal en el directorio del repo
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "wt",
            Arguments = $"-d \"{Repo.Path}\"",
            UseShellExecute = true
        });
    }
}
```

- [ ] **Step 2: Verificar compilación**

```bash
cd C:/woffu-orchestra
dotnet build Woffu.Tools.RepoOrchestra/
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git -C C:/woffu-orchestra add Woffu.Tools.RepoOrchestra/Components/RepoRow.razor
git -C C:/woffu-orchestra commit -m "feat: add RepoRow component with status badges and terminal action"
```

---

## Task 10: Componente BulkActions

**Files:**
- Create: `Woffu.Tools.RepoOrchestra/Components/BulkActions.razor`

- [ ] **Step 1: Crear BulkActions.razor**

`Woffu.Tools.RepoOrchestra/Components/BulkActions.razor`
```razor
@using Woffu.Tools.RepoOrchestra.Models
@using Woffu.Tools.RepoOrchestra.Services
@inject GitCliService GitService
@inject ISnackbar Snackbar

<MudPaper Class="pa-2 mb-2" Elevation="0" Style="background: var(--mud-palette-surface)">
    <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">

        <MudCheckBox T="bool"
                     Value="AllSelected"
                     ValueChanged="OnSelectAllChanged"
                     Color="Color.Primary"
                     Label="Seleccionar todos" />

        <MudSpacer />

        <MudText Typo="Typo.caption" Color="Color.Primary">
            @SelectedRepos.Count seleccionados
        </MudText>

        <MudButton Variant="Variant.Outlined"
                   Color="Color.Success"
                   Size="Size.Small"
                   StartIcon="@Icons.Material.Filled.Download"
                   Disabled="@(!SelectedRepos.Any() || _busy)"
                   OnClick="PullSelected">
            Pull
        </MudButton>

        <MudButton Variant="Variant.Outlined"
                   Color="Color.Primary"
                   Size="Size.Small"
                   StartIcon="@Icons.Material.Filled.AccountTree"
                   Disabled="@(!SelectedRepos.Any() || _busy)"
                   OnClick="ShowCheckoutDialog">
            Checkout
        </MudButton>

    </MudStack>
</MudPaper>

@if (_checkoutDialogVisible)
{
    <MudPaper Class="pa-3 mb-2" Elevation="2">
        <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
            <MudTextField @bind-Value="_checkoutBranch"
                          Label="Nombre de rama"
                          Variant="Variant.Outlined"
                          Margin="Margin.Dense"
                          Style="width:250px" />
            <MudButton Color="Color.Primary" Variant="Variant.Filled"
                       Size="Size.Small" OnClick="CheckoutSelected">
                Aplicar
            </MudButton>
            <MudButton Color="Color.Default" Variant="Variant.Text"
                       Size="Size.Small" OnClick="() => _checkoutDialogVisible = false">
                Cancelar
            </MudButton>
        </MudStack>
    </MudPaper>
}

@code {
    [Parameter, EditorRequired] public IReadOnlyList<RepoInfo> SelectedRepos { get; set; } = [];
    [Parameter] public IReadOnlyList<RepoInfo> AllRepos { get; set; } = [];
    [Parameter] public EventCallback<bool> OnSelectAllChanged { get; set; }
    [Parameter] public EventCallback OnOperationCompleted { get; set; }

    private bool AllSelected => AllRepos.Any() && SelectedRepos.Count == AllRepos.Count;
    private bool _busy;
    private bool _checkoutDialogVisible;
    private string _checkoutBranch = string.Empty;

    private async Task PullSelected()
    {
        _busy = true;
        int ok = 0, failed = 0;
        foreach (var repo in SelectedRepos)
        {
            var (success, error) = await GitService.PullAsync(repo.Path);
            if (success) ok++; else { failed++; Snackbar.Add($"{repo.Name}: {error}", Severity.Error); }
        }
        Snackbar.Add($"Pull: {ok} OK, {failed} errores", failed > 0 ? Severity.Warning : Severity.Success);
        _busy = false;
        await OnOperationCompleted.InvokeAsync();
    }

    private void ShowCheckoutDialog() => _checkoutDialogVisible = true;

    private async Task CheckoutSelected()
    {
        if (string.IsNullOrWhiteSpace(_checkoutBranch)) return;
        _busy = true;
        int ok = 0, failed = 0;
        foreach (var repo in SelectedRepos)
        {
            var (success, error) = await GitService.CheckoutAsync(repo.Path, _checkoutBranch);
            if (success) ok++; else { failed++; Snackbar.Add($"{repo.Name}: {error}", Severity.Error); }
        }
        Snackbar.Add($"Checkout: {ok} OK, {failed} errores", failed > 0 ? Severity.Warning : Severity.Success);
        _checkoutDialogVisible = false;
        _checkoutBranch = string.Empty;
        _busy = false;
        await OnOperationCompleted.InvokeAsync();
    }
}
```

- [ ] **Step 2: Verificar compilación**

```bash
cd C:/woffu-orchestra
dotnet build Woffu.Tools.RepoOrchestra/
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git -C C:/woffu-orchestra add Woffu.Tools.RepoOrchestra/Components/BulkActions.razor
git -C C:/woffu-orchestra commit -m "feat: add BulkActions component with pull and checkout operations"
```

---

## Task 11: Componente RepoList

**Files:**
- Create: `Woffu.Tools.RepoOrchestra/Components/RepoList.razor`

- [ ] **Step 1: Crear RepoList.razor**

`Woffu.Tools.RepoOrchestra/Components/RepoList.razor`
```razor
@using Woffu.Tools.RepoOrchestra.Models

<div>
    @foreach (var group in Groups)
    {
        var isCollapsed = _collapsedGroups.Contains(group.Name);
        var autoCollapse = group.AutoCollapse;

        <MudPaper Class="mb-1" Elevation="0" Style="background: var(--mud-palette-surface)">
            <!-- Group header -->
            <MudStack Row="true" AlignItems="AlignItems.Center" Class="pa-2 cursor-pointer"
                      Style="background: #13131d; border-radius: 4px 4px 0 0"
                      onclick="@(() => ToggleGroup(group.Name))">
                <MudIcon Icon="@(isCollapsed ? Icons.Material.Filled.ChevronRight : Icons.Material.Filled.ExpandMore)"
                         Size="Size.Small" Color="Color.Default" />
                <MudText Typo="Typo.caption" Style="text-transform:uppercase;letter-spacing:1px;color:#666">
                    @group.Name
                </MudText>
                <MudText Typo="Typo.caption" Color="Color.Default">
                    (@group.Repos.Count(r => SelectedPaths.Contains(r.Path))/@group.Repos.Count)
                </MudText>
                @if (group.AllClean && group.AllOnMaster)
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Success" Variant="Variant.Text">
                        todos clean · master
                    </MudChip>
                }
            </MudStack>

            <!-- Group rows -->
            @if (!isCollapsed)
            {
                <MudTable Items="group.Repos" Hover="true" Dense="true" Elevation="0"
                          Style="background: transparent">
                    <HeaderContent>
                        <MudTh Style="width:40px"></MudTh>
                        <MudTh>Repositorio</MudTh>
                        <MudTh>Rama</MudTh>
                        <MudTh>Estado</MudTh>
                        <MudTh>Commits</MudTh>
                        <MudTh>Último commit</MudTh>
                        <MudTh Style="width:60px"></MudTh>
                    </HeaderContent>
                    <RowTemplate>
                        <RepoRow Repo="context"
                                 Selected="SelectedPaths.Contains(context.Path)"
                                 SelectedChanged="v => OnRepoSelectionChanged(context, v)" />
                    </RowTemplate>
                </MudTable>
            }
        </MudPaper>
    }
</div>

@code {
    [Parameter, EditorRequired] public IReadOnlyList<RepoGroup> Groups { get; set; } = [];
    [Parameter] public HashSet<string> SelectedPaths { get; set; } = [];
    [Parameter] public EventCallback<HashSet<string>> SelectedPathsChanged { get; set; }

    private readonly HashSet<string> _collapsedGroups = [];

    protected override void OnParametersSet()
    {
        // Auto-colapsar grupos que están todos clean + master
        foreach (var group in Groups.Where(g => g.AutoCollapse))
            _collapsedGroups.Add(group.Name);
    }

    private void ToggleGroup(string groupName)
    {
        if (!_collapsedGroups.Remove(groupName))
            _collapsedGroups.Add(groupName);
    }

    private async Task OnRepoSelectionChanged(RepoInfo repo, bool selected)
    {
        if (selected) SelectedPaths.Add(repo.Path);
        else SelectedPaths.Remove(repo.Path);
        await SelectedPathsChanged.InvokeAsync(SelectedPaths);
    }
}
```

- [ ] **Step 2: Verificar compilación**

```bash
cd C:/woffu-orchestra
dotnet build Woffu.Tools.RepoOrchestra/
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git -C C:/woffu-orchestra add Woffu.Tools.RepoOrchestra/Components/RepoList.razor
git -C C:/woffu-orchestra commit -m "feat: add RepoList component with collapsible groups and selection"
```

---

## Task 12: Home page — todo unido

**Files:**
- Modify: `Woffu.Tools.RepoOrchestra/Pages/Home.razor` (o `Index.razor`)

- [ ] **Step 1: Actualizar Home.razor**

`Woffu.Tools.RepoOrchestra/Pages/Home.razor`
```razor
@page "/"
@using Woffu.Tools.RepoOrchestra.Components
@using Woffu.Tools.RepoOrchestra.Models
@using Woffu.Tools.RepoOrchestra.Services
@implements IDisposable
@inject RepoStateStore Store
@inject RepoScannerService Scanner

<PageTitle>Repo Orchestra</PageTitle>

<!-- Top bar -->
<MudPaper Class="pa-3 mb-2" Elevation="1" Style="background: var(--mud-palette-surface)">
    <MudStack Row="true" AlignItems="AlignItems.Center">
        <MudText Typo="Typo.h6" Color="Color.Primary">🗂 Repo Orchestra</MudText>
        <MudText Typo="Typo.caption" Color="Color.Default" Class="ml-2">
            @_filteredGroups.Sum(g => g.Repos.Count) repos · C:\woffu-orchestra
        </MudText>
        <MudSpacer />
        @if (Store.IsScanning)
        {
            <MudProgressCircular Size="Size.Small" Indeterminate="true" Color="Color.Primary" Class="mr-2" />
        }
        else
        {
            <MudText Typo="Typo.caption" Color="Color.Default">
                Último scan: @(Store.LastScanCompleted == default ? "—" : Store.LastScanCompleted.ToString("HH:mm:ss"))
            </MudText>
        }
        <MudIconButton Icon="@Icons.Material.Filled.Refresh"
                       Color="Color.Primary"
                       Title="Refresh"
                       OnClick="ManualRefresh"
                       Disabled="Store.IsScanning" />
    </MudStack>
</MudPaper>

<!-- Filters -->
<FilterBar Groups="_allGroups" OnFilterChanged="ApplyFilter" />

<!-- Bulk actions -->
<BulkActions SelectedRepos="_selectedRepos"
             AllRepos="_filteredRepos"
             OnSelectAllChanged="OnSelectAllChanged"
             OnOperationCompleted="ManualRefresh" />

<!-- Repo list -->
<RepoList Groups="_filteredGroups"
          SelectedPaths="_selectedPaths"
          SelectedPathsChanged="OnSelectedPathsChanged" />

<!-- Status bar -->
<MudPaper Class="pa-2 mt-2" Elevation="0" Style="background: #0a0a0f">
    <MudStack Row="true" Spacing="3">
        <MudText Typo="Typo.caption" Color="Color.Primary">@_selectedPaths.Count seleccionados</MudText>
        <MudText Typo="Typo.caption">⚠ @Store.Repos.Count(r => r.IsDirty) dirty</MudText>
        <MudText Typo="Typo.caption">↓ @Store.Repos.Count(r => r.BehindCount > 0) behind</MudText>
        <MudSpacer />
        @if (!Store.IsScanning)
        {
            <MudText Typo="Typo.caption" Color="Color.Default">
                Auto-refresh en @_secondsUntilNextScan s
            </MudText>
        }
    </MudStack>
</MudPaper>

@code {
    private FilterCriteria _filter = new(string.Empty, string.Empty, string.Empty);
    private HashSet<string> _selectedPaths = [];
    private IReadOnlyList<RepoGroup> _filteredGroups = [];
    private IReadOnlyList<RepoInfo> _filteredRepos = [];
    private IReadOnlyList<RepoInfo> _selectedRepos = [];
    private IReadOnlyList<string> _allGroups = [];
    private int _secondsUntilNextScan = 60;
    private System.Timers.Timer? _countdownTimer;

    protected override void OnInitialized()
    {
        Store.OnStateChanged += OnStateChanged;
        RefreshView();
        StartCountdown();
    }

    private void OnStateChanged()
    {
        RefreshView();
        InvokeAsync(StateHasChanged);
        if (!Store.IsScanning) _secondsUntilNextScan = 60;
    }

    private void RefreshView()
    {
        var filtered = ApplyCriteria(Store.Repos, _filter);
        _allGroups = Store.Repos.Select(r => r.Group).Distinct().OrderBy(g => g).ToList();
        _filteredRepos = filtered;
        _filteredGroups = filtered
            .GroupBy(r => r.Group)
            .OrderBy(g => g.Key)
            .Select(g => new RepoGroup(g.Key, g.ToList()))
            .ToList();
        _selectedRepos = _selectedPaths.Count > 0
            ? filtered.Where(r => _selectedPaths.Contains(r.Path)).ToList()
            : [];
    }

    private void ApplyFilter(FilterCriteria criteria)
    {
        _filter = criteria;
        RefreshView();
    }

    private static IReadOnlyList<RepoInfo> ApplyCriteria(
        IReadOnlyList<RepoInfo> repos, FilterCriteria f)
    {
        return repos
            .Where(r => string.IsNullOrEmpty(f.Search) ||
                        r.Name.Contains(f.Search, StringComparison.OrdinalIgnoreCase))
            .Where(r => string.IsNullOrEmpty(f.Group) || r.Group == f.Group)
            .Where(r => f.Status switch
            {
                "dirty" => r.IsDirty,
                "clean" => !r.IsDirty,
                "behind" => r.BehindCount > 0,
                "feature" => r.Branch is not ("master" or "main") && !string.IsNullOrEmpty(r.Branch),
                _ => true
            })
            .ToList();
    }

    private Task ManualRefresh()
    {
        Scanner.RequestManualRefresh();
        return Task.CompletedTask;
    }

    private void OnSelectAllChanged(bool selectAll)
    {
        _selectedPaths = selectAll
            ? _filteredRepos.Select(r => r.Path).ToHashSet()
            : [];
        RefreshView();
    }

    private void OnSelectedPathsChanged(HashSet<string> paths)
    {
        _selectedPaths = paths;
        RefreshView();
    }

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

    public void Dispose()
    {
        Store.OnStateChanged -= OnStateChanged;
        _countdownTimer?.Dispose();
    }
}
```

- [ ] **Step 2: Verificar compilación**

```bash
cd C:/woffu-orchestra
dotnet build Woffu.Tools.RepoOrchestra/
```
Expected: `Build succeeded.`

- [ ] **Step 3: Ejecutar todos los tests**

```bash
cd C:/woffu-orchestra
dotnet test Woffu.Tools.RepoOrchestra.U.Tests/
```
Expected: todos `Passed!`

- [ ] **Step 4: Smoke test — arrancar la app**

```bash
cd C:/woffu-orchestra/Woffu.Tools.RepoOrchestra
dotnet run
```
Abrir `http://localhost:5200`. Verificar:
- La lista de repos aparece
- Los grupos se muestran colapsados/expandidos
- El botón Refresh funciona
- Los filtros filtran la lista

Parar con `Ctrl+C`.

- [ ] **Step 5: Commit final**

```bash
git -C C:/woffu-orchestra add Woffu.Tools.RepoOrchestra/Pages/
git -C C:/woffu-orchestra commit -m "feat: complete Repo Orchestra MVP - Home page wires all components"
```

---

## Self-Review

**Spec coverage:**
- ✅ Auto-descubrimiento de repos → `RepoScannerService.DiscoverRepos()`
- ✅ Lista agrupada → `RepoList.razor` + `DetermineGroup()`
- ✅ Estado por repo (branch, dirty, ahead/behind, último commit) → `GitCliService.ScanRepoAsync()`
- ✅ Auto-scan 60s + Refresh manual → `BackgroundService` + `RequestManualRefresh()`
- ✅ Filtros (nombre, grupo, estado) → `FilterBar.razor` + `ApplyCriteria()`
- ✅ Bulk Pull → `BulkActions.PullSelected()`
- ✅ Bulk Checkout → `BulkActions.CheckoutSelected()`
- ✅ Grupos colapsables (auto si todos clean+master) → `RepoGroup.AutoCollapse` + `RepoList`
- ✅ Barra de estado → status bar en `Home.razor`
- ✅ appsettings.json configurable → `RepoOrchestraOptions`

**Placeholder scan:** ninguno. Todo el código está completo.

**Type consistency:**
- `FilterCriteria` definido en `FilterBar.razor` y usado en `Home.razor` ✅
- `RepoGroup` definido en `Models/RepoGroup.cs`, usado en `RepoList.razor` y `Home.razor` ✅
- `RepoStateStore.SetScanning()` definido en Task 4, usado en `RepoScannerService` Task 5 ✅
- `GitCliService.ScanRepoAsync()` definido en Task 3, usado en `RepoScannerService` Task 5 ✅
- `RepoScannerService.RequestManualRefresh()` definido en Task 5, usado en `Home.razor` Task 12 ✅
