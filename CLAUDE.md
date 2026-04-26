# CLAUDE.md — Woffu Orchestra

Herramienta interna de desarrollo para el equipo de Woffu. Proyecto **local**, NO es un repo compartido. Corre como Windows Service en `http://localhost:5200`.

## Propósito

Dashboard para trabajar con los ~77 repos de Woffu (clonados en `C:\_O`) y herramientas auxiliares como el mapa de comunicación Service Bus entre microservicios.

## Stack

- **.NET 10** · Blazor Server · `@rendermode InteractiveServer`
- **MudBlazor 9.3.0** — SIEMPRE componentes MudBlazor, nunca HTML crudo para UI (excepto `<iframe>` o similares cuando no hay equivalente)
- **Dark theme forzado** (primary `#c084fc`, background `#0f0f14`)
- **xUnit + Moq** para tests
- **PowerShell** para scripts auxiliares (viven en `scripts/`)

## Estructura

```
woffu-orchestra/
  scripts/                      ← PowerShell scripts (generadores, utilidades)
  src/Woffu.Tools.RepoOrchestra/
    Components/
      Layout/                   ← MainLayout, NavMenu, ReconnectModal
      Pages/                    ← una página por feature
    Services/                   ← lógica de negocio, sin UI
    Models/                     ← DTOs, options
    Helpers/
    wwwroot/
      maps/                     ← HTMLs estáticos generados (no editar a mano)
    Program.cs
    appsettings.json
  tests/Woffu.Tools.RepoOrchestra.U.Tests/
    Services/
      When_<Condicion>/
        Then_<Resultado>.cs     ← un test por archivo
  docs/
    plans/                      ← planes de implementación
```

## Convenciones

### C#
- **Primary constructors siempre** — nunca constructor con cuerpo explícito
- **Servicios como Singleton** salvo que tengan estado por request
- **Records** para DTOs y resultados inmutables
- `IProcessRunner` para cualquier ejecución de proceso externo (para testabilidad)
- `IOptions<T>` para configuración desde `appsettings.json`
- Namespaces file-scoped

### Blazor
- Cada página: `@rendermode InteractiveServer`
- UI con MudBlazor: `MudStack`, `MudButton`, `MudAlert`, `MudProgressLinear`, etc.
- Icons: `Icons.Material.Filled.*`
- Colores vía variables MudBlazor: `var(--mud-palette-primary)`, etc.

### Tests
Patrón obligatorio del proyecto:
```
tests/.../Services/When_<Condicion>/Then_<Resultado>.cs
```
- Una clase `When_*` por carpeta
- Un método `[Fact] public async Task Execute()` por archivo
- Mock de dependencias con `Moq`

Correr antes de cada commit:
```bash
dotnet test
```

### Commits
- Formato `<type>: <description>` (feat, fix, docs, test, refactor, chore)
- Commit pequeños y frecuentes — uno por paso lógico del plan

### Pull Requests
- Si el cambio toca la UI, **siempre** incluir un screenshot en el cuerpo del PR.
- Para tomar capturas usar Puppeteer headless desde este entorno, guardar en `docs/screenshots/` y referenciar con la URL raw de GitHub en el PR body.
- Un PR por tipo de cambio: código en un PR, assets/screenshots en otro si aplica.

## Features

| Página | Ruta | Descripción |
|--------|------|-------------|
| Home | `/` | Lista de repos clonados en `C:\_O` con git status, filtros, bulk actions |
| Service Bus Map | `/service-bus-map` | Mapa de comunicación MassTransit entre microservicios |

## Agregar una nueva feature

1. Crear `Services/MiFeatureService.cs` (primary constructor, usar `IProcessRunner` si ejecuta shell)
2. Registrar en `Program.cs`: `builder.Services.AddSingleton<MiFeatureService>();`
3. Si necesita config: crear options class + sección en `appsettings.json` + `builder.Services.Configure<T>(...)`
4. Crear `Components/Pages/MiFeature.razor` con `@page "/mi-feature"` y `@rendermode InteractiveServer`
5. Agregar NavLink en `Components/Layout/NavMenu.razor`
6. Tests en `tests/.../Services/When_MiFeatureService_*/`

## Servicios existentes (referencia)

| Servicio | Propósito |
|----------|-----------|
| `GitCliService` | Wrapper de git.exe (GetStatus, Pull, Checkout, etc.) |
| `RepoScannerService` | `BackgroundService` que escanea repos cada N segundos |
| `RepoStateStore` | Estado thread-safe con evento `OnStateChanged` |
| `VersionService` | Tracking de actualizaciones de versión |
| `IProcessRunner` / `ProcessRunner` | Ejecución de procesos externos (testeable) |
| `ServiceBusMapService` | Genera el mapa de Service Bus via `generate-servicebus-map.ps1` |

## Scripts

Viven en `scripts/`. Se ejecutan desde el servicio correspondiente mediante `IProcessRunner`.

| Script | Descripción |
|--------|-------------|
| `generate-servicebus-map.ps1` | Escanea `C:\_O\Woffu.Services.*` buscando `IntegrationEventHandler<T>` (consumers) y definiciones de `IntegrationEvent` en `*.IE.Publisher` (publishers); inyecta el JSON resultante en el template y genera `wwwroot/maps/servicebus-map.html` |

## Configuración

`appsettings.json` tiene una sección por módulo. Bindear con `builder.Services.Configure<MisOptions>(builder.Configuration.GetSection("MiModulo"))`.

```json
{
  "RepoOrchestra":  { "RootPath": "C:\\_O", ... },
  "ServiceBusMap":  { "ScriptPath": "...", "ReposRoot": "C:\\_O", ... }
}
```

## Publish / Deploy

El proyecto corre como Windows Service. Ver `install-service.ps1` y `publish.ps1`.
