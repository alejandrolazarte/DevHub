using Microsoft.Extensions.Options;

namespace DevHub.Services;

public record ServiceBusMapResult(bool Success, string Output, string Error, DateTime CompletedAt);

public partial class ServiceBusMapService(
    IProcessRunner runner,
    IOptions<ServiceBusMapOptions> options,
    IHostEnvironment env,
    ILogger<ServiceBusMapService> logger)
{
    private readonly ServiceBusMapOptions _options = options.Value;

    public async Task<ServiceBusMapResult> RegenerateAsync(CancellationToken ct = default)
    {
        var contentRoot = env.ContentRootPath;
        var scriptPath = Path.Combine(contentRoot, _options.ScriptPath);
        var templatePath = Path.Combine(contentRoot, _options.TemplateFile);
        var outputPath = Path.Combine(contentRoot, _options.OutputFile);

        var arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                        $"-ReposRoot \"{_options.ReposRoot}\" " +
                        $"-RepositoryNamePattern \"{_options.RepositoryNamePattern}\" " +
                        $"-DisplayNamePrefixToTrim \"{_options.DisplayNamePrefixToTrim}\" " +
                        $"-TemplateFile \"{templatePath}\" " +
                        $"-OutputFile \"{outputPath}\"";

        LogRegenerating(logger, scriptPath);
        var result = await runner.RunAsync("powershell.exe", arguments, contentRoot, ct);

        var success = result.ExitCode == 0;
        if (!success)
        {
            LogRegenerationFailed(logger, result.ExitCode, result.StdErr);
        }

        return new ServiceBusMapResult(success, result.StdOut, result.StdErr, DateTime.UtcNow);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Regenerating Service Bus map: {Script}")]
    private static partial void LogRegenerating(ILogger logger, string script);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Service Bus map regeneration failed (exit {Code}): {Err}")]
    private static partial void LogRegenerationFailed(ILogger logger, int code, string err);
}
