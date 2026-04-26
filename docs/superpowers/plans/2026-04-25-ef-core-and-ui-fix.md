# EF Core Migration + UI Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace raw ADO.NET `RepoCatalogService` with an EF Core `ApplicationDbContext`-based implementation, and fix the oversized textbox UI on the Home and Settings pages.

**Architecture:** `IRepoCatalogService` interface already exists. The new `EfRepoCatalogService` implements it using `IDbContextFactory<ApplicationDbContext>` (required because the service is singleton — never inject `DbContext` directly into a singleton). `ApplicationDbContext` maps the existing `RepoCatalogEntry` model. No migrations for now — `EnsureCreated()` on startup is sufficient for a personal tool.

**Tech Stack:** .NET 10, EF Core 9 (latest stable compatible with .NET 10), SQLite (default) or SQL Server (via config), MudBlazor 9.3

---

## File Map

| File | Change |
|------|--------|
| `src/DevHub/DevHub.csproj` | Replace `Microsoft.Data.Sqlite` + `Microsoft.Data.SqlClient` with EF Core packages |
| `src/DevHub/Data/ApplicationDbContext.cs` | **New** — EF Core DbContext |
| `src/DevHub/Models/RepoCatalogEntry.cs` | Change from `record` to `class` for EF compatibility |
| `src/DevHub/Services/EfRepoCatalogService.cs` | **New** — EF Core implementation of `IRepoCatalogService` |
| `src/DevHub/Services/RepoCatalogService.cs` | **Delete** — raw ADO.NET implementation |
| `src/DevHub/Program.cs` | Register `AddDbContextFactory` + `EfRepoCatalogService` |
| `src/DevHub/appsettings.json` | Remove `RepoCatalog:Provider` section; keep `ConnectionStrings` |
| `src/DevHub/Components/Pages/Home.razor` | Fix oversized textbox width |
| `src/DevHub/Components/Pages/Settings.razor` | Fix oversized textbox width |
| `tests/DevHub.U.Tests/DevHub.U.Tests.csproj` | Replace `Microsoft.Data.Sqlite` + `Microsoft.Data.SqlClient` with EF Core packages |
| `tests/DevHub.U.Tests/Helpers/TestDbContextFactory.cs` | **New** — test helper for `IDbContextFactory<ApplicationDbContext>` |
| `tests/DevHub.U.Tests/Services/When_RepoCatalogService_is_used/Then_add_persists_repo_path.cs` | Update to use `EfRepoCatalogService` |
| `tests/DevHub.U.Tests/Services/When_RepoCatalogService_is_used/Then_add_normalizes_relative_and_quoted_paths.cs` | Update to use `EfRepoCatalogService` |
| `tests/DevHub.U.Tests/Services/When_RepoCatalogService_is_used/Then_import_from_root_adds_git_repos_only.cs` | Update to use `EfRepoCatalogService` |
| `tests/DevHub.U.Tests/Services/When_RepoCatalogService_is_used/Then_remove_deletes_persisted_repo_path.cs` | Update to use `EfRepoCatalogService` |

---

## Task 1: Fix oversized textbox UI

**Files:**
- Modify: `src/DevHub/Components/Pages/Home.razor`
- Modify: `src/DevHub/Components/Pages/Settings.razor`

The `MudTextField` in both pages uses `flex: 1 1 340px` which makes it grow to fill the entire row width, resulting in a very large input area. Replace with a fixed max-width.

- [ ] **Step 1: Fix textbox width in Home.razor**

In `src/DevHub/Components/Pages/Home.razor`, find the `MudTextField` block (lines 43–50) and replace the `Style` attribute:

**Before:**
```razor
<MudTextField T="string"
              Value="_repoPathInput"
              ValueChanged="value => _repoPathInput = value"
              Label="Agregar repo por ruta"
              Placeholder="D:\repos\MyRepo"
              Variant="Variant.Outlined"
              Margin="Margin.Dense"
              Style="min-width: 340px; flex: 1 1 340px;" />
```

**After:**
```razor
<MudTextField T="string"
              Value="_repoPathInput"
              ValueChanged="value => _repoPathInput = value"
              Label="Agregar repo por ruta"
              Placeholder="D:\repos\MyRepo"
              Variant="Variant.Outlined"
              Margin="Margin.Dense"
              Style="width: 480px; max-width: 100%;" />
```

- [ ] **Step 2: Fix textbox width in Settings.razor**

In `src/DevHub/Components/Pages/Settings.razor`, find the `MudTextField` (lines 26–33) and replace the `Style` attribute:

**Before:**
```razor
<MudTextField T="string"
              Value="_repoPathInput"
              ValueChanged="value => _repoPathInput = value"
              Label="Repo path"
              Placeholder="D:\\repos\\MyRepo"
              Variant="Variant.Outlined"
              Margin="Margin.Dense"
              Style="min-width: 320px; flex: 1 1 320px;" />
```

**After:**
```razor
<MudTextField T="string"
              Value="_repoPathInput"
              ValueChanged="value => _repoPathInput = value"
              Label="Repo path"
              Placeholder="D:\\repos\\MyRepo"
              Variant="Variant.Outlined"
              Margin="Margin.Dense"
              Style="width: 480px; max-width: 100%;" />
```

- [ ] **Step 3: Build and verify**

```bash
dotnet build DevHub.slnx
```

Expected: 0 errors. Hot reload should show the fix in the browser.

- [ ] **Step 4: Commit**

```bash
git add src/DevHub/Components/Pages/Home.razor src/DevHub/Components/Pages/Settings.razor
git commit -m "fix: reduce textbox width on Home and Settings pages"
```

---

## Task 2: Update NuGet packages

**Files:**
- Modify: `src/DevHub/DevHub.csproj`
- Modify: `tests/DevHub.U.Tests/DevHub.U.Tests.csproj`

- [ ] **Step 1: Update main project packages**

Replace the full `<ItemGroup>` with packages in `src/DevHub/DevHub.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>
    <Version>1.0.0</Version>
    <AssemblyVersion>1.0.0</AssemblyVersion>
  </PropertyGroup>

  <ItemGroup>
    <Content Remove="wwwroot\maps\servicebus-map.html" />
    <None Include="wwwroot\maps\servicebus-map.html" Condition="Exists('wwwroot\maps\servicebus-map.html')" CopyToOutputDirectory="Never" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="BlazorMonaco" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
    <PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" />
    <PackageReference Include="MudBlazor" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Update test project packages**

Replace the packages `<ItemGroup>` in `tests/DevHub.U.Tests/DevHub.U.Tests.csproj`:

```xml
  <ItemGroup>
    <PackageReference Include="coverlet.collector">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Moq" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
```

Note: `Microsoft.Data.SqlClient` and `Microsoft.Data.Sqlite` are removed. EF Core packages include them transitively.

- [ ] **Step 3: Restore packages**

```bash
dotnet restore DevHub.slnx
```

Expected: restore succeeds, no errors.

---

## Task 3: Create ApplicationDbContext

**Files:**
- Create: `src/DevHub/Data/ApplicationDbContext.cs`
- Modify: `src/DevHub/Models/RepoCatalogEntry.cs`

- [ ] **Step 1: Update RepoCatalogEntry to be EF-compatible**

EF Core requires either a parameterless constructor or primary constructor support. Change `src/DevHub/Models/RepoCatalogEntry.cs` from a positional record to a class:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHub.Models;

[Table("repo_catalog")]
public class RepoCatalogEntry
{
    [Key]
    [MaxLength(1024)]
    public string Path { get; set; } = string.Empty;

    [Column("added_utc")]
    public DateTime AddedUtc { get; set; }

    public RepoCatalogEntry() { }

    public RepoCatalogEntry(string path, DateTime addedUtc)
    {
        Path = path;
        AddedUtc = addedUtc;
    }
}
```

- [ ] **Step 2: Create ApplicationDbContext**

Create `src/DevHub/Data/ApplicationDbContext.cs`:

```csharp
using DevHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHub.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<RepoCatalogEntry> RepoCatalog => Set<RepoCatalogEntry>();
}
```

No fluent API needed — `[Table]`, `[Key]`, `[MaxLength]`, and `[Column]` data annotations on `RepoCatalogEntry` handle all mapping.

---

## Task 4: Create EfRepoCatalogService

**Files:**
- Create: `src/DevHub/Services/EfRepoCatalogService.cs`

- [ ] **Step 1: Create the EF Core service**

Create `src/DevHub/Services/EfRepoCatalogService.cs`:

```csharp
using DevHub.Data;
using DevHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHub.Services;

public class EfRepoCatalogService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ILogger<EfRepoCatalogService> logger) : IRepoCatalogService
{
    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
    }

    public async Task<IReadOnlyList<RepoCatalogEntry>> GetReposAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.RepoCatalog
            .AsNoTracking()
            .OrderBy(r => r.Path)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetRepoPathsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.RepoCatalog
            .AsNoTracking()
            .OrderBy(r => r.Path)
            .Select(r => r.Path)
            .ToListAsync(ct);
    }

    public async Task AddAsync(string repoPath, CancellationToken ct = default)
    {
        var normalizedPath = NormalizePath(repoPath);

        if (!Directory.Exists(normalizedPath))
            throw new DirectoryNotFoundException($"Path does not exist: {normalizedPath}");
        if (!Directory.Exists(Path.Combine(normalizedPath, ".git")))
            throw new InvalidOperationException($"Not a git repository: {normalizedPath}");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var exists = await db.RepoCatalog.AnyAsync(r => r.Path == normalizedPath, ct);
        if (!exists)
        {
            db.RepoCatalog.Add(new RepoCatalogEntry(normalizedPath, DateTime.UtcNow));
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Added repo: {Path}", normalizedPath);
        }
    }

    public async Task RemoveAsync(string repoPath, CancellationToken ct = default)
    {
        var normalizedPath = NormalizePath(repoPath);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.RepoCatalog
            .Where(r => r.Path == normalizedPath)
            .ExecuteDeleteAsync(ct);
        logger.LogInformation("Removed repo: {Path}", normalizedPath);
    }

    public async Task<int> ImportFromRootAsync(string rootPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return 0;

        var candidates = Directory.GetDirectories(rootPath)
            .Where(p => Directory.Exists(Path.Combine(p, ".git")))
            .Select(p => Path.GetFullPath(p))
            .OrderBy(p => p)
            .ToList();

        if (candidates.Count == 0) return 0;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.RepoCatalog.Select(r => r.Path).ToHashSetAsync(ct);

        var toAdd = candidates.Where(p => !existing.Contains(p)).ToList();
        foreach (var path in toAdd)
            db.RepoCatalog.Add(new RepoCatalogEntry(path, DateTime.UtcNow));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Imported {Count} repos from {Root}", toAdd.Count, rootPath);
        return toAdd.Count;
    }

    public string GetDisplayConnectionString() => "EF Core — see ConnectionStrings:DefaultConnection in appsettings.json";

    private static string NormalizePath(string repoPath) =>
        Path.GetFullPath(repoPath.Trim().Trim('"'));
}
```

---

## Task 5: Update Program.cs and appsettings.json

**Files:**
- Modify: `src/DevHub/Program.cs`
- Modify: `src/DevHub/appsettings.json`
- Delete: `src/DevHub/Services/RepoCatalogService.cs`

- [ ] **Step 1: Delete the old ADO.NET service**

Delete the file `src/DevHub/Services/RepoCatalogService.cs`.

```bash
git rm src/DevHub/Services/RepoCatalogService.cs
```

- [ ] **Step 2: Update Program.cs**

Replace the full contents of `src/DevHub/Program.cs`:

```csharp
using DevHub.Components;
using DevHub.Data;
using DevHub.Services;
using DevHub.Services.SecretProfiles;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.Configure<DevHubOptions>(
    builder.Configuration.GetSection("DevHub"));
builder.Services.Configure<ServiceBusMapOptions>(
    builder.Configuration.GetSection("ServiceBusMap"));

// EF Core — use IDbContextFactory so singleton services can create/dispose DbContext per operation
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "devhub.db")}";

var dbProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";

if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}
else
{
    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}

builder.Services.AddSingleton<VersionService>();
builder.Services.AddSingleton<GitCliService>();
builder.Services.AddSingleton<IRepoCatalogService, EfRepoCatalogService>();
builder.Services.AddSingleton<RepoStateStore>();
builder.Services.AddSingleton<RepoScannerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RepoScannerService>());
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton<ServiceBusMapService>();

builder.Services.Configure<SecretProfileOptions>(
    builder.Configuration.GetSection("SecretProfiles"));
builder.Services.AddSingleton<IFileSystem, FileSystem>();
builder.Services.AddSingleton<SecretProfileService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

- [ ] **Step 3: Update appsettings.json**

Replace the full contents of `src/DevHub/appsettings.json`. Remove `RepoCatalog:Provider`, add `DatabaseProvider`:

```json
{
  "DevHub": {
    "RootPath": "C:\\_O",
    "ScanIntervalSeconds": 60,
    "ParallelScanDegree": 8,
    "ExcludedRepos": [],
    "Groups": [
      { "Name": "Services",   "Color": "primary",   "Prefixes": [ "Woffu.Services." ] },
      { "Name": "Libraries",  "Color": "secondary",  "Prefixes": [ "Woffu.Library." ] },
      { "Name": "Functions",  "Color": "info",       "Prefixes": [ "Woffu.Functions." ] },
      { "Name": "Frontend",   "Color": "success",    "Prefixes": [ "Woffu.Frontend." ] },
      { "Name": "DevOps",     "Color": "warning",    "Prefixes": [ "Devops." ] },
      { "Name": "Tools",      "Color": "tertiary",   "Prefixes": [ "Woffu.Tools.", "Woffu.Utils." ] }
    ],
    "DefaultGroup": "Other"
  },
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "DefaultConnection": "Server=DESKTOP-SLPCU2M;Database=DevHub;User Id=sa;Password=hola123;TrustServerCertificate=True;Encrypt=False"
  },
  "ServiceBusMap": {
    "ScriptPath": "..\\..\\scripts\\generate-servicebus-map.ps1",
    "TemplateFile": "wwwroot\\maps\\servicebus-map.template.html",
    "OutputFile": "wwwroot\\maps\\servicebus-map.html",
    "ReposRoot": "C:\\_O"
  },
  "SecretProfiles": {
    "ProfilesRoot": "..\\..\\profiles",
    "Services": [
      {
        "Name": "Documents",
        "UserSecretsId": "Woffu.Services.Documents.API.Secrets",
        "ProdProfileNames": [ "prod" ]
      }
    ]
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

> To switch to SQLite: change `"DatabaseProvider": "Sqlite"` and set `"DefaultConnection": "Data Source=devhub.db"`.

- [ ] **Step 4: Build**

```bash
dotnet build DevHub.slnx
```

Expected: 0 errors. Fix any compilation errors from leftover references to `RepoCatalogService` (the old concrete class) before continuing.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: replace ADO.NET RepoCatalogService with EF Core ApplicationDbContext"
```

---

## Task 6: Update tests to use EfRepoCatalogService

**Files:**
- Create: `tests/DevHub.U.Tests/Helpers/TestDbContextFactory.cs`
- Modify: all 4 files in `tests/DevHub.U.Tests/Services/When_RepoCatalogService_is_used/`

The tests use an in-memory SQLite database (`:memory:`) via a kept-open `SqliteConnection`. This is the standard EF Core testing pattern — the connection must stay open for the lifetime of the test because SQLite in-memory databases exist only while the connection is open.

- [ ] **Step 1: Create TestDbContextFactory helper**

Create `tests/DevHub.U.Tests/Helpers/TestDbContextFactory.cs`:

```csharp
using DevHub.Data;
using Microsoft.EntityFrameworkCore;

namespace DevHub.U.Tests.Helpers;

internal sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
    : IDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext() => new(options);
}
```

- [ ] **Step 2: Rewrite Then_add_persists_repo_path.cs**

Replace full contents of `tests/DevHub.U.Tests/Services/When_RepoCatalogService_is_used/Then_add_persists_repo_path.cs`:

```csharp
using DevHub.Data;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_add_persists_repo_path
{
    [Fact]
    public async Task Execute()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new ApplicationDbContext(options))
            await db.Database.EnsureCreatedAsync();

        var factory = new TestDbContextFactory(options);
        var sut = new EfRepoCatalogService(factory, NullLogger<EfRepoCatalogService>.Instance);

        using var repo = new TempGitRepo();

        await sut.AddAsync(repo.Path);
        var repoPaths = await sut.GetRepoPathsAsync();

        repoPaths.ShouldContain(Path.GetFullPath(repo.Path));
    }
}
```

- [ ] **Step 3: Rewrite Then_add_normalizes_relative_and_quoted_paths.cs**

Replace full contents of `tests/DevHub.U.Tests/Services/When_RepoCatalogService_is_used/Then_add_normalizes_relative_and_quoted_paths.cs`:

```csharp
using DevHub.Data;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_add_normalizes_relative_and_quoted_paths
{
    [Fact]
    public async Task Execute()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new ApplicationDbContext(options))
            await db.Database.EnsureCreatedAsync();

        var factory = new TestDbContextFactory(options);
        var sut = new EfRepoCatalogService(factory, NullLogger<EfRepoCatalogService>.Instance);

        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using var repo = new TempGitRepoAt(Path.Combine(tempRoot, "RepoA"));

        var previousDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempRoot);
        try
        {
            await sut.AddAsync("\".\\RepoA\"");
            var repoPaths = await sut.GetRepoPathsAsync();

            repoPaths.Count.ShouldBe(1);
            repoPaths[0].ShouldBe(Path.GetFullPath(repo.Path));
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            TempGitRepo.ForceDeleteDirectory(tempRoot);
        }
    }
}
```

- [ ] **Step 4: Rewrite Then_import_from_root_adds_git_repos_only.cs**

Replace full contents of `tests/DevHub.U.Tests/Services/When_RepoCatalogService_is_used/Then_import_from_root_adds_git_repos_only.cs`:

```csharp
using DevHub.Data;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_import_from_root_adds_git_repos_only
{
    [Fact]
    public async Task Execute()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new ApplicationDbContext(options))
            await db.Database.EnsureCreatedAsync();

        var factory = new TestDbContextFactory(options);
        var sut = new EfRepoCatalogService(factory, NullLogger<EfRepoCatalogService>.Instance);

        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        using var repoA = new TempGitRepoAt(Path.Combine(tempRoot, "RepoA"));
        using var repoB = new TempGitRepoAt(Path.Combine(tempRoot, "RepoB"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "Notes"));

        try
        {
            var imported = await sut.ImportFromRootAsync(tempRoot);
            var repoPaths = await sut.GetRepoPathsAsync();

            imported.ShouldBe(2);
            repoPaths.Count.ShouldBe(2);
            repoPaths.ShouldContain(Path.GetFullPath(repoA.Path));
            repoPaths.ShouldContain(Path.GetFullPath(repoB.Path));
        }
        finally
        {
            TempGitRepo.ForceDeleteDirectory(tempRoot);
        }
    }
}
```

- [ ] **Step 5: Rewrite Then_remove_deletes_persisted_repo_path.cs**

Replace full contents of `tests/DevHub.U.Tests/Services/When_RepoCatalogService_is_used/Then_remove_deletes_persisted_repo_path.cs`:

```csharp
using DevHub.Data;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_remove_deletes_persisted_repo_path
{
    [Fact]
    public async Task Execute()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new ApplicationDbContext(options))
            await db.Database.EnsureCreatedAsync();

        var factory = new TestDbContextFactory(options);
        var sut = new EfRepoCatalogService(factory, NullLogger<EfRepoCatalogService>.Instance);

        using var repo = new TempGitRepo();

        await sut.AddAsync(repo.Path);
        await sut.RemoveAsync(repo.Path);
        var repoPaths = await sut.GetRepoPathsAsync();

        repoPaths.ShouldBeEmpty();
    }
}
```

- [ ] **Step 6: Run all tests**

```bash
dotnet test DevHub.slnx
```

Expected: all 27 tests pass. The 4 `When_RepoCatalogService_is_used` tests now use `EfRepoCatalogService` with in-memory SQLite — no file cleanup needed, no connection pool issues.

- [ ] **Step 7: Final commit**

```bash
git add -A
git commit -m "test: migrate When_RepoCatalogService_is_used tests to EfRepoCatalogService with in-memory SQLite"
```
