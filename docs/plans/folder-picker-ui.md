# Plan: Folder Picker para Agregar Repo / Importar Root

## Contexto del proyecto

- **Stack:** .NET 10 · Blazor Server · `@rendermode InteractiveServer`
- **UI:** MudBlazor 9.3.0 · dark theme (primary `#c084fc`, background `#0f0f14`)
- **La app corre como Windows Service en `localhost:5200`** — el filesystem del servidor ES el del usuario
- **Convenciones obligatorias:**
  - Primary constructors siempre (nunca constructor con cuerpo explícito)
  - Servicios como Singleton
  - Namespaces file-scoped
  - MudBlazor para toda la UI (nunca HTML crudo)
  - Sin comentarios salvo que el WHY sea no obvio

## Archivos relevantes

| Archivo | Descripción |
|---------|-------------|
| `src/DevHub/Components/Pages/Home.razor` | Página principal — contiene los inputs a reemplazar |
| `src/DevHub/Components/Pages/Home.razor.cs` | Code-behind de Home |
| `src/DevHub/Services/FolderPickerService.cs` | **CREAR** — lista directorios del servidor |
| `src/DevHub/Components/FolderPickerDialog.razor` | **CREAR** — dialog con navegador de carpetas |
| `src/DevHub/Components/FolderPickerDialog.razor.cs` | **CREAR** — code-behind del dialog |
| `src/DevHub/Program.cs` | Registrar `FolderPickerService` |

---

## Paso 1 — Crear `FolderPickerService.cs`

**Ruta:** `src/DevHub/Services/FolderPickerService.cs`

```csharp
namespace DevHub.Services;

public class FolderPickerService
{
    public IReadOnlyList<string> GetDrives() =>
        DriveInfo.GetDrives()
                 .Where(d => d.IsReady)
                 .Select(d => d.RootDirectory.FullName)
                 .ToList();

    public IReadOnlyList<string> GetSubDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path)
                            .OrderBy(p => p)
                            .ToList();
        }
        catch
        {
            return [];
        }
    }

    public string? GetParent(string path) =>
        Directory.GetParent(path)?.FullName;
}
```

Registrar en `Program.cs` junto a los otros singletons:
```csharp
builder.Services.AddSingleton<FolderPickerService>();
```

---

## Paso 2 — Crear `FolderPickerDialog.razor`

**Ruta:** `src/DevHub/Components/FolderPickerDialog.razor`

```razor
@using DevHub.Services
@inject FolderPickerService FolderPicker

<MudDialog>
    <TitleContent>
        <MudText Typo="Typo.h6">
            <MudIcon Icon="@Icons.Material.Filled.FolderOpen" Class="mr-2" />
            Seleccionar carpeta
        </MudText>
    </TitleContent>
    <DialogContent>
        <MudStack Spacing="1">
            <!-- Breadcrumb del path actual -->
            <MudPaper Class="pa-2" Elevation="0" Style="background: var(--mud-palette-background)">
                <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="1" Wrap="Wrap.Wrap">
                    @foreach (var (segment, path) in GetBreadcrumbs())
                    {
                        <MudLink Color="Color.Primary" OnClick="() => NavigateTo(path)">@segment</MudLink>
                        <MudText Typo="Typo.caption">/</MudText>
                    }
                </MudStack>
            </MudPaper>

            <!-- Botón subir un nivel -->
            @if (FolderPicker.GetParent(_currentPath) is { } parent)
            {
                <MudListItem T="string" Icon="@Icons.Material.Filled.ArrowUpward"
                             OnClick="() => NavigateTo(parent)">
                    ..
                </MudListItem>
            }

            <!-- Lista de subdirectorios -->
            <MudList T="string" Dense="true" Style="max-height: 350px; overflow-y: auto;">
                @foreach (var dir in _subDirs)
                {
                    <MudListItem T="string"
                                 Icon="@Icons.Material.Filled.Folder"
                                 IconColor="Color.Warning"
                                 OnClick="() => NavigateTo(dir)">
                        @System.IO.Path.GetFileName(dir)
                    </MudListItem>
                }
                @if (_subDirs.Count == 0)
                {
                    <MudListItem T="string" Disabled="true">
                        <MudText Typo="Typo.caption" Color="Color.Default">(carpeta vacía)</MudText>
                    </MudListItem>
                }
            </MudList>

            <!-- Path seleccionado -->
            <MudPaper Class="pa-2" Elevation="0" Style="background: var(--mud-palette-surface)">
                <MudText Typo="Typo.caption" Color="Color.Primary">Seleccionada:</MudText>
                <MudText Typo="Typo.body2">@_currentPath</MudText>
            </MudPaper>
        </MudStack>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancelar</MudButton>
        <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="Confirm">
            Seleccionar esta carpeta
        </MudButton>
    </DialogActions>
</MudDialog>
```

---

## Paso 3 — Crear `FolderPickerDialog.razor.cs`

**Ruta:** `src/DevHub/Components/FolderPickerDialog.razor.cs`

```csharp
using DevHub.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DevHub.Components;

public partial class FolderPickerDialog
{
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public string InitialPath { get; set; } =
        OperatingSystem.IsWindows() ? @"C:\" : "/home";

    [Inject] FolderPickerService FolderPicker { get; set; } = default!;

    private string _currentPath = string.Empty;
    private IReadOnlyList<string> _subDirs = [];

    protected override void OnInitialized()
    {
        _currentPath = InitialPath;
        Refresh();
    }

    private void NavigateTo(string path)
    {
        _currentPath = path;
        Refresh();
    }

    private void Refresh() =>
        _subDirs = FolderPicker.GetSubDirectories(_currentPath);

    private IEnumerable<(string Segment, string Path)> GetBreadcrumbs()
    {
        var parts = _currentPath.Split(
            [System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        var accumulated = OperatingSystem.IsWindows() ? string.Empty : "/";
        foreach (var part in parts)
        {
            accumulated = System.IO.Path.Combine(accumulated, part);
            yield return (part, accumulated);
        }
    }

    private void Confirm() => MudDialog.Close(DialogResult.Ok(_currentPath));
    private void Cancel() => MudDialog.Cancel();
}
```

---

## Paso 4 — Actualizar `Home.razor`

### Quitar estos elementos (la sección entera con los MudTextField de ruta):

```razor
<!-- ELIMINAR todo esto: -->
<MudTextField T="string" Value="_repoPathInput" ... Label="Agregar repo por ruta" ... />
<MudButton ... OnClick="AddRepoAsync">Agregar repo</MudButton>
<MudTextField T="string" @bind-Value="_importRootPath" Label="Importar desde root" ... />
<MudButton ... OnClick="ImportRootAsync">Importar</MudButton>
```

### Reemplazar por dos botones uno al lado del otro:

```razor
<MudPaper Class="pa-3 mb-2" Elevation="0" Style="background: var(--mud-palette-surface)">
    <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
        <MudButton Variant="Variant.Filled"
                   Color="Color.Primary"
                   StartIcon="@Icons.Material.Filled.FolderOpen"
                   Disabled="@_catalogBusy"
                   OnClick="PickAndAddRepoAsync">
            Agregar repo
        </MudButton>
        <MudButton Variant="Variant.Outlined"
                   Color="Color.Secondary"
                   StartIcon="@Icons.Material.Filled.FolderCopy"
                   Disabled="@_catalogBusy"
                   OnClick="PickAndImportRootAsync">
            Importar desde root
        </MudButton>
    </MudStack>
</MudPaper>
```

---

## Paso 5 — Actualizar `Home.razor.cs`

### Agregar inject:
```csharp
[Inject] IDialogService DialogService { get; set; } = default!;
```

### Eliminar campos que ya no se usan:
```csharp
// ELIMINAR:
private string _repoPathInput = string.Empty;
private string _importRootPath = string.Empty;
```

### Reemplazar métodos `AddRepoAsync` e `ImportRootAsync` por:

```csharp
private async Task PickAndAddRepoAsync()
{
    var path = await OpenFolderPickerAsync();
    if (path is null) return;
    _catalogBusy = true;
    try
    {
        await RepoCatalog.AddRepoAsync(path);
        Scanner.RequestManualRefresh();
    }
    finally
    {
        _catalogBusy = false;
    }
}

private async Task PickAndImportRootAsync()
{
    var path = await OpenFolderPickerAsync(DevHubOptions.Value.RootPath);
    if (path is null) return;
    _catalogBusy = true;
    try
    {
        await RepoCatalog.ImportFromRootAsync(path);
        Scanner.RequestManualRefresh();
    }
    finally
    {
        _catalogBusy = false;
    }
}

private async Task<string?> OpenFolderPickerAsync(string? initialPath = null)
{
    var parameters = new DialogParameters<FolderPickerDialog>
    {
        { x => x.InitialPath, initialPath ?? DevHubOptions.Value.RootPath }
    };
    var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
    var dialog = await DialogService.ShowAsync<FolderPickerDialog>("Seleccionar carpeta", parameters, options);
    var result = await dialog.Result;
    return result is { Canceled: false, Data: string path } ? path : null;
}
```

---

## Tests a agregar

**Ruta:** `tests/DevHub.U.Tests/Services/When_FolderPickerService_is_used/`

```
Then_GetSubDirectories_returns_sorted_dirs.cs
Then_GetSubDirectories_returns_empty_on_error.cs
Then_GetParent_returns_null_at_root.cs
```

Patrón obligatorio: clase `When_FolderPickerService_is_used`, un `[Fact] public async Task Execute()` por archivo.

---

## Notas finales

- El dialog usa `IDialogService` de MudBlazor — ya está registrado via `AddMudServices()` en `Program.cs`
- `FolderPickerDialog` debe estar en el namespace `DevHub.Components`
- Agregar `@using DevHub.Components` en `_Imports.razor` si no está ya
- No crear documentación adicional, no agregar comentarios explicativos
