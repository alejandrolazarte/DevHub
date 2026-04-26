using DevHub.Models;

namespace DevHub.Services;

public interface IRepoCatalogService
{
    Task<IReadOnlyList<RepoCatalogEntry>> GetReposAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRepoPathsAsync(CancellationToken ct = default);
    Task AddAsync(string repoPath, CancellationToken ct = default);
    Task RemoveAsync(string repoPath, CancellationToken ct = default);
    Task<int> ImportFromRootAsync(string rootPath, CancellationToken ct = default);
    string GetDisplayConnectionString();
}
