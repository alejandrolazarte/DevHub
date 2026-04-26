using DevHub.Data;
using DevHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHub.Services;

public partial class EfRepoCatalogService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ILogger<EfRepoCatalogService> logger) : IRepoCatalogService
{
    public async Task<IReadOnlyList<RepoCatalogEntry>> GetReposAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.RepoCatalogEntries
            .AsNoTracking()
            .OrderBy(r => r.Path)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetRepoPathsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.RepoCatalogEntries
            .AsNoTracking()
            .OrderBy(r => r.Path)
            .Select(r => r.Path)
            .ToListAsync(ct);
    }

    public async Task AddAsync(string repoPath, CancellationToken ct = default)
    {
        var normalizedPath = NormalizePath(repoPath);

        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException($"Path does not exist: {normalizedPath}");
        }

        if (!Directory.Exists(Path.Combine(normalizedPath, ".git")))
        {
            throw new InvalidOperationException($"Not a git repository: {normalizedPath}");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var exists = await db.RepoCatalogEntries.AnyAsync(r => r.Path == normalizedPath, ct);
        if (!exists)
        {
            db.RepoCatalogEntries.Add(new RepoCatalogEntry(normalizedPath, DateTime.UtcNow));
            await db.SaveChangesAsync(ct);
            LogRepoAdded(logger, normalizedPath);
        }
    }

    public async Task RemoveAsync(string repoPath, CancellationToken ct = default)
    {
        var normalizedPath = NormalizePath(repoPath);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.RepoCatalogEntries
            .Where(r => r.Path == normalizedPath)
            .ExecuteDeleteAsync(ct);
        LogRepoRemoved(logger, normalizedPath);
    }

    public async Task<int> ImportFromRootAsync(string rootPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return 0;
        }

        var candidates = Directory.GetDirectories(rootPath)
            .Where(p => Directory.Exists(Path.Combine(p, ".git")))
            .Select(p => Path.GetFullPath(p))
            .OrderBy(p => p)
            .ToList();

        if (candidates.Count == 0)
        {
            return 0;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.RepoCatalogEntries.Select(r => r.Path).ToHashSetAsync(ct);

        var toAdd = candidates.Where(p => !existing.Contains(p)).ToList();
        foreach (var path in toAdd)
        {
            db.RepoCatalogEntries.Add(new RepoCatalogEntry(path, DateTime.UtcNow));
        }

        await db.SaveChangesAsync(ct);
        LogReposImported(logger, toAdd.Count, rootPath);
        return toAdd.Count;
    }

    public string GetDisplayConnectionString() => "EF Core — see ConnectionStrings:DefaultConnection in appsettings.json";

    private static string NormalizePath(string repoPath) =>
        Path.GetFullPath(repoPath.Trim().Trim('"'));

    [LoggerMessage(Level = LogLevel.Information, Message = "Added repo: {Path}")]
    private static partial void LogRepoAdded(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed repo: {Path}")]
    private static partial void LogRepoRemoved(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Imported {Count} repos from {Root}")]
    private static partial void LogReposImported(ILogger logger, int count, string root);
}
