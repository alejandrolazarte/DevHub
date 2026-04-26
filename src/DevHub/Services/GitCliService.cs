using System.Diagnostics;
using DevHub.Models;

namespace DevHub.Services;

public class GitCliService : IGitService
{
    private static readonly string GitExe = ResolveGitExe();

    private static string ResolveGitExe()
    {
        // Windows Services run with a minimal process PATH that often excludes Git.
        // Read the SYSTEM and USER PATH from the registry — that's where git installers
        // register themselves, and it works on any machine without hardcoding paths.
        var systemPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
        var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";

        foreach (var dir in $"{systemPath};{userPath}".Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), "git.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return "git"; // last resort: rely on process PATH (works fine under dotnet run)
    }

    public async Task<(bool IsDirty, int DirtyFileCount)> GetStatusAsync(
        string repoPath, CancellationToken ct = default)
    {
        var (output, _, exitCode) = await RunGitAsync(repoPath, ["status", "--porcelain"], ct);
        if (exitCode != 0)
        {
            return (false, 0);
        }

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return (lines.Length > 0, lines.Length);
    }

    public async Task<string> GetBranchAsync(
        string repoPath, CancellationToken ct = default)
    {
        var (output, _, exitCode) = await RunGitAsync(repoPath, ["branch", "--show-current"], ct);
        return exitCode == 0 ? output : string.Empty;
    }

    public async Task<(int Ahead, int Behind)> GetAheadBehindAsync(
        string repoPath, CancellationToken ct = default)
    {
        var (output, _, exitCode) = await RunGitAsync(
            repoPath, ["rev-list", "--count", "--left-right", "HEAD...@{u}"], ct);
        if (exitCode != 0)
        {
            return (0, 0);
        }

        var parts = output.Split('\t');
        if (parts.Length != 2)
        {
            return (0, 0);
        }

        return (int.TryParse(parts[0], out var ahead) ? ahead : 0,
                int.TryParse(parts[1], out var behind) ? behind : 0);
    }

    public async Task<string> GetLastCommitMessageAsync(
        string repoPath, CancellationToken ct = default)
    {
        var (output, _, exitCode) = await RunGitAsync(repoPath, ["log", "-1", "--pretty=%s"], ct);
        return exitCode == 0 ? output : string.Empty;
    }

    public async Task<DateTime> GetLastCommitDateAsync(
        string repoPath, CancellationToken ct = default)
    {
        var (output, _, exitCode) = await RunGitAsync(repoPath, ["log", "-1", "--format=%ct"], ct);
        if (exitCode != 0 || !long.TryParse(output, out var unixSeconds))
        {
            return DateTime.MinValue;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime;
    }

    public async Task<string> GetRemoteUrlAsync(
        string repoPath, CancellationToken ct = default)
    {
        var (output, _, exitCode) = await RunGitAsync(repoPath, ["remote", "get-url", "origin"], ct);
        return exitCode == 0 ? output : string.Empty;
    }

    public async Task<IReadOnlyList<string>> GetBranchesAsync(
        string repoPath, CancellationToken ct = default)
    {
        var (output, _, exitCode) = await RunGitAsync(repoPath, ["branch", "--format=%(refname:short)"], ct);
        if (exitCode != 0)
        {
            return [];
        }

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                     .Select(b => b.Trim())
                     .Where(b => !string.IsNullOrEmpty(b))
                     .ToList();
    }

    public async Task<(bool Success, string Error)> PullAsync(
        string repoPath, CancellationToken ct = default)
    {
        var (_, error, exitCode) = await RunGitAsync(repoPath, ["pull", "--ff-only"], ct);
        return (exitCode == 0, error);
    }

    public async Task<(bool Success, string Error)> CheckoutAsync(
        string repoPath, string branch, CancellationToken ct = default)
    {
        var (_, _, exitCode) = await RunGitAsync(repoPath, ["checkout", branch], ct);
        if (exitCode == 0)
        {
            return (true, string.Empty);
        }

        var (_, error2, exitCode2) = await RunGitAsync(repoPath, ["checkout", "-b", branch], ct);
        return (exitCode2 == 0, exitCode2 != 0 ? error2 : string.Empty);
    }

    public bool IsGitRepo(string path) =>
        Directory.Exists(Path.Combine(path, ".git"));

    public async Task<RepoInfo> ScanRepoAsync(
        string repoPath, string group, string groupColor, CancellationToken ct = default)
    {
        var name = System.IO.Path.GetFileName(repoPath);
        var gitDir = System.IO.Path.Combine(repoPath, ".git");

        if (!Directory.Exists(gitDir))
        {
            return new RepoInfo
            {
                Name = name,
                Path = repoPath,
                Group = group,
                GroupColor = groupColor,
                IsGitRepo = false,
                LastScanned = DateTime.Now
            };
        }

        try
        {
            var statusTask = GetStatusAsync(repoPath, ct);
            var branchTask = GetBranchAsync(repoPath, ct);
            var aheadBehindTask = GetAheadBehindAsync(repoPath, ct);
            var lastCommitTask = GetLastCommitMessageAsync(repoPath, ct);
            var lastCommitDateTask = GetLastCommitDateAsync(repoPath, ct);
            var remoteUrlTask = GetRemoteUrlAsync(repoPath, ct);

            await Task.WhenAll(statusTask, branchTask, aheadBehindTask, lastCommitTask, lastCommitDateTask, remoteUrlTask);

            var (isDirty, dirtyCount) = await statusTask;
            var (ahead, behind) = await aheadBehindTask;

            return new RepoInfo
            {
                Name = name,
                Path = repoPath,
                Group = group,
                GroupColor = groupColor,
                Branch = await branchTask,
                IsDirty = isDirty,
                DirtyFileCount = dirtyCount,
                AheadCount = ahead,
                BehindCount = behind,
                LastCommitMessage = await lastCommitTask,
                LastCommitDate = await lastCommitDateTask,
                RemoteUrl = await remoteUrlTask,
                LastScanned = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            return new RepoInfo
            {
                Name = name,
                Path = repoPath,
                Group = group,
                GroupColor = groupColor,
                ScanError = ex.Message,
                LastScanned = DateTime.Now
            };
        }
    }

    private static async Task<(string Output, string Error, int ExitCode)> RunGitAsync(
        string workingDir, string[] args, CancellationToken ct = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GitExe,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("safe.directory=*");
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        process.Start();
        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var error = await process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            return (output.Trim(), error.Trim(), process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
