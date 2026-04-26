using DevHub.Data;
using DevHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHub.Services;

public class CustomCommandService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task<IReadOnlyList<CustomRepoCommand>> GetByRepoAsync(string repoPath, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CustomRepoCommands
            .Where(c => c.RepoPath == repoPath)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(string repoPath, string name, string command, string icon = "terminal", CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.CustomRepoCommands.Add(new CustomRepoCommand
        {
            RepoPath = repoPath,
            Name = name,
            Command = command,
            Icon = icon
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.CustomRepoCommands.FindAsync([id], ct);
        if (entity is not null)
        {
            db.CustomRepoCommands.Remove(entity);
            await db.SaveChangesAsync(ct);
        }
    }
}