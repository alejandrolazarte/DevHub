using System.Collections.Concurrent;
using DevHub.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevHub.Services;

public partial class RepoScannerService(
    IGitService gitService,
    IRepoCatalogService repoCatalogService,
    RepoStateStore store,
    IGroupRuleService groupRuleService,
    IOptions<DevHubOptions> options,
    ILogger<RepoScannerService> logger) : BackgroundService
{
    private readonly DevHubOptions _options = options.Value;
    private readonly SemaphoreSlim _triggerSemaphore = new(0, 1);
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debouncers = new(StringComparer.OrdinalIgnoreCase);
    private CancellationToken _stopping;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stopping = stoppingToken;
        var rules = await groupRuleService.GetAllAsync(stoppingToken);
        GroupRuleCache.Initialize(rules, _options.DefaultGroup);
        await TriggerScanAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _triggerSemaphore.WaitAsync(stoppingToken);
                await TriggerScanAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task TriggerScanAsync(CancellationToken ct = default)
    {
        store.SetScanning(true);
        try
        {
            var repoPaths = await DiscoverReposAsync(ct);
            LogScanning(logger, repoPaths.Count);

            var results = new RepoInfo[repoPaths.Count];
            await Parallel.ForEachAsync(
                repoPaths.Select((path, i) => (path, i)),
                new ParallelOptions { MaxDegreeOfParallelism = _options.ParallelScanDegree, CancellationToken = ct },
                async (item, token) =>
                {
                    var repoName = Path.GetFileName(item.path);
                    var (group, groupColor) = RepoGroupResolver.Resolve(repoName, GroupRuleCache.GetRules());
                    results[item.i] = await gitService.ScanRepoAsync(item.path, group, groupColor, token);
                });

            store.SetRepos(results, resetScanning: false);
            LogScanComplete(logger);
            ReconcileWatchers(repoPaths);
        }
        finally
        {
            store.SetScanning(false);
        }
    }

    public void RequestManualRefresh()
    {
        try { _triggerSemaphore.Release(); }
        catch (SemaphoreFullException) { /* trigger already pending */ }
    }

    private void ReconcileWatchers(IReadOnlyList<string> currentPaths)
    {
        var pathSet = currentPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in _watchers.Keys.Where(p => !pathSet.Contains(p)).ToList())
        {
            RemoveWatcher(path);
        }

        foreach (var path in currentPaths.Where(p => !_watchers.ContainsKey(p)))
        {
            AddWatcher(path);
        }
    }

    private void AddWatcher(string repoPath)
    {
        var gitDir = Path.Combine(repoPath, ".git");
        if (!Directory.Exists(gitDir))
        {
            return;
        }

        var watcher = new FileSystemWatcher(gitDir)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
        };

        watcher.Changed += (_, _) => OnGitDirChanged(repoPath);
        watcher.Created += (_, _) => OnGitDirChanged(repoPath);
        watcher.Deleted += (_, _) => OnGitDirChanged(repoPath);
        watcher.Renamed += (_, _) => OnGitDirChanged(repoPath);

        _watchers[repoPath] = watcher;
    }

    private void RemoveWatcher(string repoPath)
    {
        if (_watchers.TryRemove(repoPath, out var watcher))
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        if (_debouncers.TryRemove(repoPath, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private void OnGitDirChanged(string repoPath)
    {
        var newCts = CancellationTokenSource.CreateLinkedTokenSource(_stopping);

        var oldCts = _debouncers.AddOrUpdate(repoPath, newCts, (_, existing) =>
        {
            existing.Cancel();
            existing.Dispose();
            return newCts;
        });

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, newCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await ScanSingleRepoAsync(repoPath, _stopping);
        }, _stopping);
    }

    private async Task ScanSingleRepoAsync(string repoPath, CancellationToken ct)
    {
        try
        {
            var repoName = Path.GetFileName(repoPath);
            var (group, groupColor) = RepoGroupResolver.Resolve(repoName, GroupRuleCache.GetRules());
            var repoInfo = await gitService.ScanRepoAsync(repoPath, group, groupColor, ct);
            store.UpdateRepo(repoInfo);
            LogRepoUpdated(logger, repoName);
        }
        catch (OperationCanceledException) { }
    }

    private async Task<List<string>> DiscoverReposAsync(CancellationToken ct)
    {
        var repoPaths = (await repoCatalogService.GetRepoPathsAsync(ct))
            .Where(Directory.Exists)
            .Where(path => !_options.ExcludedRepos.Contains(Path.GetFileName(path)))
            .OrderBy(path => path)
            .ToList();

        if (repoPaths.Count == 0 && Directory.Exists(_options.RootPath))
        {
            await repoCatalogService.ImportFromRootAsync(_options.RootPath, ct);
            repoPaths = (await repoCatalogService.GetRepoPathsAsync(ct))
                .Where(Directory.Exists)
                .Where(path => !_options.ExcludedRepos.Contains(Path.GetFileName(path)))
                .OrderBy(path => path)
                .ToList();
        }

        return repoPaths;
    }

    public override void Dispose()
    {
        foreach (var path in _watchers.Keys.ToList())
        {
            RemoveWatcher(path);
        }

        base.Dispose();
        GC.SuppressFinalize(this);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Scanning {Count} repos")]
    private static partial void LogScanning(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scan complete")]
    private static partial void LogScanComplete(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Repo updated via watcher: {RepoName}")]
    private static partial void LogRepoUpdated(ILogger logger, string repoName);
}
