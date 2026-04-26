using DevHub.Models;
using Microsoft.AspNetCore.Components;

namespace DevHub.Components;

public partial class RepoList
{
    [Parameter, EditorRequired] public IReadOnlyList<RepoGroup> Groups { get; set; } = [];
    [Parameter] public HashSet<string> SelectedPaths { get; set; } = [];
    [Parameter] public EventCallback<HashSet<string>> SelectedPathsChanged { get; set; }
    [Parameter] public EventCallback<RepoInfo> OnRemoveRequested { get; set; }

    private readonly HashSet<string> _collapsedGroups = [];
    private readonly HashSet<string> _manuallyExpanded = [];

    protected override void OnParametersSet()
    {
        foreach (var group in Groups.Where(g => g.AutoCollapse && !_manuallyExpanded.Contains(g.Name)))
        {
            _collapsedGroups.Add(group.Name);
        }
    }

    private void ToggleGroup(string groupName)
    {
        var isExpanded = _manuallyExpanded.Contains(groupName);
        if (isExpanded)
        {
            _manuallyExpanded.Remove(groupName);
            _collapsedGroups.Add(groupName);
        }
        else
        {
            _manuallyExpanded.Add(groupName);
            _collapsedGroups.Remove(groupName);
        }
    }

    private async Task OnRepoSelectionChanged(RepoInfo repo, bool selected)
    {
        if (selected)
        {
            SelectedPaths.Add(repo.Path);
        }
        else
        {
            SelectedPaths.Remove(repo.Path);
        }

        await SelectedPathsChanged.InvokeAsync(SelectedPaths);
    }
}
