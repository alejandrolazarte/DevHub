using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevHub.Services;

public partial class BackgroundFetchService(
    IRepoCatalogService repoCatalogService,
    IGitService gitService,
    IOptions<DevHubOptions> options,
    ILogger<BackgroundFetchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the initial scan complete before the first fetch
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await FetchAllAsync(stoppingToken);
            await Task.Delay(
                TimeSpan.FromMinutes(options.Value.FetchIntervalMinutes),
                stoppingToken);
        }
    }

    private async Task FetchAllAsync(CancellationToken ct)
    {
        IReadOnlyList<string> paths;
        try
        {
            paths = await repoCatalogService.GetRepoPathsAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        LogFetching(logger, paths.Count);

        await Parallel.ForEachAsync(
            paths.Where(Directory.Exists),
            new ParallelOptions { MaxDegreeOfParallelism = options.Value.ParallelScanDegree, CancellationToken = ct },
            async (repoPath, token) =>
            {
                var (success, error) = await gitService.FetchAsync(repoPath, token);
                if (!success && !string.IsNullOrEmpty(error))
                {
                    LogFetchFailed(logger, Path.GetFileName(repoPath), error);
                }
            });

        LogFetchComplete(logger);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetching {Count} repos")]
    private static partial void LogFetching(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetch complete")]
    private static partial void LogFetchComplete(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fetch failed for {RepoName}: {Error}")]
    private static partial void LogFetchFailed(ILogger logger, string repoName, string error);
}
