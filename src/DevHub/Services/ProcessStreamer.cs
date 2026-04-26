using System.Diagnostics;

namespace DevHub.Services;

public class ProcessStreamer : IProcessStreamer
{
    public async Task StreamAsync(
        string workingDirectory,
        string command,
        Func<string, Task> onLine,
        CancellationToken ct = default)
    {
        var (executable, arguments) = ParseCommand(command);

        var psi = new ProcessStartInfo(executable, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = ReadStreamAsync(process.StandardOutput, onLine, ct);
        var stderrTask = ReadStreamAsync(process.StandardError, onLine, ct);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);
    }

    private static async Task ReadStreamAsync(
        StreamReader reader,
        Func<string, Task> onLine,
        CancellationToken ct)
    {
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null && !ct.IsCancellationRequested)
        {
            await onLine(line);
        }
    }

    private static (string Executable, string Arguments) ParseCommand(string command)
    {
        if (OperatingSystem.IsWindows())
        {
            return ("cmd.exe", $"/c {command}");
        }

        return ("/bin/sh", $"-c \"{command}\"");
    }
}