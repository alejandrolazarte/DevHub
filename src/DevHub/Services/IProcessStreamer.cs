namespace DevHub.Services;

public interface IProcessStreamer
{
    Task StreamAsync(
        string workingDirectory,
        string command,
        Func<string, Task> onLine,
        CancellationToken ct = default);
}