using DevHub.Models;

namespace DevHub.Services;

public sealed class TerminalPanelService(ShellSessionService sessions)
{
    public bool IsOpen { get; private set; }
    public RepoInfo? ActiveRepo { get; private set; }

    public event Action? StateChanged;

    public void OpenForRepo(RepoInfo repo)
    {
        if (!IsOpen)
        {
            IsOpen = true;
            ActiveRepo = repo;
            StateChanged?.Invoke();
            return;
        }

        if (ActiveRepo?.Path == repo.Path)
        {
            StateChanged?.Invoke();
            return;
        }

        var current = sessions.TryGet(ActiveRepo?.Path);
        var busy = current is { HasExited: false }
            && current.GetLines().Any(l => l.Kind == ConsoleLineKind.Input);

        if (!busy)
        {
            ActiveRepo = repo;
        }

        StateChanged?.Invoke();
    }

    public void Close()
    {
        IsOpen = false;
        ActiveRepo = null;
        StateChanged?.Invoke();
    }
}
