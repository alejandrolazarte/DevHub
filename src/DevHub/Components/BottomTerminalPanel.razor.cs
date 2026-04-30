using DevHub.Models;
using DevHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace DevHub.Components;

public partial class BottomTerminalPanel : IDisposable
{
    [Inject] TerminalPanelService PanelService { get; set; } = default!;
    [Inject] ShellSessionService Sessions { get; set; } = default!;
    [Inject] RepoCommandsService CommandsService { get; set; } = default!;
    [Inject] CustomCommandService CustomCommandService { get; set; } = default!;
    [Inject] HiddenCommandService HiddenCommandService { get; set; } = default!;
    [Inject] IDialogService DialogService { get; set; } = default!;
    [Inject] ISnackbar Snackbar { get; set; } = default!;
    [Inject] IJSRuntime JS { get; set; } = default!;

    private InteractiveConsole? _console;
    private DotNetObjectReference<BottomTerminalPanel>? _dotnetRef;
    private bool _commandsOpen;
    private bool _minimized;
    private bool _compact;
    private bool _loading;
    private int _panelHeight = 340;
    private string _cmdFilter = string.Empty;
    private string? _loadedRepoPath;
    private IReadOnlyList<ProjectCommand> _autoCommands = [];
    private List<CustomRepoCommand> _customCommands = [];

    private bool _sessionAlive =>
        Sessions.TryGet(PanelService.ActiveRepo?.Path) is { HasExited: false };

    private IEnumerable<ProjectCommand> FilteredAutoCommands =>
        string.IsNullOrWhiteSpace(_cmdFilter)
            ? _autoCommands
            : _autoCommands.Where(c => c.Name.Contains(_cmdFilter, StringComparison.OrdinalIgnoreCase)
                                    || c.Command.Contains(_cmdFilter, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<CustomRepoCommand> FilteredCustomCommands =>
        string.IsNullOrWhiteSpace(_cmdFilter)
            ? _customCommands
            : _customCommands.Where(c => c.Name.Contains(_cmdFilter, StringComparison.OrdinalIgnoreCase)
                                      || c.Command.Contains(_cmdFilter, StringComparison.OrdinalIgnoreCase));

    protected override void OnInitialized()
    {
        _dotnetRef = DotNetObjectReference.Create(this);
        PanelService.StateChanged += OnPanelStateChanged;
    }

    [JSInvokable]
    public async Task SetHeight(int height)
    {
        _panelHeight = height;
        await InvokeAsync(StateHasChanged);
    }

    private async Task StartResizeAsync(MouseEventArgs e)
    {
        if (_dotnetRef is null)
        {
            return;
        }

        await JS.InvokeVoidAsync("devhubTerminal.startResize", _dotnetRef, e.ClientY, _panelHeight);
    }

    protected override async Task OnParametersSetAsync()
    {
        var repoPath = PanelService.ActiveRepo?.Path;
        if (repoPath is null || repoPath == _loadedRepoPath)
        {
            return;
        }

        await LoadCommandsAsync(repoPath);
    }

    private async void OnPanelStateChanged()
    {
        await InvokeAsync(StateHasChanged);

        var repoPath = PanelService.ActiveRepo?.Path;
        if (repoPath is not null && repoPath != _loadedRepoPath)
        {
            await LoadCommandsAsync(repoPath);
        }
    }

    private async Task LoadCommandsAsync(string repoPath)
    {
        _loading = true;
        _loadedRepoPath = repoPath;
        _cmdFilter = string.Empty;
        await InvokeAsync(StateHasChanged);

        try
        {
            _autoCommands = await CommandsService.GetAutoCommandsAsync(repoPath);
            _customCommands = [.. await CustomCommandService.GetByRepoAsync(repoPath)];
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error cargando comandos: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void InjectCommand(string command) => _console?.InjectCommand(command);

    private void ToggleCommands() => _commandsOpen = !_commandsOpen;

    private void Close() => PanelService.Close();

    private async Task KillSessionAsync()
    {
        var path = PanelService.ActiveRepo?.Path;
        if (path is null)
        {
            return;
        }

        Sessions.Kill(path);
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnConsoleSessionChanged()
    {
        await InvokeAsync(StateHasChanged);
    }

    private async Task RescanAsync()
    {
        var path = PanelService.ActiveRepo?.Path;
        if (path is null)
        {
            return;
        }

        await HiddenCommandService.RestoreAllAsync(path);
        await LoadCommandsAsync(path);
    }

    private async Task HideAutoCommandAsync(ProjectCommand cmd)
    {
        var path = PanelService.ActiveRepo?.Path;
        if (path is null)
        {
            return;
        }

        await HiddenCommandService.HideAsync(path, cmd.Name);
        _autoCommands = _autoCommands.Where(c => c.Name != cmd.Name).ToList();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenAddCommandDialogAsync()
    {
        var path = PanelService.ActiveRepo?.Path;
        if (path is null)
        {
            return;
        }

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<AddCommandDialog>("Nuevo comando", options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: (string name, string command, string icon) })
        {
            await CustomCommandService.AddAsync(path, name, command, icon);
            _customCommands = [.. await CustomCommandService.GetByRepoAsync(path)];
            Snackbar.Add("Comando guardado.", Severity.Success);
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task DeleteCustomAsync(int id)
    {
        await CustomCommandService.DeleteAsync(id);
        _customCommands.RemoveAll(c => c.Id == id);
        await InvokeAsync(StateHasChanged);
    }

    private static readonly Dictionary<string, string> _customIconMap = new()
    {
        ["terminal"] = Icons.Material.Filled.Terminal,
        ["play"]     = Icons.Material.Filled.PlayArrow,
        ["rocket"]   = Icons.Material.Filled.RocketLaunch,
        ["build"]    = Icons.Material.Filled.Build,
        ["bug"]      = Icons.Material.Filled.BugReport,
        ["code"]     = Icons.Material.Filled.Code,
        ["settings"] = Icons.Material.Filled.Settings,
        ["database"] = Icons.Material.Filled.Storage,
        ["web"]      = Icons.Material.Filled.Language,
        ["refresh"]  = Icons.Material.Filled.Refresh,
        ["star"]     = Icons.Material.Filled.Star,
        ["api"]      = Icons.Material.Filled.Api,
    };

    private static string GetCustomIcon(CustomRepoCommand cmd) =>
        _customIconMap.GetValueOrDefault(cmd.Icon, Icons.Material.Filled.Terminal);

    private static string GetAutoIcon(ProjectCommand cmd)
    {
        var name = cmd.Name.ToLowerInvariant();

        if (name.Contains("run"))
        {
            return Icons.Material.Filled.PlayArrow;
        }

        if (name.Contains("build"))
        {
            return Icons.Material.Filled.Build;
        }

        if (name.Contains("test"))
        {
            return Icons.Material.Filled.BugReport;
        }

        if (name.Contains("serve") || name.Contains("dev") || name.Contains("start"))
        {
            return Icons.Material.Filled.RocketLaunch;
        }

        return Icons.Material.Filled.ChevronRight;
    }

    public void Dispose()
    {
        _dotnetRef?.Dispose();
        PanelService.StateChanged -= OnPanelStateChanged;
        GC.SuppressFinalize(this);
    }
}
