# Plan: Limpiar Settings — eliminar Add/Import duplicados

**Goal:** Quitar el bloque "Add repo + Import root" de la página Settings, que duplica la funcionalidad del folder picker de Home y además está roto (`ImportRoot` llama a `RootPath` que ahora es `""`). Settings queda como vista de gestión pura: ver catálogo, borrar repos, ver config, gestionar group rules.

**Repo:** `D:\claude\DevHub`
**Stack:** .NET 10, Blazor Server, MudBlazor 9.3.0

---

## Archivos a tocar

| Archivo | Acción |
|---|---|
| `src/DevHub/Components/Pages/Settings.razor` | Modificar — borrar el bloque de texto + botones |
| `src/DevHub/Components/Pages/Settings.razor.cs` | Modificar — borrar campos y métodos relacionados |

---

## Task 1: Limpiar Settings.razor

**Archivo:** `src/DevHub/Components/Pages/Settings.razor`

Buscar y eliminar completamente el bloque que contiene el `MudTextField` de "Repo path" y los botones `ADD REPO` / `IMPORT ROOT`. Es el bloque dentro del primer `<MudPaper Class="pa-4"...>` bajo `Catalog`.

El bloque a eliminar es este (líneas ~27–55):

```razor
<MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2" Wrap="Wrap.Wrap">
    <MudTextField T="string"
                  Value="_repoPathInput"
                  ValueChanged="value => _repoPathInput = value"
                  Label="Repo path"
                  Placeholder="D:\\repos\\MyRepo"
                  Variant="Variant.Outlined"
                  Margin="Margin.Dense"
                  Style="width: 480px; max-width: 100%;" />
    <MudButton Variant="Variant.Filled"
               Color="Color.Primary"
               StartIcon="@Icons.Material.Filled.Add"
               Disabled="@_catalogBusy"
               OnClick="AddRepoAsync">
        Add repo
    </MudButton>
    <MudButton Variant="Variant.Outlined"
               Color="Color.Secondary"
               StartIcon="@Icons.Material.Filled.FolderCopy"
               Disabled="@_catalogBusy"
               OnClick="ImportRootAsync">
        Import root
    </MudButton>
    <MudIconButton Icon="@Icons.Material.Filled.Refresh"
                   Color="Color.Default"
                   Disabled="@_catalogBusy"
                   aria-label="Reload catalog"
                   OnClick="LoadCatalogAsync" />
</MudStack>
```

Reemplazarlo por solo el botón refresh:

```razor
<MudStack Row="true" JustifyContent="JustifyContent.FlexEnd">
    <MudIconButton Icon="@Icons.Material.Filled.Refresh"
                   Color="Color.Default"
                   aria-label="Reload catalog"
                   OnClick="LoadCatalogAsync" />
</MudStack>
```

> Verificar que `@inject IOptions<DevHubOptions> DevHubOptions` siga siendo necesario en el resto de la página (se usa en la sección General para mostrar `RootPath`, `FetchIntervalMinutes`, etc.). Si todavía se usa, dejarlo. Si no, eliminarlo junto con `@using Microsoft.Extensions.Options`.

---

## Task 2: Limpiar Settings.razor.cs

**Archivo:** `src/DevHub/Components/Pages/Settings.razor.cs`

- [ ] Eliminar el campo `_repoPathInput`:
```csharp
// BORRAR:
private string _repoPathInput = string.Empty;
```

- [ ] Eliminar el método `AddRepoAsync` completo:
```csharp
// BORRAR:
private async Task AddRepoAsync()
{
    if (string.IsNullOrWhiteSpace(_repoPathInput)) { return; }
    _catalogBusy = true;
    try
    {
        await RepoCatalog.AddAsync(_repoPathInput, CancellationToken.None);
        _repoPathInput = string.Empty;
        await LoadCatalogAsync();
        Snackbar.Add("Repo added to catalog.", Severity.Success);
    }
    catch (Exception ex) { Snackbar.Add(ex.Message, Severity.Error); }
    finally { _catalogBusy = false; }
}
```

- [ ] Eliminar el método `ImportRootAsync` completo:
```csharp
// BORRAR:
private async Task ImportRootAsync()
{
    _catalogBusy = true;
    try
    {
        var imported = await RepoCatalog.ImportFromRootAsync(DevHubOptions.Value.RootPath, CancellationToken.None);
        await LoadCatalogAsync();
        Snackbar.Add(...);
    }
    catch (Exception ex) { Snackbar.Add(ex.Message, Severity.Error); }
    finally { _catalogBusy = false; }
}
```

- [ ] Verificar si `_catalogBusy` quedó sin referencias tras los cambios. `LoadCatalogAsync` y `LoadGroupRulesAsync` no lo usan. Si ningún método restante lo referencia, eliminarlo:
```csharp
// BORRAR si ya no se usa:
private bool _catalogBusy;
```

---

## Task 3: Verificar compilación y tests

```bash
cd D:\claude\DevHub
dotnet build src/DevHub/DevHub.csproj --no-restore -v q
dotnet test tests/DevHub.U.Tests/DevHub.U.Tests.csproj --no-restore -v q
```

Esperado: 0 errores de compilación, todos los tests en verde.

---

## Task 4: Commit

```bash
git add src/DevHub/Components/Pages/Settings.razor src/DevHub/Components/Pages/Settings.razor.cs
git commit -m "refactor: remove duplicate add/import from Settings, catalog is read-only management view"
```

---

## Resultado esperado

Settings pasa de tener "añadir repos" duplicado a ser exclusivamente una vista de gestión:

- **Catalog:** tabla con todos los repos, botón borrar individual, botón refresh
- **General:** info de config (DB path, fetch interval, parallel degree, default group)
- **Group Rules:** CRUD de reglas de agrupación

La operativa de agregar/importar repos queda solo en Home, con el folder picker.
