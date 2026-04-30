namespace DevHub.Services;

public partial class CanvasVsCodeService(
    IProcessRunner runner,
    ILogger<CanvasVsCodeService> logger)
{
    public async Task OpenFileAsync(string filePath, int lineNumber, CancellationToken ct = default)
    {
        try
        {
            await runner.RunAsync("code", $"--goto \"{filePath}:{lineNumber}\"", ct: ct);
        }
        catch (Exception ex)
        {
            LogFailed(logger, filePath, lineNumber, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "VS Code could not open {FilePath}:{Line}")]
    private static partial void LogFailed(ILogger logger, string filePath, int line, Exception ex);
}
