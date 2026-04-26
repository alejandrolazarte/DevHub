namespace DevHub.Services;

public record ProcessResult(int ExitCode, string StdOut, string StdErr);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, string arguments, string? workingDirectory = null, CancellationToken ct = default);
}
