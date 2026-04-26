# GroupRule ABM — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mover los `GroupRule` de `appsettings.json` a la base de datos SQL con un CRUD completo en la página `/settings`.

**Architecture:** `GroupRule` pasa a ser una entidad EF con `int Id`, `int Order` (para prioridad de matching), `List<string> Prefixes` almacenado como JSON en una columna `TEXT`. `IGroupRuleService` abstrae el acceso. `RepoScannerService` carga las reglas desde la DB en cada scan. En el primer arranque, si la tabla está vacía, se hace seed desde `DevHubOptions.Groups` (que luego se elimina de `appsettings.json`).

**Tech Stack:** EF Core 9, Blazor Server, MudBlazor 9.3, xUnit + Moq + Shouldly

---

## File Map

| Acción | Archivo |
|--------|---------|
| Modify | `src/DevHub/Models/GroupRule.cs` |
| Modify | `src/DevHub/Data/ApplicationDbContext.cs` |
| Create | `src/DevHub/Services/IGroupRuleService.cs` |
| Create | `src/DevHub/Services/GroupRuleService.cs` |
| Modify | `src/DevHub/Services/RepoScannerService.cs` |
| Modify | `src/DevHub/Services/DevHubOptions.cs` |
| Modify | `src/DevHub/Components/Pages/Settings.razor` |
| Modify | `src/DevHub/Program.cs` |
| Modify | `src/DevHub/appsettings.json` |
| Create | `tests/DevHub.U.Tests/Services/When_GroupRuleService_is_used/Then_add_persists_rule.cs` |
| Create | `tests/DevHub.U.Tests/Services/When_GroupRuleService_is_used/Then_delete_removes_rule.cs` |
| Create | `tests/DevHub.U.Tests/Services/When_GroupRuleService_is_used/Then_update_persists_changes.cs` |
| Create | `tests/DevHub.U.Tests/Services/When_GroupRuleService_is_used/DbFixture.cs` |

---

## Task 1: Convertir `GroupRule` en entidad EF con `Id` y `Order`

**Files:**
- Modify: `src/DevHub/Models/GroupRule.cs`
- Modify: `src/DevHub/Data/ApplicationDbContext.cs`

### Contexto
`GroupRule` es actualmente un POCO de config sin PK. Pasa a ser una entidad EF con:
- `int Id` — surrogate PK
- `int Order` — controla el orden de evaluación de prefijos (menor = mayor prioridad)
- `List<string> Prefixes` — se almacena como JSON en columna `TEXT`, compatible con SQLite y SQL Server

- [ ] **Step 1: Leer el archivo actual**

```bash
cat src/DevHub/Models/GroupRule.cs
cat src/DevHub/Data/ApplicationDbContext.cs
```

- [ ] **Step 2: Reemplazar `GroupRule.cs`**

```csharp
namespace DevHub.Models;

public class GroupRule
{
    public int Id { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "default";
    public List<string> Prefixes { get; set; } = [];
}
```

- [ ] **Step 3: Actualizar `ApplicationDbContext.cs` — agregar `DbSet<GroupRule>` y config FluentAPI**

```csharp
using System.Text.Json;
using DevHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHub.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<RepoCatalogEntry> RepoCatalogEntries => Set<RepoCatalogEntry>();
    public DbSet<GroupRule> GroupRules => Set<GroupRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RepoCatalogEntry>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Path).HasMaxLength(1024).IsRequired();
            e.HasIndex(r => r.Path).IsUnique();
        });

        modelBuilder.Entity<GroupRule>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).HasMaxLength(256).IsRequired();
            e.Property(r => r.Color).HasMaxLength(64).IsRequired();
            e.Property(r => r.Prefixes)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? [])
                .HasColumnType("TEXT")
                .IsRequired();
        });
    }
}
```

- [ ] **Step 4: Buildear**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores.

- [ ] **Step 5: Commit**

```bash
git add src/DevHub/Models/GroupRule.cs src/DevHub/Data/ApplicationDbContext.cs
git commit -m "feat: add GroupRule as EF entity with Id, Order and JSON Prefixes"
```

---

## Task 2: Crear `IGroupRuleService` y `GroupRuleService`

**Files:**
- Create: `src/DevHub/Services/IGroupRuleService.cs`
- Create: `src/DevHub/Services/GroupRuleService.cs`

### Contexto
`IGroupRuleService` abstrae el CRUD de `GroupRule`. `GroupRuleService` implementa con `IDbContextFactory<ApplicationDbContext>` (singleton-safe — nunca inyectar `DbContext` directamente en un singleton).

- [ ] **Step 1: Crear `src/DevHub/Services/IGroupRuleService.cs`**

```csharp
using DevHub.Models;

namespace DevHub.Services;

public interface IGroupRuleService
{
    Task<IReadOnlyList<GroupRule>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(GroupRule rule, CancellationToken ct = default);
    Task UpdateAsync(GroupRule rule, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task SeedFromConfigAsync(IEnumerable<GroupRule> configRules, CancellationToken ct = default);
}
```

- [ ] **Step 2: Crear `src/DevHub/Services/GroupRuleService.cs`**

```csharp
using DevHub.Data;
using DevHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHub.Services;

public class GroupRuleService(IDbContextFactory<ApplicationDbContext> dbFactory) : IGroupRuleService
{
    public async Task<IReadOnlyList<GroupRule>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.GroupRules
            .AsNoTracking()
            .OrderBy(r => r.Order)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(GroupRule rule, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var maxOrder = await db.GroupRules.AnyAsync(ct)
            ? await db.GroupRules.MaxAsync(r => r.Order, ct)
            : -1;
        rule.Id = 0; // evitar conflictos de PK
        rule.Order = maxOrder + 1;
        db.GroupRules.Add(rule);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(GroupRule rule, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.GroupRules.Update(rule);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.GroupRules.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
    }

    // Llamado en startup: si la tabla está vacía, carga las reglas del config
    public async Task SeedFromConfigAsync(IEnumerable<GroupRule> configRules, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await db.GroupRules.AnyAsync(ct))
            return;

        var order = 0;
        foreach (var rule in configRules)
        {
            rule.Id = 0;
            rule.Order = order++;
            db.GroupRules.Add(rule);
        }
        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: Buildear**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores.

- [ ] **Step 4: Commit**

```bash
git add src/DevHub/Services/IGroupRuleService.cs src/DevHub/Services/GroupRuleService.cs
git commit -m "feat: add IGroupRuleService and GroupRuleService for GroupRule CRUD"
```

---

## Task 3: Tests para `GroupRuleService`

**Files:**
- Create: `tests/DevHub.U.Tests/Services/When_GroupRuleService_is_used/DbFixture.cs`
- Create: `tests/DevHub.U.Tests/Services/When_GroupRuleService_is_used/Then_add_persists_rule.cs`
- Create: `tests/DevHub.U.Tests/Services/When_GroupRuleService_is_used/Then_delete_removes_rule.cs`
- Create: `tests/DevHub.U.Tests/Services/When_GroupRuleService_is_used/Then_update_persists_changes.cs`

- [ ] **Step 1: Crear `DbFixture.cs`**

```csharp
using DevHub.Data;
using DevHub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DevHub.U.Tests.Services.When_GroupRuleService_is_used;

internal sealed class DbFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    public readonly IDbContextFactory<ApplicationDbContext> Factory;

    public DbFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        Factory = new TestDbContextFactory(options);
    }

    public GroupRuleService CreateSut() => new(Factory);

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
}
```

- [ ] **Step 2: Crear `Then_add_persists_rule.cs`**

```csharp
using DevHub.Models;
using Shouldly;

namespace DevHub.U.Tests.Services.When_GroupRuleService_is_used;

public class Then_add_persists_rule
{
    [Fact]
    public async Task Then_add_persists_rule()
    {
        using var fixture = new DbFixture();
        var sut = fixture.CreateSut();

        var rule = new GroupRule { Name = "Core", Color = "primary", Prefixes = ["Core.", "Api."] };
        await sut.AddAsync(rule);

        var all = await sut.GetAllAsync();
        all.Count.ShouldBe(1);
        all[0].Name.ShouldBe("Core");
        all[0].Prefixes.ShouldBe(["Core.", "Api."]);
        all[0].Order.ShouldBe(0);
    }
}
```

- [ ] **Step 3: Crear `Then_delete_removes_rule.cs`**

```csharp
using DevHub.Models;
using Shouldly;

namespace DevHub.U.Tests.Services.When_GroupRuleService_is_used;

public class Then_delete_removes_rule
{
    [Fact]
    public async Task Then_delete_removes_rule()
    {
        using var fixture = new DbFixture();
        var sut = fixture.CreateSut();

        var rule = new GroupRule { Name = "ToDelete", Color = "error", Prefixes = ["Del."] };
        await sut.AddAsync(rule);
        var added = (await sut.GetAllAsync())[0];

        await sut.DeleteAsync(added.Id);

        var all = await sut.GetAllAsync();
        all.ShouldBeEmpty();
    }
}
```

- [ ] **Step 4: Crear `Then_update_persists_changes.cs`**

```csharp
using DevHub.Models;
using Shouldly;

namespace DevHub.U.Tests.Services.When_GroupRuleService_is_used;

public class Then_update_persists_changes
{
    [Fact]
    public async Task Then_update_persists_changes()
    {
        using var fixture = new DbFixture();
        var sut = fixture.CreateSut();

        var rule = new GroupRule { Name = "Original", Color = "primary", Prefixes = ["Orig."] };
        await sut.AddAsync(rule);
        var added = (await sut.GetAllAsync())[0];

        added.Name = "Updated";
        added.Prefixes = ["Orig.", "New."];
        await sut.UpdateAsync(added);

        var updated = (await sut.GetAllAsync())[0];
        updated.Name.ShouldBe("Updated");
        updated.Prefixes.Count.ShouldBe(2);
    }
}
```

- [ ] **Step 5: Ejecutar los tests**

```bash
dotnet test tests/DevHub.U.Tests --filter "When_GroupRuleService_is_used"
```

Expected: 3 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add tests/DevHub.U.Tests/Services/When_GroupRuleService_is_used/
git commit -m "test: add GroupRuleService CRUD tests"
```

---

## Task 4: Registrar en DI y hacer seed en startup

**Files:**
- Modify: `src/DevHub/Program.cs`
- Modify: `src/DevHub/Services/DevHubOptions.cs`

### Contexto
`GroupRuleService` se registra como `AddScoped` (no singleton — aunque usa factory, no hay razón para singleton). En startup se llama `SeedFromConfigAsync` con las reglas del config, que no hace nada si la tabla ya tiene datos. Después de este task, `DevHubOptions.Groups` se puede dejar como lista vacía por defecto — el config de appsettings solo sirve para el seed inicial.

- [ ] **Step 1: Leer `Program.cs`**

```bash
cat src/DevHub/Program.cs
```

- [ ] **Step 2: Registrar `IGroupRuleService`**

Agregar junto a los otros registros de servicios:

```csharp
builder.Services.AddScoped<IGroupRuleService, GroupRuleService>();
```

- [ ] **Step 3: Ejecutar el seed en startup, después de `EnsureCreatedAsync`**

Localizar donde se llama `EnsureInitializedAsync` en startup (probablemente en un `IHostedService` o al inicio del pipeline). Agregar el seed inmediatamente después:

```csharp
// En el bloque de startup, después de app.Build() y antes de app.Run()
using (var scope = app.Services.CreateScope())
{
    var catalog = scope.ServiceProvider.GetRequiredService<IRepoCatalogService>();
    await catalog.EnsureInitializedAsync();

    // Seed GroupRules desde config si la tabla está vacía
    var groupRuleService = scope.ServiceProvider.GetRequiredService<IGroupRuleService>();
    var devHubOptions = scope.ServiceProvider.GetRequiredService<IOptions<DevHubOptions>>().Value;
    await groupRuleService.SeedFromConfigAsync(devHubOptions.Groups);
}
```

Si `EnsureInitializedAsync` está en otro lugar (p.ej. un `BackgroundService`), agregar el seed allí mismo, justo después de `EnsureCreatedAsync`.

- [ ] **Step 4: Buildear**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores.

- [ ] **Step 5: Ejecutar todos los tests**

```bash
dotnet test tests/DevHub.U.Tests
```

Expected: todos pasan.

- [ ] **Step 6: Commit**

```bash
git add src/DevHub/Program.cs
git commit -m "feat: register GroupRuleService and seed GroupRules from config on startup"
```

---

## Task 5: Conectar `RepoScannerService` a `IGroupRuleService`

**Files:**
- Modify: `src/DevHub/Services/RepoScannerService.cs`

### Contexto
`RepoScannerService` actualmente lee `_options.Groups` para resolver grupos. Pasa a leer las reglas de la DB via `IGroupRuleService`. Como `RepoScannerService` es un `BackgroundService` (singleton), no puede inyectar `IGroupRuleService` directamente si es Scoped. Solución: inyectar `IServiceScopeFactory` y crear un scope por ciclo de scan.

- [ ] **Step 1: Leer `RepoScannerService.cs` para ver el constructor actual**

```bash
cat src/DevHub/Services/RepoScannerService.cs
```

- [ ] **Step 2: Cambiar el constructor para inyectar `IServiceScopeFactory`**

```csharp
// ANTES
public RepoScannerService(
    IGitService gitService,
    IRepoCatalogService repoCatalog,
    RepoStateStore store,
    IOptions<DevHubOptions> options,
    ILogger<RepoScannerService> logger)

// DESPUÉS
public RepoScannerService(
    IGitService gitService,
    IRepoCatalogService repoCatalog,
    RepoStateStore store,
    IOptions<DevHubOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<RepoScannerService> logger)
```

Guardar `scopeFactory` en un campo `private readonly IServiceScopeFactory _scopeFactory`.

- [ ] **Step 3: En `TriggerScanAsync`, cargar las reglas desde la DB al inicio de cada ciclo**

Localizar el inicio del método donde se resuelven los grupos. Agregar:

```csharp
private async Task TriggerScanAsync(CancellationToken ct)
{
    // Cargar reglas desde DB en cada ciclo — refleja cambios del usuario sin reiniciar
    IReadOnlyList<GroupRule> groupRules;
    await using (var scope = _scopeFactory.CreateAsyncScope())
    {
        var groupRuleService = scope.ServiceProvider.GetRequiredService<IGroupRuleService>();
        groupRules = await groupRuleService.GetAllAsync(ct);
    }

    // Usar groupRules en lugar de _options.Groups para RepoGroupResolver.Resolve(...)
    // Resto del método igual, reemplazar _options.Groups → groupRules
    // ...
}
```

Buscar todos los usos de `_options.Groups` dentro de `TriggerScanAsync` y reemplazar con `groupRules`.

- [ ] **Step 4: Buildear**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores.

- [ ] **Step 5: Ejecutar todos los tests**

```bash
dotnet test tests/DevHub.U.Tests
```

Si algún test de `RepoScannerService` pasa `IOptions<DevHubOptions>` con `Groups` poblados, actualizar el mock para ya no depender de `Groups` (el scanner ahora los lee de la DB).

Expected: todos pasan.

- [ ] **Step 6: Commit**

```bash
git add src/DevHub/Services/RepoScannerService.cs
git commit -m "feat: RepoScannerService loads GroupRules from DB per scan cycle"
```

---

## Task 6: UI — sección GroupRules ABM en `/settings`

**Files:**
- Modify: `src/DevHub/Components/Pages/Settings.razor`

### Contexto
Agregar una sección en `/settings` debajo de la sección "Catalog" con:
- Tabla de reglas actuales: Order, Name (chip con color), Color, Prefixes (separados por coma), botón Delete
- Formulario inline para agregar nueva regla: campos Name, Color (select), Prefixes (input), botón Add
- Botón Edit por fila que abre los campos en modo edición inline (sin dialog)

El componente inyecta `IGroupRuleService`. Los colores disponibles para el select son los que soporta `ColorHelper.FromGroupColor`: `primary`, `secondary`, `tertiary`, `info`, `success`, `warning`, `error`, `default`.

- [ ] **Step 1: Leer `Settings.razor` completo**

```bash
cat src/DevHub/Components/Pages/Settings.razor
```

- [ ] **Step 2: Agregar `@inject IGroupRuleService GroupRuleService` al header del componente**

```razor
@inject IGroupRuleService GroupRuleService
```

- [ ] **Step 3: Agregar la sección de GroupRules al markup, DESPUÉS de la sección "Catalog" y ANTES de la sección "General"**

```razor
<MudPaper Class="pa-4" Elevation="1">
    <MudStack Spacing="2">
        <MudStack Row="true" AlignItems="AlignItems.Center">
            <MudText Typo="Typo.subtitle2" Color="Color.Secondary">Group Rules</MudText>
            <MudSpacer />
            <MudChip T="string" Size="Size.Small" Color="Color.Primary" Variant="Variant.Outlined">
                @_groupRules.Count rules
            </MudChip>
        </MudStack>

        @* Formulario para nueva regla *@
        <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2" Wrap="Wrap.Wrap">
            <MudTextField T="string"
                          @bind-Value="_newRule.Name"
                          Label="Name"
                          Placeholder="Services"
                          Variant="Variant.Outlined"
                          Margin="Margin.Dense"
                          Style="width: 160px;" />
            <MudSelect T="string"
                       @bind-Value="_newRule.Color"
                       Label="Color"
                       Variant="Variant.Outlined"
                       Margin="Margin.Dense"
                       Style="width: 140px;">
                @foreach (var c in _availableColors)
                {
                    <MudSelectItem Value="@c">
                        <MudChip T="string" Size="Size.Small"
                                 Color="@ColorHelper.FromGroupColor(c)"
                                 Variant="Variant.Outlined">@c</MudChip>
                    </MudSelectItem>
                }
            </MudSelect>
            <MudTextField T="string"
                          @bind-Value="_newPrefixesInput"
                          Label="Prefixes (comma separated)"
                          Placeholder="Svc., Api."
                          Variant="Variant.Outlined"
                          Margin="Margin.Dense"
                          Style="min-width: 240px; flex: 1 1 240px;" />
            <MudButton Variant="Variant.Filled"
                       Color="Color.Primary"
                       StartIcon="@Icons.Material.Filled.Add"
                       Disabled="@_groupRulesBusy"
                       OnClick="AddGroupRuleAsync">
                Add rule
            </MudButton>
        </MudStack>

        @if (_groupRules.Count == 0)
        {
            <MudAlert Severity="Severity.Info" Variant="Variant.Outlined" Dense="true">
                No group rules configured.
            </MudAlert>
        }
        else
        {
            <MudTable Items="_groupRules" Dense="true" Hover="true" Elevation="0">
                <HeaderContent>
                    <MudTh>Order</MudTh>
                    <MudTh>Name</MudTh>
                    <MudTh>Color</MudTh>
                    <MudTh>Prefixes</MudTh>
                    <MudTh></MudTh>
                </HeaderContent>
                <RowTemplate>
                    @if (_editingRuleId == context.Id)
                    {
                        <MudTd>@context.Order</MudTd>
                        <MudTd>
                            <MudTextField T="string" @bind-Value="context.Name"
                                          Variant="Variant.Outlined" Margin="Margin.Dense" />
                        </MudTd>
                        <MudTd>
                            <MudSelect T="string" @bind-Value="context.Color"
                                       Variant="Variant.Outlined" Margin="Margin.Dense" Style="width:120px;">
                                @foreach (var c in _availableColors)
                                {
                                    <MudSelectItem Value="@c">@c</MudSelectItem>
                                }
                            </MudSelect>
                        </MudTd>
                        <MudTd>
                            <MudTextField T="string"
                                          Value="@string.Join(", ", context.Prefixes)"
                                          ValueChanged="v => context.Prefixes = ParsePrefixes(v)"
                                          Variant="Variant.Outlined" Margin="Margin.Dense" />
                        </MudTd>
                        <MudTd>
                            <MudIconButton Icon="@Icons.Material.Filled.Save"
                                           Size="Size.Small" Color="Color.Success"
                                           OnClick="@(() => SaveGroupRuleAsync(context))" />
                            <MudIconButton Icon="@Icons.Material.Filled.Cancel"
                                           Size="Size.Small" Color="Color.Default"
                                           OnClick="@(() => _editingRuleId = null)" />
                        </MudTd>
                    }
                    else
                    {
                        <MudTd>@context.Order</MudTd>
                        <MudTd>
                            <MudChip T="string" Size="Size.Small"
                                     Color="@ColorHelper.FromGroupColor(context.Color)"
                                     Variant="Variant.Outlined">@context.Name</MudChip>
                        </MudTd>
                        <MudTd>@context.Color</MudTd>
                        <MudTd>
                            <MudText Typo="Typo.body2">@string.Join(", ", context.Prefixes)</MudText>
                        </MudTd>
                        <MudTd>
                            <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                           Size="Size.Small" Color="Color.Default"
                                           Disabled="@_groupRulesBusy"
                                           OnClick="@(() => _editingRuleId = context.Id)" />
                            <MudTooltip Text="Delete rule">
                                <MudIconButton Icon="@Icons.Material.Filled.DeleteOutline"
                                               Size="Size.Small" Color="Color.Default"
                                               Disabled="@_groupRulesBusy"
                                               OnClick="@(() => DeleteGroupRuleAsync(context.Id))" />
                            </MudTooltip>
                        </MudTd>
                    }
                </RowTemplate>
            </MudTable>
        }
    </MudStack>
</MudPaper>
```

- [ ] **Step 4: Agregar campos y métodos en el bloque `@code`**

```csharp
// Campos nuevos
private bool _groupRulesBusy;
private int? _editingRuleId;
private GroupRule _newRule = new() { Color = "default" };
private string _newPrefixesInput = string.Empty;
private IReadOnlyList<GroupRule> _groupRules = [];

private static readonly string[] _availableColors =
    ["primary", "secondary", "tertiary", "info", "success", "warning", "error", "default"];

// Llamar desde OnInitializedAsync (agregar junto a LoadCatalogAsync)
private async Task LoadGroupRulesAsync()
{
    _groupRules = await GroupRuleService.GetAllAsync();
}

private async Task AddGroupRuleAsync()
{
    if (string.IsNullOrWhiteSpace(_newRule.Name)) return;

    _groupRulesBusy = true;
    try
    {
        _newRule.Prefixes = ParsePrefixes(_newPrefixesInput);
        await GroupRuleService.AddAsync(_newRule);
        _newRule = new GroupRule { Color = "default" };
        _newPrefixesInput = string.Empty;
        await LoadGroupRulesAsync();
        Snackbar.Add("Group rule added.", Severity.Success);
    }
    catch (Exception ex)
    {
        Snackbar.Add(ex.Message, Severity.Error);
    }
    finally
    {
        _groupRulesBusy = false;
    }
}

private async Task SaveGroupRuleAsync(GroupRule rule)
{
    _groupRulesBusy = true;
    try
    {
        await GroupRuleService.UpdateAsync(rule);
        _editingRuleId = null;
        await LoadGroupRulesAsync();
        Snackbar.Add("Group rule saved.", Severity.Success);
    }
    catch (Exception ex)
    {
        Snackbar.Add(ex.Message, Severity.Error);
    }
    finally
    {
        _groupRulesBusy = false;
    }
}

private async Task DeleteGroupRuleAsync(int id)
{
    _groupRulesBusy = true;
    try
    {
        await GroupRuleService.DeleteAsync(id);
        await LoadGroupRulesAsync();
        Snackbar.Add("Group rule deleted.", Severity.Info);
    }
    catch (Exception ex)
    {
        Snackbar.Add(ex.Message, Severity.Error);
    }
    finally
    {
        _groupRulesBusy = false;
    }
}

private static List<string> ParsePrefixes(string input) =>
    input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
         .ToList();
```

- [ ] **Step 5: Actualizar `OnInitializedAsync` para llamar también a `LoadGroupRulesAsync`**

```csharp
protected override async Task OnInitializedAsync()
{
    await LoadCatalogAsync();
    await LoadGroupRulesAsync();
}
```

- [ ] **Step 6: Buildear**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores.

- [ ] **Step 7: Commit**

```bash
git add src/DevHub/Components/Pages/Settings.razor
git commit -m "feat: add GroupRules CRUD section to Settings page"
```

---

## Task 7: Limpiar `appsettings.json` — eliminar `Groups` del config

**Files:**
- Modify: `src/DevHub/appsettings.json`
- Modify: `src/DevHub/Services/DevHubOptions.cs`

### Contexto
Las reglas ahora viven en la DB. El seed inicial ya se hizo (Task 4). La sección `Groups` de `appsettings.json` queda obsoleta. Se elimina de la config y de `DevHubOptions`. **Importante:** el seed solo corre si la tabla está vacía, así que eliminar `Groups` del config no borra las reglas que ya están en la DB.

- [ ] **Step 1: En `appsettings.json`, eliminar la clave `"Groups"` dentro de `"DevHub"`**

```json
{
  "DevHub": {
    "RootPath": "C:\\_O",
    "ScanIntervalSeconds": 60,
    "ParallelScanDegree": 8,
    "ExcludedRepos": [],
    "DefaultGroup": "Other"
  },
  ...
}
```

La clave `"Groups": [...]` se elimina completamente.

- [ ] **Step 2: En `DevHubOptions.cs`, eliminar la propiedad `Groups`**

```csharp
public class DevHubOptions
{
    public string RootPath { get; set; } = string.Empty;
    public int ScanIntervalSeconds { get; set; } = 60;
    public int ParallelScanDegree { get; set; } = 4;
    public string DefaultGroup { get; set; } = "Other";
    public List<string> ExcludedRepos { get; set; } = [];
    // Groups eliminado — ahora vive en la DB
}
```

- [ ] **Step 3: Buscar referencias a `devHubOptions.Groups` o `_options.Groups` en el código**

```bash
grep -rn "\.Groups" src/DevHub/
```

Actualizar cualquier referencia que quede. En el startup (Task 4), la llamada a `SeedFromConfigAsync(devHubOptions.Groups)` ahora pasa lista vacía — el seed no hará nada porque la tabla ya tiene datos. Esto es correcto.

- [ ] **Step 4: Buildear**

```bash
dotnet build src/DevHub/DevHub.csproj
```

Expected: 0 errores.

- [ ] **Step 5: Ejecutar todos los tests**

```bash
dotnet test tests/DevHub.U.Tests
```

Expected: todos pasan.

- [ ] **Step 6: Commit**

```bash
git add src/DevHub/appsettings.json src/DevHub/Services/DevHubOptions.cs
git commit -m "cleanup: remove Groups from appsettings, GroupRules now live in DB"
```

---

*7 tasks — model → service → tests → DI/seed → scanner → UI → cleanup. Ejecutar en orden.*
