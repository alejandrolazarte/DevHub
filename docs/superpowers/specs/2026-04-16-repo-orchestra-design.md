# Repo Orchestra — Design Spec

**Date:** 2026-04-16  
**Status:** Approved  
**Author:** Alejandro Lazarte

---

## Overview

Repo Orchestra is a local web app that provides a unified view of all ~77 Woffu repositories cloned under `C:\_O`. It solves the daily friction of managing multiple repos across features, checking git state, and performing bulk operations (pull, branch switch) without opening a terminal per repo.

Runs locally with `dotnet run`. No cloud, no database, no installation beyond .NET 10.

---

## Goals

- See at a glance which repos are dirty, behind, or on a feature branch
- Perform bulk git operations (pull, checkout) on a selection of repos
- Auto-refresh state every 60 seconds; manual refresh also available
- Fast to start, easy to maintain, readable code

## Non-Goals (MVP)

- No Azure DevOps integration (future: Dev Hub)
- No authentication
- No database persistence (future: SQLite for preferences/notes)
- No git push, merge, or commit operations (read + pull + checkout only)
- No diff viewer

---

## Tech Stack

| Layer | Technology | License |
|-------|-----------|---------|
| Runtime | .NET 10 | MIT |
| UI framework | Blazor Server | MIT |
| UI components | MudBlazor | MIT |
| Git operations | `git` CLI via `Process.Start` | GPL-2 (external binary, not linked) |
| Configuration | `appsettings.json` (.NET built-in) | MIT |

No paid or proprietary libraries. No database for MVP.

---

## Architecture

Single .NET 10 project: `Woffu.Tools.RepoOrchestra`.

```
Woffu.Tools.RepoOrchestra/
  Components/
    RepoList.razor       ← tabla principal de repos
    RepoRow.razor        ← fila individual con estado
    FilterBar.razor      ← filtros (nombre, grupo, estado)
    BulkActions.razor    ← botones Pull / Checkout sobre selección
  Services/
    GitCliService.cs     ← toda la lógica git (Process.Start)
    RepoScannerService.cs ← IHostedService, loop de 60s + trigger manual
    RepoStateStore.cs    ← Singleton, estado en memoria + evento OnStateChanged
  Models/
    RepoInfo.cs          ← datos de un repo
    RepoGroup.cs         ← agrupación lógica
    ScanResult.cs        ← resultado de un scan individual
  Pages/
    Home.razor           ← página principal
  appsettings.json
  Program.cs
```

### Data flow

```
RepoScannerService (60s loop o trigger manual)
  → GitCliService.ScanAsync(repo)
    → Process.Start("git status"), ("git branch"), ("git log"), ("git rev-list")
  → RepoStateStore.Update(repoInfoList)
    → dispara OnStateChanged
      → UI suscrita re-renderiza automáticamente (Blazor Server + SignalR built-in)
```

Botón Refresh → llama a `RepoScannerService.TriggerScanAsync()` → mismo flujo.

---

## Models

### RepoInfo

```csharp
public record RepoInfo
{
    public string Name { get; init; }
    public string Path { get; init; }
    public string Group { get; init; }        // "Services", "Libraries", etc.
    public string Branch { get; init; }
    public bool IsDirty { get; init; }
    public int DirtyFileCount { get; init; }
    public int AheadCount { get; init; }
    public int BehindCount { get; init; }
    public string LastCommitMessage { get; init; }
    public DateTime LastScanned { get; init; }
    public bool IsGitRepo { get; init; }      // false si no tiene .git/
}
```

### RepoStateStore

```csharp
public class RepoStateStore
{
    public IReadOnlyList<RepoInfo> Repos { get; }
    public bool IsScanning { get; }
    public DateTime LastScanCompleted { get; }
    public event Action OnStateChanged;
    public void Update(IReadOnlyList<RepoInfo> repos);
}
```

---

## Services

### GitCliService

Ejecuta comandos `git` en un directorio dado. Parsea la salida de texto.

| Método | Comando git | Propósito |
|--------|------------|-----------|
| `GetStatusAsync(path)` | `git status --porcelain` | Detectar archivos modificados |
| `GetBranchAsync(path)` | `git branch --show-current` | Rama actual |
| `GetAheadBehindAsync(path)` | `git rev-list --count HEAD...@{u}` | Commits ahead/behind |
| `GetLastCommitAsync(path)` | `git log -1 --pretty=%s` | Último mensaje de commit |
| `PullAsync(path)` | `git pull` | Pull con fast-forward |
| `FetchAsync(path)` | `git fetch` | Fetch sin merge |
| `CheckoutAsync(path, branch)` | `git checkout [-b] branch` | Checkout, crea si no existe |

Todos los métodos son `async`, usan `CancellationToken`, y devuelven `Result<T>` o lanzan excepción tipada para errores de git.

### RepoScannerService (IHostedService)

- Al arrancar: escanea todos los repos una vez
- Loop: espera 60s (configurable), escanea en paralelo con `Parallel.ForEachAsync`
- Expone `TriggerScanAsync()` para refresh manual
- Durante el scan: `RepoStateStore.IsScanning = true` → la UI muestra spinner

### RepoStateStore (Singleton)

- Mantiene la lista de `RepoInfo` en memoria
- Expone `OnStateChanged` para que los componentes Blazor se suscriban
- Thread-safe (lock al escribir, lista inmutable al leer)

---

## UI

### Home.razor

Página principal. Suscribe a `RepoStateStore.OnStateChanged` → llama `StateHasChanged()`.

Layout:
```
[TopBar: título, path raíz, último scan, botón Refresh]
[FilterBar: búsqueda por nombre, dropdown grupo, dropdown estado]
[BulkActions: checkAll, Pull seleccionados, Checkout en seleccionados]
[RepoList: grupos colapsables → filas RepoRow]
[StatusBar: N seleccionados, X dirty, Y behind, próximo scan en Zs]
```

### Grupos

Repos agrupados por prefijo del nombre:
| Grupo | Patrón |
|-------|--------|
| Services | `Woffu.Services.*` |
| Libraries | `Woffu.Library.*` |
| Functions | `Woffu.Functions.*` |
| Frontend | `Woffu.Frontend.*` |
| DevOps | `Devops.*` |
| Tools | `Woffu.Tools.*`, `Woffu.Utils.*` |
| Other | resto |

Un grupo se colapsa automáticamente si todos sus repos están clean y en master. El usuario puede expandir/colapsar manualmente.

### RepoRow

Columnas: checkbox · nombre · rama (color: amarillo=feature, verde=master) · estado (dirty/clean/behind) · ahead↑ behind↓ · último commit

Acciones por fila (hover): `Pull`, `Checkout`, `Abrir en terminal` (`wt -d {path}` o `explorer {path}`)

---

## Configuration (appsettings.json)

```json
{
  "RepoOrchestra": {
    "RootPath": "C:\\_O",
    "ScanIntervalSeconds": 60,
    "ParallelScanDegree": 8,
    "ExcludedRepos": [],
    "Port": 5200
  }
}
```

---

## Error handling

- Si un repo no tiene `.git/` → `IsGitRepo = false`, se muestra con badge "No git" pero no falla el scan
- Si `git` falla en un repo (ej. repo corrupto) → se captura la excepción, `RepoInfo` tiene `Error` con el mensaje, UI muestra badge rojo con tooltip
- Si `git` no está en PATH → error fatal al arrancar con mensaje claro
- Operaciones bulk: si falla en un repo, continúa con el resto y muestra resumen al final (X OK, Y errores)

---

## Testing

- **Unit tests** (`Woffu.Tools.RepoOrchestra.U.Tests`): `GitCliService` con repos git reales en temp dir, `RepoStateStore` thread-safety, parsing de salida git
- **No integration tests** para MVP (la app en sí es la integración)
- Convención: clase `When_<condición>`, método `Then_<resultado>` por archivo

---

## Out of scope / Future

- SQLite para persistir favoritos, notas por repo, grupos personalizados
- Integración ADO: ver tareas vinculadas a la rama activa
- Integración Dev Hub: tokens, secrets, estado de servicios
- Notificaciones desktop cuando un repo queda behind
- Dark/light theme toggle (MudBlazor lo soporta nativamente)
