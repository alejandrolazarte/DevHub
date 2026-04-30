using DevHub.Data;
using DevHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHub.Services;

public partial class EfCanvasService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ILogger<EfCanvasService> logger) : ICanvasService
{
    public async Task<IReadOnlyList<CanvasBoard>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Canvases
            .AsNoTracking()
            .OrderByDescending(c => c.UpdatedUtc)
            .ToListAsync(ct);
    }

    public async Task<CanvasBoard?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Canvases.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<CanvasBoard> CreateAsync(string name, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var canvas = new CanvasBoard { Name = name, CreatedUtc = now, UpdatedUtc = now };
        db.Canvases.Add(canvas);
        await db.SaveChangesAsync(ct);
        LogCreated(logger, canvas.Id, name);
        return canvas;
    }

    public async Task SaveGraphAsync(int canvasId, string cytoscapeJson, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Canvases
            .Where(c => c.Id == canvasId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.CytoscapeJson, cytoscapeJson)
                .SetProperty(c => c.UpdatedUtc, DateTime.UtcNow), ct);
        LogSaved(logger, canvasId);
    }

    public async Task RenameAsync(int canvasId, string newName, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Canvases
            .Where(c => c.Id == canvasId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Name, newName)
                .SetProperty(c => c.UpdatedUtc, DateTime.UtcNow), ct);
    }

    public async Task DeleteAsync(int canvasId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Canvases.Where(c => c.Id == canvasId).ExecuteDeleteAsync(ct);
        LogDeleted(logger, canvasId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Canvas {Id} '{Name}' created")]
    private static partial void LogCreated(ILogger logger, int id, string name);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Canvas {Id} graph saved")]
    private static partial void LogSaved(ILogger logger, int id);

    [LoggerMessage(Level = LogLevel.Information, Message = "Canvas {Id} deleted")]
    private static partial void LogDeleted(ILogger logger, int id);
}
