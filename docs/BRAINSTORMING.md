# Brainstorming — Repo Orchestra

Sesión de ideación para crear una app local útil para el día a día en Woffu (~77 repos .NET).

---

## Idea elegida: Repo Orchestra

**Panel visual de estado git para todos los repos de Woffu.**

App local **Blazor Server (.NET 10)** que muestra en tiempo real el estado git de los ~77 repos clonados en `C:\_O`.

### Por qué esta idea

Con 77 repos en el orquestador es difícil saber de un vistazo:
- Cuántos repos tienes en ramas feature activas
- Cuáles están por detrás del remoto (behind)
- Cuáles tienen cambios sin commitear (dirty)
- Dónde estás trabajando ahora mismo

### Stack técnico

| Capa | Tecnología |
|------|-----------|
| Framework | .NET 10, Blazor Server |
| UI | MudBlazor v9 (MIT), dark theme |
| Git | git CLI via `Process.Start` (sin LibGit2Sharp) |
| Background | `IHostedService` (BackgroundService) |
| Estado | Singleton `RepoStateStore` + eventos |
| Tests | xUnit + FluentAssertions |

### Funcionalidades implementadas (MVP)

- **Scan automático** cada 60s + botón de refresh manual
- **Grupos colapsables**: DevOps, Frontend, Functions, Libraries, Services, Tools, Other
  - Auto-colapsa grupos donde todos están clean + en master
  - Recuerda qué grupos el usuario expandió manualmente
- **Filtros**: por nombre, grupo y estado (dirty / clean / behind / feature branch)
- **Selección múltiple** + operaciones bulk: `git pull --ff-only` y `git checkout <rama>`
- **Columnas**: Repositorio · Rama (chip coloreado) · Estado · Commits (↑↓) · Último commit
- **Abrir en terminal** (Windows Terminal) con un click
- **Status bar**: repos seleccionados, dirty count, behind count, countdown al próximo scan

### Otras ideas descartadas en el brainstorming

| Idea | Motivo descarte |
|------|----------------|
| Dashboard de pipelines Azure DevOps | Ya existe en el portal ADO |
| Generador de changelogs entre servicios | Complejidad alta, valor bajo frecuencia |
| Buscador global de código cross-repo | VS Code ya lo hace bien con multi-root workspace |
| Auto-updater de NuGets | Riesgo de romper builds, mejor hacerlo manual |

---

## URL local

`http://localhost:5200`

## Ubicación del proyecto

`C:\woffu-orchestra\Woffu.Tools.RepoOrchestra`
