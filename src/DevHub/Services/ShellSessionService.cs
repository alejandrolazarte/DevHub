using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DevHub.Models;

namespace DevHub.Services;

// ── ShellSession ────────────────────────────────────────────────────────────

public sealed class ShellSession : IDisposable
{
    private static readonly Regex AnsiRegex =
        new(@"\x1B\[[0-9;]*[a-zA-Z]|\x1B\].*?(\x07|\x1B\\)|\x1B[=>]|\x1B\([A-Z]", RegexOptions.Compiled);

    private readonly List<ConsoleLine> _lines = [];
    private readonly object _linesLock = new();
    private readonly CancellationTokenSource _cts = new();

    // PTY path (Windows)
    private readonly WindowsConPty? _pty;

    // Fallback path (non-Windows)
    private readonly Process? _process;

    private string _lineBuffer = string.Empty;

    public event Action<ConsoleLine>? LineAdded;
    public event Action? Exited;

    public string RepoPath { get; }
    public bool HasExited { get; private set; }

    private ShellSession(string repoPath, WindowsConPty pty)
    {
        RepoPath = repoPath;
        _pty = pty;
        _ = ReadLoopAsync(pty.OutputStream);
    }

    private ShellSession(string repoPath, Process process)
    {
        RepoPath = repoPath;
        _process = process;
    }

    public static ShellSession Create(string repoPath)
    {
        var (exe, args) = GetShellConfig();
        var cwd = Directory.Exists(repoPath) ? repoPath : Environment.CurrentDirectory;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var pty = WindowsConPty.Create(cwd, exe, args);
                return new ShellSession(repoPath, pty);
            }
            catch
            {
                // fall through to Process fallback
            }
        }

        return CreateWithProcess(repoPath, exe, args, cwd);
    }

    private static ShellSession CreateWithProcess(string repoPath, string exe, string args, string cwd)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = cwd,
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
            if (e.Data is not null)
            {
                session.AddLine(ConsoleLine.FromOutput(StripAnsi(e.Data)));
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                session.AddLine(ConsoleLine.FromError(StripAnsi(e.Data)));
            }
        };
        process.Exited += (_, _) =>
        {
            session.HasExited = true;
            session.AddLine(ConsoleLine.FromSystem("[shell exited]"));
            session.Exited?.Invoke();
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return session;
    }

    private async Task ReadLoopAsync(Stream stream)
    {
        var buffer = new byte[4096];
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer, _cts.Token);
                if (read == 0)
                {
                    break;
                }

                ProcessChunk(Encoding.UTF8.GetString(buffer, 0, read));
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            FlushLineBuffer();
            HasExited = true;
            AddLine(ConsoleLine.FromSystem("[shell exited]"));
            Exited?.Invoke();
        }
    }

    private void ProcessChunk(string raw)
    {
        var text = StripAnsi(raw)
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        _lineBuffer += text;

        var idx = _lineBuffer.IndexOf('\n');
        while (idx >= 0)
        {
            var line = _lineBuffer[..idx];
            _lineBuffer = _lineBuffer[(idx + 1)..];

            if (!string.IsNullOrEmpty(line))
            {
                AddLine(ConsoleLine.FromOutput(line));
            }

            idx = _lineBuffer.IndexOf('\n');
        }
    }

    private void FlushLineBuffer()
    {
        var remaining = _lineBuffer.Trim('\r', '\n');
        if (!string.IsNullOrWhiteSpace(remaining))
        {
            AddLine(ConsoleLine.FromOutput(remaining));
        }

        _lineBuffer = string.Empty;
    }

    public IReadOnlyList<ConsoleLine> GetLines()
    {
        lock (_linesLock)
        {
            return [.. _lines];
        }
    }

    public async Task SendAsync(string command)
    {
        AddLine(ConsoleLine.FromInput(command));

        if (_pty is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(command + "\n");
            await _pty.InputStream.WriteAsync(bytes);
            await _pty.InputStream.FlushAsync();
        }
        else if (_process is not null)
        {
            await _process.StandardInput.WriteLineAsync(command);
        }
    }

    public void ClearLines()
    {
        lock (_linesLock)
        {
            _lines.Clear();
        }
    }

    public void Kill()
    {
        _cts.Cancel();

        if (_pty is not null)
        {
            try { _pty.Kill(); } catch { }
            try { _pty.Dispose(); } catch { }
        }
        else if (_process is not null)
        {
            try { _process.Kill(entireProcessTree: true); } catch { }
            try { _process.Dispose(); } catch { }
        }
    }

    public void Dispose() => Kill();

    private void AddLine(ConsoleLine line)
    {
        lock (_linesLock)
        {
            _lines.Add(line);
        }

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

    public ShellSession? TryGet(string? repoPath)
    {
        if (repoPath is null)
        {
            return null;
        }

        lock (_lock)
        {
            return _sessions.TryGetValue(repoPath, out var s) ? s : null;
        }
    }

    public ShellSession GetOrCreate(string repoPath)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(repoPath, out var existing) && !existing.HasExited)
            {
                return existing;
            }

            var session = ShellSession.Create(repoPath);
            _sessions[repoPath] = session;
            return session;
        }
    }

    public void Kill(string repoPath)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(repoPath, out var s))
            {
                return;
            }

            s.Kill();
            _sessions.Remove(repoPath);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var s in _sessions.Values)
            {
                s.Kill();
            }

            _sessions.Clear();
        }
    }
}
