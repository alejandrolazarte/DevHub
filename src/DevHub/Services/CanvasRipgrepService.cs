using System.Text.Json;
using DevHub.Models;

namespace DevHub.Services;

public partial class CanvasRipgrepService(
    IProcessRunner runner,
    ILogger<CanvasRipgrepService> logger) : ICanvasRipgrepService
{
    private static readonly SemaphoreSlim _concurrency = new(8, 8);

    public async Task<SymbolSearchResult> SearchAsync(string term, IReadOnlyList<string> repoPaths, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return new SymbolSearchResult(term, []);
        }

        var tasks = repoPaths.Select(p => SearchRepoAsync(term, p, ct));
        var results = await Task.WhenAll(tasks);
        var matches = results.SelectMany(r => r).ToList();
        LogSearchComplete(logger, term, matches.Count);
        return new SymbolSearchResult(term, matches);
    }

    public async Task<ContextBlock> GetContextAsync(string filePath, int lineNumber, int contextLines = 3, CancellationToken ct = default)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, ct);
            var start = Math.Max(0, lineNumber - 1 - contextLines);
            var end = Math.Min(lines.Length - 1, lineNumber - 1 + contextLines);
            var block = Enumerable.Range(start, end - start + 1)
                .Select(i => new ContextLine(i + 1, lines[i], i + 1 == lineNumber))
                .ToList();
            return new ContextBlock(filePath, lineNumber, block);
        }
        catch (Exception ex)
        {
            LogContextFailed(logger, filePath, ex);
            return new ContextBlock(filePath, lineNumber, []);
        }
    }

    private async Task<IEnumerable<SymbolMatch>> SearchRepoAsync(string term, string repoPath, CancellationToken ct)
    {
        await _concurrency.WaitAsync(ct);
        try
        {
            var result = await runner.RunAsync(
                "rg",
                $"--line-number --no-heading --glob \"*.cs\" --glob \"*.ts\" --glob \"*.tsx\" -- \"{EscapeArg(term)}\"",
                repoPath,
                ct);

            if (result.ExitCode > 1)
            {
                if (!string.IsNullOrWhiteSpace(result.StdErr))
                {
                    LogRgError(logger, repoPath, result.StdErr.Trim());
                }
                return [];
            }

            return ParseMatches(term, repoPath, result.StdOut);
        }
        catch (Exception ex)
        {
            LogRgException(logger, repoPath, ex);
            return [];
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private static IEnumerable<SymbolMatch> ParseMatches(string term, string repoPath, string stdout)
    {
        var repoName = Path.GetFileName(repoPath.TrimEnd(Path.DirectorySeparatorChar));

        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(':', 3);
            if (parts.Length < 3)
            {
                continue;
            }

            if (!int.TryParse(parts[1], out var lineNumber))
            {
                continue;
            }

            var filePath = Path.IsPathRooted(parts[0]) ? parts[0] : Path.Combine(repoPath, parts[0]);
            var lineText = parts[2];
            var classification = SymbolClassifier.Classify(filePath, lineText, term);

            yield return new SymbolMatch(
                repoPath,
                repoName,
                filePath,
                lineNumber,
                lineText,
                classification?.Language,
                classification?.Role);
        }
    }

    private static string EscapeArg(string term) => term.Replace("\"", "\\\"");

    [LoggerMessage(Level = LogLevel.Debug, Message = "Search '{Term}' completed: {Count} matches")]
    private static partial void LogSearchComplete(ILogger logger, string term, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "rg error in {RepoPath}: {Error}")]
    private static partial void LogRgError(ILogger logger, string repoPath, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "rg failed for {RepoPath}")]
    private static partial void LogRgException(ILogger logger, string repoPath, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read context from {FilePath}")]
    private static partial void LogContextFailed(ILogger logger, string filePath, Exception ex);
}
