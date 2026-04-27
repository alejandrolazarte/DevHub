using DevHub.Models;
using DevHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace DevHub.Components;

public partial class InteractiveConsole : IDisposable
{
    [Inject] ShellSessionService Sessions { get; set; } = default!;
    [Inject] IJSRuntime JS { get; set; } = default!;
    [Inject] IDialogService DialogService { get; set; } = default!;
    [Inject] FolderPickerService FolderPicker { get; set; } = default!;

    [Parameter] public string? RepoPath { get; set; }

    private ShellSession? _session;
    private string _currentPath = string.Empty;
    private string _filter = string.Empty;
    private string _inputText = string.Empty;
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    private bool _autoScroll = true;
    private ElementReference _outputDiv;
    private ElementReference _inputEl;

    private IReadOnlyList<ConsoleLine> FilteredLines
    {
        get
        {
            var lines = _session?.GetLines() ?? [];
            if (string.IsNullOrWhiteSpace(_filter)) return lines;
            return lines.Where(l => l.Text.Contains(_filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrEmpty(RepoPath) || RepoPath == _currentPath) return;
        await AttachSessionAsync(RepoPath);
    }

    private async Task AttachSessionAsync(string repoPath)
    {
        DetachSession();
        _currentPath = repoPath;
        _filter = string.Empty;
        _autoScroll = true;

        _session = Sessions.GetOrCreate(repoPath);
        _session.LineAdded += OnLineAdded;
        _session.Exited += OnShellExited;

        await InvokeAsync(StateHasChanged);
        await ScrollToBottomAsync();
    }

    // ── Public API for parent ────────────────────────────────────────────────

    public void InjectCommand(string command)
    {
        _inputText = command;
        _autoScroll = true;
        StateHasChanged();
        _ = FocusInputAsync();
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    private async Task ExecuteCommandAsync()
    {
        if (_session is null || string.IsNullOrWhiteSpace(_inputText) || _session.HasExited)
            return;

        var cmd = _inputText.Trim();
        _inputText = string.Empty;

        if (_history.Count == 0 || _history[^1] != cmd)
            _history.Add(cmd);
        _historyIndex = _history.Count;

        TrackCdLocally(cmd);

        _autoScroll = true;
        await _session.SendAsync(cmd);
        await InvokeAsync(StateHasChanged);
        await ScrollToBottomAsync();
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Enter":
                await ExecuteCommandAsync();
                break;

            case "ArrowUp" when _history.Count > 0:
                _historyIndex = Math.Max(0, _historyIndex - 1);
                _inputText = _history[_historyIndex];
                StateHasChanged();
                break;

            case "ArrowDown":
                _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
                _inputText = _historyIndex < _history.Count ? _history[_historyIndex] : string.Empty;
                StateHasChanged();
                break;
        }
    }

    // ── Folder picker ─────────────────────────────────────────────────────────

    private async Task OpenFolderPickerAsync()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var parameters = new DialogParameters { ["InitialPath"] = _currentPath };
        var dialog = await DialogService.ShowAsync<FolderPickerDialog>("Cambiar directorio", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: string path })
            InjectCommand(OperatingSystem.IsWindows() ? $"cd \"{path}\"" : $"cd '{path}'");
    }

    // ── Shell lifecycle ───────────────────────────────────────────────────────

    private async Task ToggleShellAsync()
    {
        if (_session?.HasExited == true)
        {
            DetachSession();
            Sessions.Kill(_currentPath);
            await AttachSessionAsync(_currentPath);
        }
        else
        {
            Sessions.Kill(_currentPath);
            DetachSession();
            await InvokeAsync(StateHasChanged);
        }
    }

    private void ClearConsole()
    {
        _session?.ClearLines();
        StateHasChanged();
    }

    // ── Scroll ────────────────────────────────────────────────────────────────

    private async Task OnOutputScrolled()
    {
        try
        {
            var atBottom = await JS.InvokeAsync<bool>("devhubConsole.isScrolledToBottom", _outputDiv);
            if (_autoScroll && !atBottom)
                _autoScroll = false;
        }
        catch { }
    }

    private async Task ResumeAutoScrollAsync()
    {
        _autoScroll = true;
        await ScrollToBottomAsync();
    }

    private async Task ScrollToBottomAsync()
    {
        try { await JS.InvokeVoidAsync("devhubConsole.scrollToBottom", _outputDiv); }
        catch { }
    }

    private async Task FocusInputAsync()
    {
        try { await JS.InvokeVoidAsync("devhubConsole.focusElement", _inputEl); }
        catch { }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private async void OnLineAdded(ConsoleLine _)
    {
        try
        {
            await InvokeAsync(StateHasChanged);
            if (_autoScroll) await ScrollToBottomAsync();
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    private async void OnShellExited()
    {
        try { await InvokeAsync(StateHasChanged); }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void TrackCdLocally(string cmd)
    {
        // Keep _currentPath in sync for the folder button display
        if (!cmd.StartsWith("cd ", StringComparison.OrdinalIgnoreCase) &&
            !cmd.Equals("cd", StringComparison.OrdinalIgnoreCase))
            return;

        var parts = cmd.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return;

        var target = parts[1].Trim('"', '\'');
        try
        {
            var resolved = Path.IsPathRooted(target)
                ? target
                : Path.GetFullPath(Path.Combine(_currentPath, target));

            if (Directory.Exists(resolved))
                _currentPath = resolved;
        }
        catch { }
    }

    private void DetachSession()
    {
        if (_session is null) return;
        _session.LineAdded -= OnLineAdded;
        _session.Exited -= OnShellExited;
        _session = null;
    }

    private static string LineStyle(ConsoleLine line) =>
        line.Kind switch
        {
            ConsoleLineKind.Input  => "opacity:0.85; padding:1px 0",
            ConsoleLineKind.Error  => "color:#fca5a5; padding:1px 0",
            ConsoleLineKind.System => "color:#44445a; font-style:italic; padding:1px 0",
            _                      => "padding:1px 0",
        };

    public void Dispose() => DetachSession();
}
