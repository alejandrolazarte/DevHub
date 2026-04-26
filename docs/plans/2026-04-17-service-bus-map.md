# Service Bus Map Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrar una página "Service Bus Map" en la app Blazor woffu-orchestra que muestra el mapa de comunicación entre microservicios vía Azure Service Bus, con un botón para regenerar el mapa ejecutando el script PowerShell existente.

**Architecture:** La página Blazor sirve el HTML generado en un `<iframe>` (el mapa usa Cytoscape.js y debe correr aislado). Un `ServiceBusMapService` ejecuta el script `generate-servicebus-map.ps1` mediante un `IProcessRunner` inyectable para hacer la lógica testeable. El script vive en `scripts/` y escribe el output a `wwwroot/maps/servicebus-map.html` que Blazor sirve como estático.

**Tech Stack:** .NET 10, Blazor Server InteractiveServer, MudBlazor 9.3.0, PowerShell (pwsh.exe), Cytoscape.js (en el HTML embebido)

---

## Task 1: Mover archivos de C:\_O al proyecto

**Files:**
- Create: `scripts/generate-servicebus-map.ps1` (movido desde `C:\_O`)
- Create: `src/Woffu.Tools.RepoOrchestra/wwwroot/maps/servicebus-map.template.html` (movido desde `C:\_O`)
- Delete: `C:\_O/generate-servicebus-map.ps1`
- Delete: `C:\_O/docs/servicebus-map.html`, `C:\_O/docs/servicebus-map.md`, `C:\_O/docs/servicebus-map.template.html`

- [ ] **Step 1: Copiar el script al proyecto**

```powershell
Copy-Item "C:\_O\generate-servicebus-map.ps1" "C:\woffu-orchestra\scripts\generate-servicebus-map.ps1"
```

- [ ] **Step 2: Crear la carpeta maps en wwwroot y copiar el template**

```powershell
New-Item -ItemType Directory -Force "C:\woffu-orchestra\src\Woffu.Tools.RepoOrchestra\wwwroot\maps"
Copy-Item "C:\_O\docs\servicebus-map.template.html" "C:\woffu-orchestra\src\Woffu.Tools.RepoOrchestra\wwwroot\maps\servicebus-map.template.html"
```

- [ ] **Step 3: Limpiar los archivos del repo compartido C:\_O**

```powershell
Remove-Item "C:\_O\generate-servicebus-map.ps1"
Remove-Item "C:\_O\docs\servicebus-map.html" -ErrorAction SilentlyContinue
Remove-Item "C:\_O\docs\servicebus-map.md" -ErrorAction SilentlyContinue
Remove-Item "C:\_O\docs\servicebus-map.template.html"
```

- [ ] **Step 4: Verificar que la estructura quedó bien**

```
woffu-orchestra/
  scripts/
    generate-servicebus-map.ps1   ✓
  src/.../wwwroot/maps/
    servicebus-map.template.html  ✓
```

- [ ] **Step 5: Commit**

```bash
git add scripts/ src/Woffu.Tools.RepoOrchestra/wwwroot/maps/
git commit -m "feat: move service bus map scripts and template into project"
```

---

## Task 2: Agregar ServiceBusMapOptions

**Files:**
- Modify: `src/Woffu.Tools.RepoOrchestra/Services/RepoScannerService.cs` (agregar `ServiceBusMapOptions` al final del archivo, siguiendo el patrón de `RepoOrchestraOptions`)
- Modify: `src/Woffu.Tools.RepoOrchestra/appsettings.json`

- [ ] **Step 1: Agregar la clase de options al final de RepoScannerService.cs**

En `src/Woffu.Tools.RepoOrchestra/Services/RepoScannerService.cs`, agregar al final del archivo:

```csharp
public class ServiceBusMapOptions
{
    public string ScriptPath   { get; set; } = @"scripts\generate-servicebus-map.ps1";
    public string TemplateFile { get; set; } = @"wwwroot\maps\servicebus-map.template.html";
    public string OutputFile   { get; set; } = @"wwwroot\maps\servicebus-map.html";
    public string ReposRoot    { get; set; } = @"C:\_O";
}
```

- [ ] **Step 2: Agregar la sección al appsettings.json**

```json
{
  "RepoOrchestra": {
    "RootPath": "C:\\_O",
    "ScanIntervalSeconds": 60,
    "ParallelScanDegree": 8,
    "ExcludedRepos": []
  },
  "ServiceBusMap": {
    "ScriptPath": "scripts\\generate-servicebus-map.ps1",
    "TemplateFile": "wwwroot\\maps\\servicebus-map.template.html",
    "OutputFile": "wwwroot\\maps\\servicebus-map.html",
    "ReposRoot": "C:\\_O"
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

- [ ] **Step 3: Commit**

```bash
git add src/Woffu.Tools.RepoOrchestra/Services/RepoScannerService.cs
git add src/Woffu.Tools.RepoOrchestra/appsettings.json
git commit -m "feat: add ServiceBusMapOptions"
```

---

## Task 3: Crear IProcessRunner y ProcessRunner

**Files:**
- Create: `src/Woffu.Tools.RepoOrchestra/Services/IProcessRunner.cs`
- Create: `src/Woffu.Tools.RepoOrchestra/Services/ProcessRunner.cs`

Abstracción necesaria para que `ServiceBusMapService` sea testeable sin ejecutar procesos reales.

- [ ] **Step 1: Crear IProcessRunner.cs**

```csharp
namespace Woffu.Tools.RepoOrchestra.Services;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, string arguments, string workingDirectory, CancellationToken ct = default);
}

public record ProcessResult(int ExitCode, string Output, string Error)
{
    public bool Success => ExitCode == 0;
}
```

- [ ] **Step 2: Crear ProcessRunner.cs**

```csharp
using System.Diagnostics;
using System.Text;

namespace Woffu.Tools.RepoOrchestra.Services;

public class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string fileName, string arguments, string workingDirectory, CancellationToken ct = default)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder  = new StringBuilder();

        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory       = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived  += (_, e) => { if (e.Data is not null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        return new ProcessResult(process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }
}
```

- [ ] **Step 3: Build para verificar que compila**

```bash
dotnet build src/Woffu.Tools.RepoOrchestra/
```

Expected: `Build succeeded`

- [ ] **Step 4: Commit**

```bash
git add src/Woffu.Tools.RepoOrchestra/Services/IProcessRunner.cs
git add src/Woffu.Tools.RepoOrchestra/Services/ProcessRunner.cs
git commit -m "feat: add IProcessRunner abstraction for testable process execution"
```

---

## Task 4: Crear ServiceBusMapService

**Files:**
- Create: `src/Woffu.Tools.RepoOrchestra/Services/ServiceBusMapService.cs`

- [ ] **Step 1: Crear ServiceBusMapService.cs**

```csharp
using Microsoft.Extensions.Options;

namespace Woffu.Tools.RepoOrchestra.Services;

public record GenerateResult(bool Success, string Output, DateTimeOffset GeneratedAt);

public class ServiceBusMapService(
    IProcessRunner processRunner,
    IOptions<ServiceBusMapOptions> options,
    ILogger<ServiceBusMapService> logger)
{
    private readonly ServiceBusMapOptions _options = options.Value;

    public bool MapExists =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, _options.OutputFile));

    public async Task<GenerateResult> GenerateAsync(CancellationToken ct = default)
    {
        var workingDir   = AppContext.BaseDirectory;
        var scriptPath   = Path.Combine(workingDir, _options.ScriptPath);
        var templateFile = Path.Combine(workingDir, _options.TemplateFile);
        var outputFile   = Path.Combine(workingDir, _options.OutputFile);

        var arguments = $"-NonInteractive -File \"{scriptPath}\" " +
                        $"-ReposRoot \"{_options.ReposRoot}\" " +
                        $"-TemplateFile \"{templateFile}\" " +
                        $"-OutputFile \"{outputFile}\"";

        logger.LogInformation("Running service bus map generator. Script: {Script}", scriptPath);

        var result = await processRunner.RunAsync("pwsh.exe", arguments, workingDir, ct);

        var output = string.IsNullOrWhiteSpace(result.Error)
            ? result.Output
            : $"{result.Output}\nERROR:\n{result.Error}";

        if (!result.Success)
            logger.LogError("Generator failed (exit {Code}):\n{Output}", result.ExitCode, output);
        else
            logger.LogInformation("Generator succeeded:\n{Output}", output);

        return new GenerateResult(result.Success, output, DateTimeOffset.Now);
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/Woffu.Tools.RepoOrchestra/
```

Expected: `Build succeeded`

- [ ] **Step 3: Commit**

```bash
git add src/Woffu.Tools.RepoOrchestra/Services/ServiceBusMapService.cs
git commit -m "feat: add ServiceBusMapService"
```

---

## Task 5: Tests para ServiceBusMapService

**Files:**
- Create: `tests/Woffu.Tools.RepoOrchestra.U.Tests/Services/When_ServiceBusMapService_generates/Then_returns_success_on_exit_zero.cs`
- Create: `tests/Woffu.Tools.RepoOrchestra.U.Tests/Services/When_ServiceBusMapService_generates/Then_returns_failure_on_nonzero_exit.cs`

Seguir el patrón del proyecto: una clase `When_<condición>` por carpeta, un método `Then_<resultado>` por archivo.

- [ ] **Step 1: Crear el test de éxito**

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Woffu.Tools.RepoOrchestra.Services;

namespace Woffu.Tools.RepoOrchestra.U.Tests.Services.When_ServiceBusMapService_generates;

public class Then_returns_success_on_exit_zero
{
    [Fact]
    public async Task Execute()
    {
        var runner = new Mock<IProcessRunner>();
        runner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "  Services : 10\n  Edges    : 25\n", string.Empty));

        var options = Options.Create(new ServiceBusMapOptions
        {
            ScriptPath   = "scripts/generate-servicebus-map.ps1",
            TemplateFile = "wwwroot/maps/servicebus-map.template.html",
            OutputFile   = "wwwroot/maps/servicebus-map.html",
            ReposRoot    = @"C:\_O",
        });

        var sut = new ServiceBusMapService(runner.Object, options, NullLogger<ServiceBusMapService>.Instance);

        var result = await sut.GenerateAsync();

        Assert.True(result.Success);
        Assert.Contains("Services", result.Output);
    }
}
```

- [ ] **Step 2: Ejecutar el test — debe fallar (aún no compilamos el test)**

```bash
dotnet test tests/Woffu.Tools.RepoOrchestra.U.Tests/ --filter "Then_returns_success_on_exit_zero"
```

Expected: FAIL — `error CS0246: The type or namespace name 'ServiceBusMapService' could not be found`  
(Confirma que el test es el que compila el servicio, no al revés)

- [ ] **Step 3: Crear el test de fallo**

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Woffu.Tools.RepoOrchestra.Services;

namespace Woffu.Tools.RepoOrchestra.U.Tests.Services.When_ServiceBusMapService_generates;

public class Then_returns_failure_on_nonzero_exit
{
    [Fact]
    public async Task Execute()
    {
        var runner = new Mock<IProcessRunner>();
        runner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, string.Empty, "Template not found"));

        var options = Options.Create(new ServiceBusMapOptions());

        var sut = new ServiceBusMapService(runner.Object, options, NullLogger<ServiceBusMapService>.Instance);

        var result = await sut.GenerateAsync();

        Assert.False(result.Success);
        Assert.Contains("Template not found", result.Output);
    }
}
```

- [ ] **Step 4: Ejecutar ambos tests — deben pasar**

```bash
dotnet test tests/Woffu.Tools.RepoOrchestra.U.Tests/ --filter "When_ServiceBusMapService_generates"
```

Expected: `Passed: 2`

- [ ] **Step 5: Commit**

```bash
git add tests/Woffu.Tools.RepoOrchestra.U.Tests/Services/
git commit -m "test: add ServiceBusMapService unit tests"
```

---

## Task 6: Registrar servicios en Program.cs

**Files:**
- Modify: `src/Woffu.Tools.RepoOrchestra/Program.cs`

- [ ] **Step 1: Agregar registros en Program.cs**

Agregar después de `builder.Services.AddSingleton<RepoScannerService>()`:

```csharp
builder.Services.Configure<ServiceBusMapOptions>(
    builder.Configuration.GetSection("ServiceBusMap"));
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton<ServiceBusMapService>();
```

- [ ] **Step 2: Build**

```bash
dotnet build src/Woffu.Tools.RepoOrchestra/
```

Expected: `Build succeeded`

- [ ] **Step 3: Commit**

```bash
git add src/Woffu.Tools.RepoOrchestra/Program.cs
git commit -m "feat: register ServiceBusMap services in DI"
```

---

## Task 7: Crear la página ServiceBusMap.razor

**Files:**
- Create: `src/Woffu.Tools.RepoOrchestra/Components/Pages/ServiceBusMap.razor`

La página muestra el mapa en un `<iframe>` con cache-busting y un botón Actualizar que muestra el output del script.

- [ ] **Step 1: Crear ServiceBusMap.razor**

```razor
@page "/service-bus-map"
@rendermode InteractiveServer
@inject ServiceBusMapService MapService

<PageTitle>Service Bus Map</PageTitle>

<MudStack Spacing="3" Style="height: calc(100vh - 80px);">

    <MudStack Row AlignItems="AlignItems.Center" Justify="Justify.SpaceBetween">
        <MudText Typo="Typo.h6">Service Bus Map</MudText>
        <MudButton Variant="Variant.Filled"
                   Color="Color.Primary"
                   StartIcon="@Icons.Material.Filled.Refresh"
                   OnClick="RegenerateAsync"
                   Disabled="_isGenerating">
            @(_isGenerating ? "Generando..." : "Actualizar")
        </MudButton>
    </MudStack>

    @if (_isGenerating)
    {
        <MudProgressLinear Indeterminate Color="Color.Primary" />
    }

    @if (_lastResult is not null)
    {
        <MudAlert Severity="@(_lastResult.Success ? Severity.Success : Severity.Error)"
                  Dense
                  ShowCloseIcon
                  CloseIconClicked="() => _lastResult = null">
            @if (_lastResult.Success)
            {
                <span>Mapa actualizado — @_lastResult.GeneratedAt.ToString("HH:mm:ss")</span>
            }
            else
            {
                <span>Error al generar el mapa</span>
            }
        </MudAlert>

        @if (!string.IsNullOrWhiteSpace(_lastResult.Output))
        {
            <MudExpansionPanels Dense>
                <MudExpansionPanel Text="Ver output del script">
                    <pre style="font-size:11px; color: var(--mud-palette-text-secondary); white-space: pre-wrap;">@_lastResult.Output</pre>
                </MudExpansionPanel>
            </MudExpansionPanels>
        }
    }

    @if (_mapExists)
    {
        <iframe src="@_iframeSrc"
                style="flex:1; border:none; border-radius:8px; background:#0f1117;"
                title="Service Bus Map">
        </iframe>
    }
    else
    {
        <MudPaper Class="d-flex align-center justify-center" Style="flex:1;" Elevation="0" Outlined>
            <MudStack AlignItems="AlignItems.Center" Spacing="2">
                <MudIcon Icon="@Icons.Material.Filled.AccountTree" Size="Size.Large" Color="Color.Default" />
                <MudText Color="Color.Default">El mapa aún no fue generado.</MudText>
                <MudText Typo="Typo.body2" Color="Color.Default">Hacé click en <b>Actualizar</b> para generarlo.</MudText>
            </MudStack>
        </MudPaper>
    }

</MudStack>

@code {
    private bool          _isGenerating = false;
    private bool          _mapExists;
    private string        _iframeSrc = "/maps/servicebus-map.html";
    private GenerateResult? _lastResult;

    protected override void OnInitialized()
    {
        _mapExists = MapService.MapExists;
    }

    private async Task RegenerateAsync()
    {
        _isGenerating = true;
        _lastResult   = null;
        StateHasChanged();

        _lastResult   = await MapService.GenerateAsync();
        _mapExists    = MapService.MapExists;
        _isGenerating = false;

        if (_lastResult.Success)
            _iframeSrc = $"/maps/servicebus-map.html?v={DateTimeOffset.Now.ToUnixTimeSeconds()}";

        StateHasChanged();
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/Woffu.Tools.RepoOrchestra/
```

Expected: `Build succeeded`

- [ ] **Step 3: Commit**

```bash
git add src/Woffu.Tools.RepoOrchestra/Components/Pages/ServiceBusMap.razor
git commit -m "feat: add ServiceBusMap page with iframe and refresh button"
```

---

## Task 8: Agregar nav item en NavMenu.razor

**Files:**
- Modify: `src/Woffu.Tools.RepoOrchestra/Components/Layout/NavMenu.razor`

- [ ] **Step 1: Agregar el NavLink después del de Home**

```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="service-bus-map">
        <span class="bi bi-diagram-3-fill-nav-menu" aria-hidden="true"></span> Service Bus Map
    </NavLink>
</div>
```

El icono `bi-diagram-3-fill` de Bootstrap Icons encaja bien con un mapa de conexiones.

- [ ] **Step 2: Build y run rápido para verificar que navega**

```bash
dotnet run --project src/Woffu.Tools.RepoOrchestra/
```

Abrir `http://localhost:5200` → el menú debe mostrar "Service Bus Map" → click → página carga.

- [ ] **Step 3: Commit**

```bash
git add src/Woffu.Tools.RepoOrchestra/Components/Layout/NavMenu.razor
git commit -m "feat: add Service Bus Map nav item"
```

---

## Task 9: Crear CLAUDE.md

**Files:**
- Create: `CLAUDE.md`

- [ ] **Step 1: Crear CLAUDE.md en la raíz del proyecto**

```markdown
# CLAUDE.md — Woffu Orchestra

Herramienta interna de desarrollo para el equipo de Woffu.
Proyecto local, NO es un repo compartido. Corre como Windows Service en localhost:5200.

## Stack
- .NET 10 · Blazor Server · InteractiveServer render mode
- MudBlazor 9.3.0 — SIEMPRE usar componentes MudBlazor, nunca HTML crudo para UI
- Dark theme forzado (primary: #c084fc, background: #0f0f14)

## Estructura
```
src/Woffu.Tools.RepoOrchestra/
  Components/
    Layout/        → NavMenu, MainLayout
    Pages/         → una página por feature (Home, ServiceBusMap, ...)
  Services/        → lógica de negocio, sin dependencias de UI
  wwwroot/maps/    → HTMLs generados estáticos (no editar a mano)
scripts/           → PowerShell scripts de generación
tests/             → xUnit, Moq, patrón When_*/Then_*
```

## Convenciones
- **Primary constructors** siempre — nunca constructor con cuerpo
- **Servicios como Singleton** salvo que tengan estado por request
- `IProcessRunner` para cualquier ejecución de proceso externo (testeable)
- Páginas: `@rendermode InteractiveServer` en todas

## Tests — patrón obligatorio
```
tests/.../Services/
  When_<Condicion>/
    Then_<Resultado>.cs   ← una clase When_, un método Then_, un test
```
Correr antes de cada commit: `dotnet test`

## Features actuales
| Página | Ruta | Descripción |
|--------|------|-------------|
| Home | `/` | Lista de repos clonados en C:\_O con git status |
| Service Bus Map | `/service-bus-map` | Mapa de comunicación MassTransit entre microservicios |

## Agregar una nueva feature
1. Crear `Services/MiFeatureService.cs` con `IProcessRunner` si necesita shell
2. Registrar en `Program.cs`
3. Crear `Components/Pages/MiFeature.razor` con `@page "/mi-feature"`
4. Agregar NavLink en `Components/Layout/NavMenu.razor`
5. Tests en `tests/.../Services/When_MiFeatureService_*/`
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: add CLAUDE.md with project conventions and feature map"
```

---

## Verificación final

- [ ] `dotnet test` — todos en verde
- [ ] `dotnet run` — app levanta en `http://localhost:5200`
- [ ] Navegar a `/service-bus-map` — página carga con mensaje "mapa no generado"
- [ ] Click "Actualizar" — spinner aparece, script corre, iframe carga el mapa
- [ ] Buscar un evento en el sidebar del mapa — resalta las conexiones
- [ ] Click "Actualizar" de nuevo — mapa se recarga con datos frescos
