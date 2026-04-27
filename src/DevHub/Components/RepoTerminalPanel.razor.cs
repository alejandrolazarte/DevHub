using DevHub.Models;
using DevHub.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DevHub.Components;

public partial class RepoTerminalPanel
{
    [Inject] RepoCommandsService CommandsService { get; set; } = default!;
    [Inject] CustomCommandService CustomCommandService { get; set; } = default!;
    [Inject] HiddenCommandService HiddenCommandService { get; set; } = default!;
    [Inject] IDialogService DialogService { get; set; } = default!;

    private bool _open;
    private bool _loading;
    private bool _compact;
    private RepoInfo? _repo;
    private IReadOnlyList<ProjectCommand> _autoCommands = [];
    private List<CustomRepoCommand> _customCommands = [];
    private InteractiveConsole? _console;

    public async Task OpenForRepoAsync(RepoInfo repo)
    {
        _repo = repo;
        _open = true;
        _loading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            await LoadCommandsAsync();
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

    private async Task LoadCommandsAsync()
    {
        if (_repo is null) return;
        _autoCommands = await CommandsService.GetAutoCommandsAsync(_repo.Path);
        _customCommands = [.. await CustomCommandService.GetByRepoAsync(_repo.Path)];
    }

    private async Task RescanAsync()
    {
        if (_repo is null) return;
        await HiddenCommandService.RestoreAllAsync(_repo.Path);
        await LoadCommandsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task HideAutoCommandAsync(ProjectCommand cmd)
    {
        if (_repo is null) return;
        await HiddenCommandService.HideAsync(_repo.Path, cmd.Name);
        _autoCommands = _autoCommands.Where(c => c.Name != cmd.Name).ToList();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenAddCommandDialogAsync()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<AddCommandDialog>("Nuevo comando", options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: (string name, string command, string icon) })
            await SaveCustomCommandAsync(name, command, icon);
    }

    private async Task SaveCustomCommandAsync(string name, string command, string icon)
    {
        if (_repo is null) return;
        await CustomCommandService.AddAsync(_repo.Path, name, command, icon);
        _customCommands = [.. await CustomCommandService.GetByRepoAsync(_repo.Path)];
        Snackbar.Add("Comando guardado.", Severity.Success);
        await InvokeAsync(StateHasChanged);
    }

    private async Task DeleteCustomAsync(int id)
    {
        await CustomCommandService.DeleteAsync(id);
        _customCommands.RemoveAll(c => c.Id == id);
        await InvokeAsync(StateHasChanged);
    }

    private void Close() => _open = false;

    private static readonly Dictionary<string, string> _iconMap = new()
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
        _iconMap.GetValueOrDefault(cmd.Icon, Icons.Material.Filled.Terminal);

    private static string GetIcon(ProjectCommand cmd)
    {
        var name = cmd.Name.ToLowerInvariant();
        if (name.Contains("run"))    return Icons.Material.Filled.PlayArrow;
        if (name.Contains("build"))  return Icons.Material.Filled.Build;
        if (name.Contains("test"))   return Icons.Material.Filled.BugReport;
        if (name.Contains("serve") || name.Contains("dev") || name.Contains("start"))
                                     return Icons.Material.Filled.RocketLaunch;
        return Icons.Material.Filled.ChevronRight;
    }
}
