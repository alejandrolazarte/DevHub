using DevHub.Components;
using DevHub.Data;
using DevHub.Services;
using DevHub.Services.SecretProfiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
builder.Services.AddSingleton<IGitService, GitCliService>();
builder.Services.AddSingleton<IRepoCatalogService, EfRepoCatalogService>();
builder.Services.AddSingleton<RepoStateStore>();
builder.Services.AddSingleton<RepoScannerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RepoScannerService>());
builder.Services.AddHostedService<BackgroundFetchService>();
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton<ServiceBusMapService>();

builder.Services.AddSingleton<IGroupRuleService, GroupRuleService>();

builder.Services.Configure<SecretProfileOptions>(
    builder.Configuration.GetSection("SecretProfiles"));
builder.Services.AddSingleton<IFileSystem, FileSystem>();
builder.Services.AddSingleton<SecretProfileService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    var devHubOptions = scope.ServiceProvider.GetRequiredService<IOptions<DevHubOptions>>().Value;
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        await SeedGroupRulesAsync(dbFactory, devHubOptions);
    }
    catch (Exception ex)
    {
        ProgramLog.DbInitFailed(logger, ex);
    }
}

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

static async Task SeedGroupRulesAsync(IDbContextFactory<ApplicationDbContext> dbFactory, DevHubOptions options)
{
    await using var db = await dbFactory.CreateDbContextAsync();
    if (await db.GroupRules.AnyAsync())
    {
        return;
    }

    if (options.Groups.Count == 0)
    {
        return;
    }

    var order = 0;
    foreach (var group in options.Groups)
    {
        group.Order = order++;
    }
    db.GroupRules.AddRange(options.Groups);
    await db.SaveChangesAsync();
}

static partial class ProgramLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Database initialization failed — app will start but DB may be unavailable")]
    public static partial void DbInitFailed(ILogger logger, Exception ex);
}
