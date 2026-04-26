using DevHub.Data;
using DevHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHub.Services;

public class HiddenCommandService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task<HashSet<string>> GetHiddenNamesAsync(string repoPath, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var names = await db.HiddenAutoCommands
            .Where(h => h.RepoPath == repoPath)
            .Select(h => h.Name)
            .ToListAsync(ct);
        return [.. names];
    }

    public async Task HideAsync(string repoPath, string name, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var exists = await db.HiddenAutoCommands
            .AnyAsync(h => h.RepoPath == repoPath && h.Name == name, ct);
        if (!exists)
        {
            db.HiddenAutoCommands.Add(new HiddenAutoCommand { RepoPath = repoPath, Name = name });
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task RestoreAllAsync(string repoPath, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entries = await db.HiddenAutoCommands
            .Where(h => h.RepoPath == repoPath)
            .ToListAsync(ct);
        db.HiddenAutoCommands.RemoveRange(entries);
        await db.SaveChangesAsync(ct);
    }
}
