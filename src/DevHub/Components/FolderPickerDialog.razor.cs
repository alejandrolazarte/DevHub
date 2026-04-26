using DevHub.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DevHub.Components;

public partial class FolderPickerDialog
{
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public string InitialPath { get; set; } = string.Empty;

    [Inject] FolderPickerService FolderPicker { get; set; } = default!;

    private string _currentPath = string.Empty;
    private IReadOnlyList<string> _subDirs = [];
    private IReadOnlyList<string> _drives = [];

    protected override void OnInitialized()
    {
        _currentPath = InitialPath;
        Refresh();
    }

    private bool IsAtDrivesView => string.IsNullOrEmpty(_currentPath);

    private void NavigateTo(string path)
    {
        _currentPath = path;
        Refresh();
    }

    private void NavigateToDrives()
    {
        _currentPath = string.Empty;
        Refresh();
    }

    private void GoUp()
    {
        var parent = FolderPicker.GetParent(_currentPath);
        if (parent is not null)
        {
            NavigateTo(parent);
        }
        else
        {
            NavigateToDrives();
        }
    }

    private void Refresh()
    {
        if (IsAtDrivesView)
        {
            _drives = FolderPicker.GetDrives();
            _subDirs = [];
        }
        else
        {
            _drives = [];
            _subDirs = FolderPicker.GetSubDirectories(_currentPath);
        }
    }

    private IEnumerable<(string Segment, string Path)> GetBreadcrumbs()
    {
        if (string.IsNullOrEmpty(_currentPath))
        {
            yield break;
        }

        var parts = _currentPath.Split(
            [System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        var accumulated = OperatingSystem.IsWindows() ? string.Empty : "/";
        foreach (var part in parts)
        {
            accumulated = System.IO.Path.Combine(accumulated, part);
            yield return (part, accumulated);
        }
    }

    private void Confirm() => MudDialog.Close(DialogResult.Ok(_currentPath));
    private void Cancel() => MudDialog.Cancel();
}