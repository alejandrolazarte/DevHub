using DevHub.Models;
using Microsoft.AspNetCore.Components;

namespace DevHub.Components;

public partial class FilterBar
{
    [Parameter] public IReadOnlyList<string> Groups { get; set; } = [];
    [Parameter] public EventCallback<FilterCriteria> OnFilterChanged { get; set; }

    private string SearchText { get; set; } = string.Empty;
    private string SelectedGroup { get; set; } = string.Empty;
    private string SelectedStatus { get; set; } = string.Empty;

    private void OnSearchChanged(string value)
    {
        SearchText = value;
        NotifyFilterChanged();
    }

    private void OnGroupChanged(string value)
    {
        SelectedGroup = value;
        NotifyFilterChanged();
    }

    private void OnStatusChanged(string value)
    {
        SelectedStatus = value;
        NotifyFilterChanged();
    }

    private void NotifyFilterChanged() =>
        OnFilterChanged.InvokeAsync(new FilterCriteria(SearchText, SelectedGroup, SelectedStatus));
}
