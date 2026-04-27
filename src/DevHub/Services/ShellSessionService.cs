using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DevHub.Models;

namespace DevHub.Services;

// ── ShellSession ────────────────────────────────────────────────────────────

public sealed class ShellSession
{
    private static readonly Regex AnsiRegex =
        new(@"\x1B\[[0-9;]*[a-zA-Z]|\x1B\].*?(\x07|\x1B\\)", RegexOptions.Compiled);

    private readonly List<ConsoleLine> _lines = [];
    private readonly object _linesLock = new();
    private readonly Process _process;

    public event Action<ConsoleLine>? LineAdded;
    public event Action? Exited;

    public string RepoPath { get; }
    public bool HasExited { get { try { return _process.HasExited; } catch { return true; } } }

    private ShellSession(string repoPath, Process process)
    {
        RepoPath = repoPath;
        _process = process;
    }

    public static ShellSession Create(string repoPath)
    {
        var (exe, args) = GetShellConfig();
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = Directory.Exists(repoPath) ? repoPath : Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var session = new ShellSession(repoPath, process);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) session.Add(ConsoleLine.FromOutput(StripAnsi(e.Data)));
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) session.Add(ConsoleLine.FromError(StripAnsi(e.Data)));
        };
        process.Exited += (_, _) =>
        {
            session.Add(ConsoleLine.FromSystem("[shell exited — click restart to continue]"));
            session.Exited?.Invoke();
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return session;
    }

    public IReadOnlyList<ConsoleLine> GetLines()
    {
        lock (_linesLock) return [.. _lines];
    }

    public async Task SendAsync(string command)
    {
        Add(ConsoleLine.FromInput(command));
        await _process.StandardInput.WriteLineAsync(command);
    }

    public void ClearLines()
    {
        lock (_linesLock) _lines.Clear();
    }

    public void Kill()
    {
        try { _process.Kill(entireProcessTree: true); } catch { }
        try { _process.Dispose(); } catch { }
    }

    private void Add(ConsoleLine line)
    {
        lock (_linesLock) _lines.Add(line);
        LineAdded?.Invoke(line);
    }

    private static string StripAnsi(string text) => AnsiRegex.Replace(text, "");

    private static (string Exe, string Args) GetShellConfig()
    {
        if (OperatingSystem.IsWindows())
        {
            var pwsh = FindInPath("pwsh.exe");
            return pwsh is not null
                ? (pwsh, "-NoLogo -NoExit")
                : ("powershell.exe", "-NoLogo -NoExit");
        }
        var bash = FindInPath("bash") ?? "/bin/bash";
        return (bash, string.Empty);
    }

    private static string? FindInPath(string name)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        return paths.Select(p => Path.Combine(p, name)).FirstOrDefault(File.Exists);
    }
}

// ── ShellSessionService ─────────────────────────────────────────────────────

public sealed class ShellSessionService : IDisposable
{
    private readonly Dictionary<string, ShellSession> _sessions = [];
    private readonly object _lock = new();

    public ShellSession GetOrCreate(string repoPath)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(repoPath, out var existing) && !existing.HasExited)
                return existing;

            var session = ShellSession.Create(repoPath);
            _sessions[repoPath] = session;
            return session;
        }
    }

    public void Kill(string repoPath)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(repoPath, out var s)) return;
            s.Kill();
            _sessions.Remove(repoPath);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var s in _sessions.Values) s.Kill();
            _sessions.Clear();
        }
    }
}
