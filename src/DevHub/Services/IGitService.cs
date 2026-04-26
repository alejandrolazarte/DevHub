using DevHub.Models;

namespace DevHub.Services;

public interface IGitService
{
    Task<RepoInfo> ScanRepoAsync(string repoPath, string group, string groupColor, CancellationToken ct = default);
    Task<(bool Success, string Error)> PullAsync(string repoPath, CancellationToken ct = default);
    Task<(bool Success, string Error)> CheckoutAsync(string repoPath, string branch, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetBranchesAsync(string repoPath, CancellationToken ct = default);
    bool IsGitRepo(string path);
    Task<(bool Success, string Error)> FetchAsync(string repoPath, CancellationToken ct = default);
}
