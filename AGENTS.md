# AGENTS.md — DevHub

Herramienta interna de desarrollo. Proyecto **local**, corre como Windows Service en `http://localhost:5200`.

## Propósito

Dashboard para trabajar con múltiples repos git y herramientas auxiliares como el mapa de comunicación Service Bus entre microservicios.

## Stack

- **.NET 10** · Blazor Server · `@rendermode InteractiveServer`
- **MudBlazor 9.3.0** — SIEMPRE componentes MudBlazor, nunca HTML crudo para UI (excepto `<iframe>` o similares cuando no hay equivalente)
- **Dark theme forzado** (primary `#c084fc`, background `#0f0f14`)
- **EF Core** con SQLite (dev) o SQL Server (prod) via `IDbContextFactory<ApplicationDbContext>`
- **xUnit + Moq** para tests

## Estructura

```
woffu-orchestra/
  src/
    DevHub/                       ← proyecto principal Blazor Server
      Components/
        Layout/                   ← MainLayout, NavMenu, ReconnectModal
        Pages/                    ← una página por feature (solo markup + @inject)
      Services/                   ← lógica de negocio, sin UI
      Models/                     ← DTOs, options, entidades EF
      Data/                       ← ApplicationDbContext
      wwwroot/
        maps/                     ← HTMLs estáticos generados (no editar a mano)
      Program.cs
      appsettings.json
    DevHub.Analyzers/             ← Roslyn analyzers custom (DH001, etc.)
  tests/
    DevHub.U.Tests/
      Services/
        When_<Condicion>/
          Then_<Resultado>.cs     ← un test por archivo
  docs/
    plans/                        ← planes de implementación
```

## Convenciones

### C#
- **Primary constructors siempre** — nunca constructor con cuerpo explícito
- **`IDbContextFactory<ApplicationDbContext>`** en servicios singleton — nunca inyectar `DbContext` directamente
- **Records** para DTOs y resultados inmutables
- `IProcessRunner` para cualquier ejecución de proceso externo (para testabilidad)
- `IOptions<T>` para configuración desde `appsettings.json`
- Namespaces file-scoped
- `is null` / `is not null` — nunca `== null` / `!= null`
- Braces obligatorios en todos los `if`/`else`/`for`/`foreach`

### EF Core
- FluentAPI únicamente en `OnModelCreating` — cero data annotations en entidades
- Nombre de tabla por convención del `DbSet` (no setear `ToTable()` salvo excepción)

### Blazor
- **Code-behind obligatorio**: lógica en `ComponentName.razor.cs`, el `.razor` solo markup y `@inject`
- `@rendermode InteractiveServer` en cada página
- UI con MudBlazor: `MudStack`, `MudButton`, `MudAlert`, `MudProgressLinear`, etc.
- Icons: `Icons.Material.Filled.*`

### Analyzers activos (errores de compilación)
| ID | Regla |
|----|-------|
| DH001 | `@code` en `.razor` — mover a `.razor.cs` |
| IDE0041 | `== null` → `is null` |
| IDE0270 | `!= null` → `is not null` |
| IDE0011 | Braces obligatorios |
| CA1305 | `IFormatProvider` en format strings |
| CA1848 | `[LoggerMessage]` en lugar de `logger.Log*` directo |

### Tests
Patrón obligatorio:
```
tests/.../Services/When_<Condicion>/Then_<Resultado>.cs
```
- Una clase `When_*` por carpeta
- Un método `[Fact] public async Task <Name>_Run()` por archivo
- DB: SQLite in-memory (`Data Source=:memory:`) — nunca SQL Server en tests
- Underscores permitidos solo en tests (CA1707 suprimido en `tests/**`)

### Commits
- Formato `<type>: <description>` (feat, fix, docs, test, refactor, chore)
- Commits pequeños y frecuentes
- Todo entra por PR — `main` está protegido

## CI

GitHub Actions corre en cada PR:
1. `dotnet build -warnaserror` — todos los analyzers tienen que pasar
2. `dotnet test` — todos los tests tienen que pasar

## Features

| Página | Ruta | Descripción |
|--------|------|-------------|
| Home | `/` | Lista de repos con git status, filtros, bulk actions |
| Settings | `/settings` | Group rules ABM, repo catalog |
| Secret Profiles | `/secret-profiles` | Gestión de user secrets por servicio |
| Service Bus Map | `/service-bus-map` | Mapa de comunicación MassTransit entre microservicios |

## Servicios existentes

| Servicio | Propósito |
|----------|-----------|
| `GitCliService` | Wrapper de git (scan, pull, checkout) |
| `RepoScannerService` | `BackgroundService` que escanea repos cada N segundos |
| `RepoStateStore` | Estado thread-safe con `volatile` + `ImmutableArray` |
| `EfRepoCatalogService` | CRUD de repos en DB |
| `GroupRuleService` | CRUD de reglas de agrupación en DB |
| `SecretProfileService` | Gestión de perfiles de user secrets |
| `IProcessRunner` / `ProcessRunner` | Ejecución de procesos externos (testeable) |
| `ServiceBusMapService` | Genera el mapa de Service Bus via PowerShell |

## Publish / Deploy

Corre como Windows Service. Ver `install-service.ps1` y `publish.ps1`.
