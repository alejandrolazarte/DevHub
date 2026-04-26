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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rules = await groupRuleService.GetAllAsync(stoppingToken);
        GroupRuleCache.Initialize(rules, _options.DefaultGroup);
        await TriggerScanAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.WhenAny(
                    Task.Delay(TimeSpan.FromSeconds(_options.ScanIntervalSeconds), stoppingToken),
                    _triggerSemaphore.WaitAsync(stoppingToken).ContinueWith(_ => { }, stoppingToken));

                if (!stoppingToken.IsCancellationRequested)
                {
                    await TriggerScanAsync(stoppingToken);
                }
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
                    var (group, groupColor) = RepoGroupResolver.Resolve(
                        repoName, GroupRuleCache.GetRules());
                    results[item.i] = await gitService.ScanRepoAsync(item.path, group, groupColor, token);
                });

            store.SetRepos(results, resetScanning: false);
            LogScanComplete(logger);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Scanning {Count} repos")]
    private static partial void LogScanning(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scan complete")]
    private static partial void LogScanComplete(ILogger logger);
}
