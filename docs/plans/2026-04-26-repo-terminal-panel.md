# Plan: Panel de comandos por repo (Terminal)

**Goal:** Al hacer clic en el icono Terminal de cada repo, se abre un drawer lateral que muestra comandos auto-detectados según el tipo de proyecto (Angular, React, .NET…), los scripts del `package.json` si existe, y comandos custom que el usuario puede guardar. Al hacer clic en un comando, se ejecuta y se streama el output en tiempo real dentro del panel.

**Architecture:** Un `ProjectTypeDetector` analiza los archivos del repo para determinar el tipo y devolver comandos predeterminados. Un `PackageJsonReader` parsea los scripts del `package.json`. Un `CustomCommandService` gestiona comandos personalizados por repo en DB. Un `RepoCommandsService` agrega las tres fuentes. Un `IProcessStreamer`/`ProcessStreamer` ejecuta el proceso y emite líneas via callback. El componente `RepoTerminalPanel` (MudDrawer) orquesta todo. `RepoRow` emite un evento que Home escucha para abrir el panel.

**Tech Stack:** .NET 10, Blazor Server InteractiveServer, MudBlazor 9.3.0, Entity Framework Core, xUnit + Moq

---

## Mapa de archivos

| Archivo | Acción |
|---|---|
| `src/DevHub/Models/ProjectCommand.cs` | Crear — record + enums |
| `src/DevHub/Models/CustomRepoCommand.cs` | Crear — entidad EF |
| `src/DevHub/Data/ApplicationDbContext.cs` | Modificar — añadir DbSet |
| `src/DevHub/Services/ProjectTypeDetector.cs` | Crear |
| `src/DevHub/Services/PackageJsonReader.cs` | Crear |
| `src/DevHub/Services/CustomCommandService.cs` | Crear |
| `src/DevHub/Services/RepoCommandsService.cs` | Crear |
| `src/DevHub/Services/IProcessStreamer.cs` | Crear |
| `src/DevHub/Services/ProcessStreamer.cs` | Crear |
| `src/DevHub/Components/RepoTerminalPanel.razor` | Crear |
| `src/DevHub/Components/RepoTerminalPanel.razor.cs` | Crear |
| `src/DevHub/Components/RepoRow.razor` | Modificar — nuevo EventCallback |
| `src/DevHub/Components/RepoRow.razor.cs` | Modificar — nuevo parámetro |
| `src/DevHub/Components/Pages/Home.razor` | Modificar — añadir panel + escuchar evento |
| `src/DevHub/Components/Pages/Home.razor.cs` | Modificar — método para abrir panel |
| `src/DevHub/Program.cs` | Modificar — registrar nuevos servicios |
| `tests/DevHub.U.Tests/Services/When_ProjectTypeDetector_detects/Then_angular_detected.cs` | Crear |
| `tests/DevHub.U.Tests/Services/When_ProjectTypeDetector_detects/Then_dotnet_detected.cs` | Crear |
| `tests/DevHub.U.Tests/Services/When_ProjectTypeDetector_detects/Then_react_detected.cs` | Crear |
| `tests/DevHub.U.Tests/Services/When_ProjectTypeDetector_detects/Then_unknown_when_no_markers.cs` | Crear |
| `tests/DevHub.U.Tests/Services/When_PackageJsonReader_reads/Then_scripts_returned.cs` | Crear |
| `tests/DevHub.U.Tests/Services/When_PackageJsonReader_reads/Then_empty_when_no_file.cs` | Crear |
| `tests/DevHub.U.Tests/Services/When_RepoCommandsService_aggregates/Then_all_sources_combined.cs` | Crear |

---

## Task 1: Models

**Archivo:** `src/DevHub/Models/ProjectCommand.cs`

- [ ] **Crear el archivo con este contenido exacto:**

```csharp
namespace DevHub.Models;

public enum ProjectType { Angular, React, Vue, DotNet, Node, Unknown }

public enum CommandSource { AutoDetected, PackageJson, Custom }

public record ProjectCommand(string Name, string Command, CommandSource Source);
```

**Archivo:** `src/DevHub/Models/CustomRepoCommand.cs`

- [ ] **Crear el archivo con este contenido exacto:**

```csharp
namespace DevHub.Models;

public class CustomRepoCommand
{
    public int Id { get; set; }
    public string RepoPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
}
```

- [ ] **Commit:**
```bash
git add src/DevHub/Models/ProjectCommand.cs src/DevHub/Models/CustomRepoCommand.cs
git commit -m "feat: add ProjectCommand and CustomRepoCommand models"
```

---

## Task 2: DB — añadir CustomRepoCommands

**Archivo:** `src/DevHub/Data/ApplicationDbContext.cs`

El archivo actual tiene dos DbSets: `RepoCatalogEntries` y `GroupRules`. Hay que añadir uno más y su configuración en `OnModelCreating`.

- [ ] **Añadir el using al principio del archivo (si no está):**
```csharp
using DevHub.Models;
```
Ya está presente — no añadir duplicado.

- [ ] **Añadir el DbSet después de `GroupRules`:**
```csharp
public DbSet<CustomRepoCommand> CustomRepoCommands => Set<CustomRepoCommand>();
```

- [ ] **Añadir la configuración de la entidad dentro de `OnModelCreating`, después del bloque de `GroupRule`:**
```csharp
modelBuilder.Entity<CustomRepoCommand>(e =>
{
    e.HasKey(c => c.Id);
    e.Property(c => c.RepoPath).HasMaxLength(1024).IsRequired();
    e.Property(c => c.Name).HasMaxLength(256).IsRequired();
    e.Property(c => c.Command).HasMaxLength(1024).IsRequired();
});
```

> **Nota:** El proyecto usa `EnsureCreatedAsync()` al arrancar (no migraciones), así que la tabla se crea automáticamente la próxima vez que se inicie la app. No hay que hacer nada más con la DB.

- [ ] **Commit:**
```bash
git add src/DevHub/Data/ApplicationDbContext.cs
git commit -m "feat: add CustomRepoCommands table to ApplicationDbContext"
```

---

## Task 3: ProjectTypeDetector

**Archivo:** `src/DevHub/Services/ProjectTypeDetector.cs`

- [ ] **Crear con este contenido:**

```csharp
using DevHub.Models;

namespace DevHub.Services;

public class ProjectTypeDetector
{
    public ProjectType Detect(string repoPath)
    {
        if (File.Exists(Path.Combine(repoPath, "angular.json")))
        {
            return ProjectType.Angular;
        }

        var packageJson = Path.Combine(repoPath, "package.json");
        if (File.Exists(packageJson))
        {
            var content = File.ReadAllText(packageJson);
            if (content.Contains("\"react\"") || content.Contains("\"react-dom\""))
            {
                return ProjectType.React;
            }

            if (content.Contains("\"vue\""))
            {
                return ProjectType.Vue;
            }

            return ProjectType.Node;
        }

        if (Directory.GetFiles(repoPath, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0 ||
            Directory.GetFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly).Length > 0)
        {
            return ProjectType.DotNet;
        }

        return ProjectType.Unknown;
    }

    public IReadOnlyList<ProjectCommand> GetDefaultCommands(ProjectType type) =>
        type switch
        {
            ProjectType.Angular => [
                new("Serve", "ng serve", CommandSource.AutoDetected),
                new("Build", "ng build", CommandSource.AutoDetected),
                new("Test", "ng test", CommandSource.AutoDetected)
            ],
            ProjectType.React => [
                new("Start", "npm start", CommandSource.AutoDetected),
                new("Build", "npm run build", CommandSource.AutoDetected),
                new("Test", "npm test", CommandSource.AutoDetected)
            ],
            ProjectType.Vue => [
                new("Dev", "npm run dev", CommandSource.AutoDetected),
                new("Build", "npm run build", CommandSource.AutoDetected),
                new("Test", "npm test", CommandSource.AutoDetected)
            ],
            ProjectType.DotNet => [
                new("Run", "dotnet run", CommandSource.AutoDetected),
                new("Build", "dotnet build", CommandSource.AutoDetected),
                new("Test", "dotnet test", CommandSource.AutoDetected)
            ],
            ProjectType.Node => [
                new("Start", "npm start", CommandSource.AutoDetected),
                new("Test", "npm test", CommandSource.AutoDetected)
            ],
            _ => []
        };
}
```

- [ ] **Crear los tests:**

**`tests/DevHub.U.Tests/Services/When_ProjectTypeDetector_detects/Then_angular_detected.cs`**
```csharp
using DevHub.Models;
using DevHub.Services;

namespace DevHub.U.Tests.Services.When_ProjectTypeDetector_detects;

public class Then_angular_detected
{
    [Fact]
    public async Task Execute()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "angular.json"), "{}");

        var sut = new ProjectTypeDetector();
        var result = sut.Detect(dir);

        Assert.Equal(ProjectType.Angular, result);
        Directory.Delete(dir, true);
    }
}
```

**`tests/DevHub.U.Tests/Services/When_ProjectTypeDetector_detects/Then_dotnet_detected.cs`**
```csharp
using DevHub.Models;
using DevHub.Services;

namespace DevHub.U.Tests.Services.When_ProjectTypeDetector_detects;

public class Then_dotnet_detected
{
    [Fact]
    public async Task Execute()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "MyApp.csproj"), "<Project />");

        var sut = new ProjectTypeDetector();
        var result = sut.Detect(dir);

        Assert.Equal(ProjectType.DotNet, result);
        Directory.Delete(dir, true);
    }
}
```

**`tests/DevHub.U.Tests/Services/When_ProjectTypeDetector_detects/Then_react_detected.cs`**
```csharp
using DevHub.Models;
using DevHub.Services;

namespace DevHub.U.Tests.Services.When_ProjectTypeDetector_detects;

public class Then_react_detected
{
    [Fact]
    public async Task Execute()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "package.json"),
            """{"dependencies":{"react":"^18.0.0"}}""");

        var sut = new ProjectTypeDetector();
        var result = sut.Detect(dir);

        Assert.Equal(ProjectType.React, result);
        Directory.Delete(dir, true);
    }
}
```

**`tests/DevHub.U.Tests/Services/When_ProjectTypeDetector_detects/Then_unknown_when_no_markers.cs`**
```csharp
using DevHub.Models;
using DevHub.Services;

namespace DevHub.U.Tests.Services.When_ProjectTypeDetector_detects;

public class Then_unknown_when_no_markers
{
    [Fact]
    public void Execute()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);

        var sut = new ProjectTypeDetector();
        var result = sut.Detect(dir);

        Assert.Equal(ProjectType.Unknown, result);
        Directory.Delete(dir, true);
    }
}
```

- [ ] **Correr los tests:**
```bash
dotnet test tests/DevHub.U.Tests/DevHub.U.Tests.csproj --no-restore -v q
```
Esperado: 4 tests nuevos en verde.

- [ ] **Commit:**
```bash
git add src/DevHub/Services/ProjectTypeDetector.cs tests/
git commit -m "feat: add ProjectTypeDetector with Angular/React/Vue/DotNet/Node detection"
```

---

## Task 4: PackageJsonReader

**Archivo:** `src/DevHub/Services/PackageJsonReader.cs`

- [ ] **Crear con este contenido:**

```csharp
using System.Text.Json;
using DevHub.Models;

namespace DevHub.Services;

public class PackageJsonReader
{
    public IReadOnlyList<ProjectCommand> GetScripts(string repoPath)
    {
        var packageJsonPath = Path.Combine(repoPath, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(packageJsonPath);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("scripts", out var scripts))
            {
                return [];
            }

            return scripts.EnumerateObject()
                .Select(p => new ProjectCommand(p.Name, $"npm run {p.Name}", CommandSource.PackageJson))
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
```

- [ ] **Crear los tests:**

**`tests/DevHub.U.Tests/Services/When_PackageJsonReader_reads/Then_scripts_returned.cs`**
```csharp
using DevHub.Models;
using DevHub.Services;

namespace DevHub.U.Tests.Services.When_PackageJsonReader_reads;

public class Then_scripts_returned
{
    [Fact]
    public async Task Execute()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "package.json"),
            """{"scripts":{"start":"node index.js","build":"webpack","test":"jest"}}""");

        var sut = new PackageJsonReader();
        var result = sut.GetScripts(dir);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, c => c.Name == "start" && c.Command == "npm run start");
        Assert.Contains(result, c => c.Name == "build" && c.Command == "npm run build");
        Assert.All(result, c => Assert.Equal(CommandSource.PackageJson, c.Source));
        Directory.Delete(dir, true);
    }
}
```

**`tests/DevHub.U.Tests/Services/When_PackageJsonReader_reads/Then_empty_when_no_file.cs`**
```csharp
using DevHub.Services;

namespace DevHub.U.Tests.Services.When_PackageJsonReader_reads;

public class Then_empty_when_no_file
{
    [Fact]
    public void Execute()
    {
        var sut = new PackageJsonReader();
        var result = sut.GetScripts(@"C:\ruta\que\no\existe");

        Assert.Empty(result);
    }
}
```

- [ ] **Correr los tests:**
```bash
dotnet test tests/DevHub.U.Tests/DevHub.U.Tests.csproj --no-restore -v q
```
Esperado: 2 tests nuevos en verde.

- [ ] **Commit:**
```bash
git add src/DevHub/Services/PackageJsonReader.cs tests/
git commit -m "feat: add PackageJsonReader to extract npm scripts"
```

---

## Task 5: CustomCommandService

**Archivo:** `src/DevHub/Services/CustomCommandService.cs`

- [ ] **Crear con este contenido:**

```csharp
using DevHub.Data;
using DevHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHub.Services;

public class CustomCommandService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task<IReadOnlyList<CustomRepoCommand>> GetByRepoAsync(string repoPath, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CustomRepoCommands
            .Where(c => c.RepoPath == repoPath)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(string repoPath, string name, string command, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.CustomRepoCommands.Add(new CustomRepoCommand
        {
            RepoPath = repoPath,
            Name = name,
            Command = command
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.CustomRepoCommands.FindAsync([id], ct);
        if (entity is not null)
        {
            db.CustomRepoCommands.Remove(entity);
            await db.SaveChangesAsync(ct);
        }
    }
}
```

> No hay tests de CustomCommandService en esta fase porque sigue el mismo patrón que `EfRepoCatalogService` que ya está testeado. Si minimax quiere añadirlos, puede copiar el patrón de `When_RepoCatalogService_is_used`.

- [ ] **Commit:**
```bash
git add src/DevHub/Services/CustomCommandService.cs
git commit -m "feat: add CustomCommandService for per-repo custom commands"
```

---

## Task 6: RepoCommandsService (agregador)

**Archivo:** `src/DevHub/Services/RepoCommandsService.cs`

- [ ] **Crear con este contenido:**

```csharp
using DevHub.Models;

namespace DevHub.Services;

public class RepoCommandsService(
    ProjectTypeDetector typeDetector,
    PackageJsonReader packageJsonReader,
    CustomCommandService customCommandService)
{
    public async Task<IReadOnlyList<ProjectCommand>> GetCommandsAsync(string repoPath, CancellationToken ct = default)
    {
        var type = typeDetector.Detect(repoPath);
        var defaults = typeDetector.GetDefaultCommands(type);
        var scripts = packageJsonReader.GetScripts(repoPath);
        var customs = await customCommandService.GetByRepoAsync(repoPath, ct);

        var customCommands = customs
            .Select(c => new ProjectCommand(c.Name, c.Command, CommandSource.Custom))
            .ToList();

        return [.. defaults, .. scripts, .. customCommands];
    }
}
```

- [ ] **Crear el test:**

**`tests/DevHub.U.Tests/Services/When_RepoCommandsService_aggregates/Then_all_sources_combined.cs`**

```csharp
using DevHub.Data;
using DevHub.Models;
using DevHub.Services;
using Microsoft.EntityFrameworkCore;

namespace DevHub.U.Tests.Services.When_RepoCommandsService_aggregates;

public class Then_all_sources_combined
{
    [Fact]
    public async Task Execute()
    {
        // Arrange — repo con angular.json + package.json con scripts
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "angular.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(dir, "package.json"),
            """{"scripts":{"e2e":"cypress run"}}""");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var factory = new TestDbContextFactory(options);

        var sut = new RepoCommandsService(
            new ProjectTypeDetector(),
            new PackageJsonReader(),
            new CustomCommandService(factory));

        // Act
        var commands = await sut.GetCommandsAsync(dir);

        // Assert — debe incluir defaults de Angular + el script e2e
        Assert.Contains(commands, c => c.Source == CommandSource.AutoDetected && c.Name == "Serve");
        Assert.Contains(commands, c => c.Source == CommandSource.PackageJson && c.Name == "e2e");

        Directory.Delete(dir, true);
    }
}
```

> `TestDbContextFactory` ya existe en `tests/DevHub.U.Tests/Helpers/TestDbContextFactory.cs`. Úsala tal cual.

- [ ] **Correr los tests:**
```bash
dotnet test tests/DevHub.U.Tests/DevHub.U.Tests.csproj --no-restore -v q
```
Esperado: 1 test nuevo en verde.

- [ ] **Commit:**
```bash
git add src/DevHub/Services/RepoCommandsService.cs tests/
git commit -m "feat: add RepoCommandsService aggregating auto/scripts/custom commands"
```

---

## Task 7: IProcessStreamer + ProcessStreamer

**Archivo:** `src/DevHub/Services/IProcessStreamer.cs`

- [ ] **Crear con este contenido:**

```csharp
namespace DevHub.Services;

public interface IProcessStreamer
{
    Task StreamAsync(
        string workingDirectory,
        string command,
        Func<string, Task> onLine,
        CancellationToken ct = default);
}
```

**Archivo:** `src/DevHub/Services/ProcessStreamer.cs`

- [ ] **Crear con este contenido:**

```csharp
using System.Diagnostics;

namespace DevHub.Services;

public class ProcessStreamer : IProcessStreamer
{
    public async Task StreamAsync(
        string workingDirectory,
        string command,
        Func<string, Task> onLine,
        CancellationToken ct = default)
    {
        var (executable, arguments) = ParseCommand(command);

        var psi = new ProcessStartInfo(executable, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = ReadStreamAsync(process.StandardOutput, onLine, ct);
        var stderrTask = ReadStreamAsync(process.StandardError, onLine, ct);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);
    }

    private static async Task ReadStreamAsync(
        StreamReader reader,
        Func<string, Task> onLine,
        CancellationToken ct)
    {
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is not null)
            {
                await onLine(line);
            }
        }
    }

    private static (string Executable, string Arguments) ParseCommand(string command)
    {
        if (OperatingSystem.IsWindows())
        {
            return ("cmd.exe", $"/c {command}");
        }

        return ("/bin/sh", $"-c \"{command}\"");
    }
}
```

> `ProcessStreamer` no tiene tests unitarios porque requiere un proceso real del SO. La integración se verifica al usar el panel en la app.

- [ ] **Commit:**
```bash
git add src/DevHub/Services/IProcessStreamer.cs src/DevHub/Services/ProcessStreamer.cs
git commit -m "feat: add IProcessStreamer and ProcessStreamer for real-time command output"
```

---

## Task 8: Registrar todos los servicios nuevos en Program.cs

**Archivo:** `src/DevHub/Program.cs`

- [ ] **Añadir las siguientes líneas después de `builder.Services.AddSingleton<FolderPickerService>();`:**

```csharp
builder.Services.AddSingleton<ProjectTypeDetector>();
builder.Services.AddSingleton<PackageJsonReader>();
builder.Services.AddSingleton<CustomCommandService>();
builder.Services.AddSingleton<RepoCommandsService>();
builder.Services.AddSingleton<IProcessStreamer, ProcessStreamer>();
```

- [ ] **Verificar que compila:**
```bash
dotnet build src/DevHub/DevHub.csproj --no-restore -v q 2>&1 | grep -E "^.*error (CS|IDE|RZ)"
```
Esperado: sin salida (0 errores).

- [ ] **Commit:**
```bash
git add src/DevHub/Program.cs
git commit -m "feat: register terminal panel services in DI"
```

---

## Task 9: RepoTerminalPanel — componente Blazor

**Archivo:** `src/DevHub/Components/RepoTerminalPanel.razor`

- [ ] **Crear con este contenido:**

```razor
@using DevHub.Models
@using DevHub.Services

<MudDrawer @bind-Open="_open"
           Anchor="Anchor.Right"
           Variant="DrawerVariant.Temporary"
           Width="560px"
           Elevation="4"
           CloseOnClick="false">

    <MudDrawerHeader>
        <MudStack Row="true" AlignItems="AlignItems.Center" Style="width:100%">
            <MudIcon Icon="@Icons.Material.Filled.Terminal" Color="Color.Primary" />
            <MudText Typo="Typo.h6" Class="ml-2" Style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap">
                @_repo?.Name
            </MudText>
            <MudSpacer />
            <MudIconButton Icon="@Icons.Material.Filled.Close"
                           Size="Size.Small"
                           OnClick="Close"
                           aria-label="Cerrar" />
        </MudStack>
    </MudDrawerHeader>

    <MudStack Spacing="2" Class="pa-3" Style="overflow-y:auto; height: calc(100vh - 64px)">

        @* ── Comandos disponibles ── *@
        @if (_loading)
        {
            <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
        }
        else if (_commands.Count == 0)
        {
            <MudAlert Severity="Severity.Info" Variant="Variant.Outlined" Dense="true">
                No se detectaron comandos para este repo.
            </MudAlert>
        }
        else
        {
            @foreach (var group in _commands.GroupBy(c => c.Source))
            {
                <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mt-1">
                    @SourceLabel(group.Key)
                </MudText>
                <MudStack Row="true" Wrap="Wrap.Wrap" Spacing="1">
                    @foreach (var cmd in group)
                    {
                        <MudTooltip Text="@cmd.Command">
                            <MudButton Variant="Variant.Outlined"
                                       Size="Size.Small"
                                       Color="Color.Primary"
                                       Disabled="@_running"
                                       OnClick="() => RunCommandAsync(cmd)">
                                @cmd.Name
                            </MudButton>
                        </MudTooltip>
                    }
                </MudStack>
            }
        }

        @* ── Añadir comando custom ── *@
        <MudDivider Class="my-1" />
        <MudText Typo="Typo.caption" Color="Color.Secondary">Comando personalizado</MudText>
        <MudStack Row="true" Spacing="1" AlignItems="AlignItems.Center">
            <MudTextField T="string"
                          @bind-Value="_customName"
                          Label="Nombre"
                          Variant="Variant.Outlined"
                          Margin="Margin.Dense"
                          Style="width:120px" />
            <MudTextField T="string"
                          @bind-Value="_customCommand"
                          Label="Comando"
                          Placeholder="dotnet run --project src/..."
                          Variant="Variant.Outlined"
                          Margin="Margin.Dense"
                          Style="flex:1" />
            <MudIconButton Icon="@Icons.Material.Filled.Save"
                           Color="Color.Primary"
                           Size="Size.Small"
                           Disabled="@(string.IsNullOrWhiteSpace(_customName) || string.IsNullOrWhiteSpace(_customCommand))"
                           OnClick="SaveCustomCommandAsync"
                           aria-label="Guardar comando" />
        </MudStack>

        @* ── Comandos custom guardados ── *@
        @if (_customCommands.Count > 0)
        {
            <MudStack Row="true" Wrap="Wrap.Wrap" Spacing="1">
                @foreach (var custom in _customCommands)
                {
                    <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="0">
                        <MudTooltip Text="@custom.Command">
                            <MudButton Variant="Variant.Outlined"
                                       Size="Size.Small"
                                       Color="Color.Secondary"
                                       Disabled="@_running"
                                       OnClick="() => RunCustomAsync(custom)">
                                @custom.Name
                            </MudButton>
                        </MudTooltip>
                        <MudIconButton Icon="@Icons.Material.Filled.DeleteOutline"
                                       Size="Size.Small"
                                       Color="Color.Default"
                                       OnClick="() => DeleteCustomAsync(custom.Id)"
                                       aria-label="Eliminar" />
                    </MudStack>
                }
            </MudStack>
        }

        @* ── Output ── *@
        @if (_outputLines.Count > 0 || _running)
        {
            <MudDivider Class="my-1" />
            <MudStack Row="true" AlignItems="AlignItems.Center">
                <MudText Typo="Typo.caption" Color="Color.Secondary">Output</MudText>
                <MudSpacer />
                @if (_running)
                {
                    <MudButton Size="Size.Small"
                               Color="Color.Error"
                               Variant="Variant.Outlined"
                               StartIcon="@Icons.Material.Filled.Stop"
                               OnClick="StopCommand">
                        Stop
                    </MudButton>
                }
                else
                {
                    <MudIconButton Icon="@Icons.Material.Filled.DeleteSweep"
                                   Size="Size.Small"
                                   Color="Color.Default"
                                   OnClick="ClearOutput"
                                   aria-label="Limpiar output" />
                }
            </MudStack>

            <div style="background:#0a0a0f; border-radius:4px; padding:8px; max-height:380px; overflow-y:auto; font-family:monospace; font-size:0.75rem; color:#e0e0e0;" @ref="_outputContainer">
                @foreach (var line in _outputLines)
                {
                    <div>@line</div>
                }
                @if (_running)
                {
                    <MudProgressLinear Indeterminate="true" Color="Color.Primary" Class="mt-1" />
                }
            </div>
        }

    </MudStack>
</MudDrawer>
```

**Archivo:** `src/DevHub/Components/RepoTerminalPanel.razor.cs`

- [ ] **Crear con este contenido:**

```csharp
using DevHub.Models;
using DevHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DevHub.Components;

public partial class RepoTerminalPanel
{
    [Inject] RepoCommandsService CommandsService { get; set; } = default!;
    [Inject] CustomCommandService CustomCommandService { get; set; } = default!;
    [Inject] IProcessStreamer Streamer { get; set; } = default!;
    [Inject] ISnackbar Snackbar { get; set; } = default!;
    [Inject] IJSRuntime JS { get; set; } = default!;

    private bool _open;
    private bool _loading;
    private bool _running;
    private RepoInfo? _repo;
    private IReadOnlyList<ProjectCommand> _commands = [];
    private List<CustomRepoCommand> _customCommands = [];
    private List<string> _outputLines = [];
    private CancellationTokenSource? _cts;
    private string _customName = string.Empty;
    private string _customCommand = string.Empty;
    private ElementReference _outputContainer;

    public async Task OpenForRepoAsync(RepoInfo repo)
    {
        _repo = repo;
        _open = true;
        _outputLines = [];
        _loading = true;
        StateHasChanged();

        _commands = await CommandsService.GetCommandsAsync(repo.Path);
        _customCommands = [.. await CustomCommandService.GetByRepoAsync(repo.Path)];
        _loading = false;
        StateHasChanged();
    }

    private async Task RunCommandAsync(ProjectCommand cmd)
    {
        if (_repo is null || _running)
        {
            return;
        }

        _outputLines = [$"> {cmd.Command}", ""];
        _running = true;
        _cts = new CancellationTokenSource();
        StateHasChanged();

        try
        {
            await Streamer.StreamAsync(_repo.Path, cmd.Command, async line =>
            {
                _outputLines.Add(line);
                await InvokeAsync(StateHasChanged);
                await ScrollOutputToBottomAsync();
            }, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _outputLines.Add("[detenido]");
        }
        catch (Exception ex)
        {
            _outputLines.Add($"[error] {ex.Message}");
        }
        finally
        {
            _running = false;
            _cts?.Dispose();
            _cts = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task RunCustomAsync(CustomRepoCommand custom) =>
        RunCommandAsync(new ProjectCommand(custom.Name, custom.Command, CommandSource.Custom));

    private void StopCommand()
    {
        _cts?.Cancel();
    }

    private void ClearOutput()
    {
        _outputLines = [];
    }

    private async Task SaveCustomCommandAsync()
    {
        if (_repo is null || string.IsNullOrWhiteSpace(_customName) || string.IsNullOrWhiteSpace(_customCommand))
        {
            return;
        }

        await CustomCommandService.AddAsync(_repo.Path, _customName.Trim(), _customCommand.Trim());
        _customCommands = [.. await CustomCommandService.GetByRepoAsync(_repo.Path)];
        _customName = string.Empty;
        _customCommand = string.Empty;
        Snackbar.Add("Comando guardado.", Severity.Success);
    }

    private async Task DeleteCustomAsync(int id)
    {
        await CustomCommandService.DeleteAsync(id);
        _customCommands.RemoveAll(c => c.Id == id);
    }

    private void Close()
    {
        _cts?.Cancel();
        _open = false;
    }

    private async Task ScrollOutputToBottomAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("eval",
                "document.querySelectorAll('[data-output]').forEach(el => el.scrollTop = el.scrollHeight)");
        }
        catch
        {
            // JS interop puede fallar si el componente se desmontó
        }
    }

    private static string SourceLabel(CommandSource source) => source switch
    {
        CommandSource.AutoDetected => "Detectados",
        CommandSource.PackageJson => "Scripts (package.json)",
        CommandSource.Custom => "Personalizados",
        _ => string.Empty
    };
}
```

- [ ] **Commit:**
```bash
git add src/DevHub/Components/RepoTerminalPanel.razor src/DevHub/Components/RepoTerminalPanel.razor.cs
git commit -m "feat: add RepoTerminalPanel drawer with command execution and live output"
```

---

## Task 10: Conectar RepoRow → Home → Panel

### 10a: Modificar RepoRow

**Archivo:** `src/DevHub/Components/RepoRow.razor`

- [ ] **Reemplazar el bloque del icono Terminal (actualmente copia la ruta al clipboard):**

Buscar este bloque:
```razor
<MudTooltip Text="@($"Copiar ruta: {Repo.Path}")">
    <MudIconButton Icon="@Icons.Material.Filled.Terminal"
                   Size="Size.Small"
                   aria-label="Copiar ruta del repo"
                   OnClick="CopyPath" />
</MudTooltip>
```

Reemplazarlo por:
```razor
<MudTooltip Text="Comandos del repo">
    <MudIconButton Icon="@Icons.Material.Filled.Terminal"
                   Size="Size.Small"
                   Color="Color.Primary"
                   aria-label="Abrir terminal"
                   OnClick="OpenTerminal" />
</MudTooltip>
```

**Archivo:** `src/DevHub/Components/RepoRow.razor.cs`

- [ ] **Añadir el nuevo parámetro y método** (dentro de la clase `RepoRow`, después del parámetro `OnRemoveRequested`):

```csharp
[Parameter] public EventCallback<RepoInfo> OnTerminalRequested { get; set; }
```

- [ ] **Añadir el método** (donde estaba `CopyPath`):

```csharp
private Task OpenTerminal() => OnTerminalRequested.InvokeAsync(Repo);
```

- [ ] **Eliminar el método `CopyPath` y el `@inject IJSRuntime JS` del razor** si ya no se usa en ningún otro lugar del componente. Verificar primero que no lo use `OpenPullRequest` — en este caso, `OpenPullRequest` sí usa `JS`, así que **dejar el inject**.

- [ ] **Eliminar solo el método `CopyPath`** del `.razor.cs`:
```csharp
// BORRAR este método:
private async Task CopyPath()
{
    await JS.InvokeVoidAsync("navigator.clipboard.writeText", Repo.Path);
    Snackbar.Add($"Ruta copiada: {Repo.Path}", Severity.Info, cfg => cfg.VisibleStateDuration = 2000);
}
```

### 10b: Modificar RepoList para pasar el evento

**Archivo:** `src/DevHub/Components/RepoList.razor`

- [ ] Leer el archivo y localizar dónde se usa `<RepoRow`. Añadir el parámetro `OnTerminalRequested` al componente RepoRow que se renderiza, pasando un callback hacia arriba:

```razor
<RepoRow Repo="@repo"
         Selected="@SelectedPaths.Contains(repo.Path)"
         SelectedChanged="@(v => OnRowSelectedChanged(repo, v))"
         OnRemoveRequested="@(() => OnRemoveRequested.InvokeAsync(repo))"
         OnTerminalRequested="OnTerminalRequested" />
```

- [ ] **En el code-behind de RepoList** (`RepoList.razor.cs`), añadir el parámetro:
```csharp
[Parameter] public EventCallback<RepoInfo> OnTerminalRequested { get; set; }
```

### 10c: Modificar Home para añadir el panel

**Archivo:** `src/DevHub/Components/Pages/Home.razor`

- [ ] **Añadir el using y el componente panel** justo antes del cierre del archivo (después de la barra de estado):

```razor
@using DevHub.Components
```
(ya debería estar en `_Imports.razor`, verificar)

- [ ] **Añadir el panel al final del markup, antes del cierre del último elemento:**

```razor
<RepoTerminalPanel @ref="_terminalPanel" />
```

- [ ] **Añadir `OnTerminalRequested` al componente `<RepoList>`:**

```razor
<RepoList Groups="_filteredGroups"
          SelectedPaths="_selectedPaths"
          SelectedPathsChanged="OnSelectedPathsChanged"
          OnRemoveRequested="RemoveRepoAsync"
          OnTerminalRequested="OpenTerminalAsync" />
```

**Archivo:** `src/DevHub/Components/Pages/Home.razor.cs`

- [ ] **Añadir el campo `@ref` y el método** dentro de la clase `Home`:

```csharp
private RepoTerminalPanel _terminalPanel = default!;

private Task OpenTerminalAsync(RepoInfo repo) =>
    _terminalPanel.OpenForRepoAsync(repo);
```

- [ ] **Commit:**
```bash
git add src/DevHub/Components/RepoRow.razor src/DevHub/Components/RepoRow.razor.cs
git add src/DevHub/Components/RepoList.razor src/DevHub/Components/RepoList.razor.cs
git add src/DevHub/Components/Pages/Home.razor src/DevHub/Components/Pages/Home.razor.cs
git commit -m "feat: wire terminal icon in RepoRow to open RepoTerminalPanel"
```

---

## Task 11: Verificación final

- [ ] **Compilar sin errores:**
```bash
dotnet build src/DevHub/DevHub.csproj --no-restore -v q 2>&1 | grep -E "error (CS|IDE|RZ)"
```
Esperado: sin salida.

- [ ] **Todos los tests en verde:**
```bash
dotnet test tests/DevHub.U.Tests/DevHub.U.Tests.csproj --no-restore -v q
```
Esperado: todos los tests existentes + los 7 nuevos en verde.

- [ ] **Commit final de verificación (solo si hay cambios pendientes):**
```bash
git status
# Si hay algo sin commitear:
git add -A
git commit -m "chore: final cleanup after terminal panel implementation"
```

---

## Resultado esperado

- Clic en el icono Terminal de cualquier repo → se abre un drawer en la derecha
- El drawer detecta automáticamente el tipo de proyecto y muestra los comandos relevantes
- Si hay `package.json` con scripts, se listan también
- El usuario puede guardar comandos custom por repo (persisten en DB)
- Al hacer clic en un comando, se ejecuta y el output aparece línea a línea en tiempo real
- Botón Stop para cancelar la ejecución en curso
- El icono Terminal ya no copia la ruta al clipboard (esa funcionalidad se elimina)
