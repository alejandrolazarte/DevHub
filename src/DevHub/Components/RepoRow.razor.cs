using System.Globalization;
using DevHub.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace DevHub.Components;

public partial class RepoRow
{
    [Parameter, EditorRequired] public RepoInfo Repo { get; set; } = default!;
    [Parameter] public bool Selected { get; set; }
    [Parameter] public EventCallback<bool> SelectedChanged { get; set; }
    [Parameter] public EventCallback OnRemoveRequested { get; set; }

    private Color BranchColor => Repo.Branch is "master" or "main"
        ? Color.Success
        : Color.Warning;

    private string BuildCommitTooltip()
    {
        if (Repo.LastCommitDate == DateTime.MinValue)
        {
            return Repo.LastCommitMessage;
        }

        if (string.IsNullOrWhiteSpace(Repo.LastCommitMessage))
        {
            return Repo.LastCommitDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return $"{Repo.LastCommitMessage} · {Repo.LastCommitDate:yyyy-MM-dd HH:mm:ss}";
    }

    private static string ToRelativeTime(DateTime date)
    {
        if (date == DateTime.MinValue)
        {
            return "—";
        }

        var diff = DateTime.Now - date;
        if (diff.TotalMinutes < 1)
        {
            return "just now";
        }
        if (diff.TotalHours < 1)
        {
            return $"{(int)diff.TotalMinutes}m ago";
        }
        if (diff.TotalDays < 1)
        {
            return $"{(int)diff.TotalHours}h ago";
        }
        if (diff.TotalDays < 30)
        {
            return $"{(int)diff.TotalDays}d ago";
        }

        return date.ToString("MMM yyyy", CultureInfo.InvariantCulture);
    }

    private async Task OpenPullRequest()
    {
        await JS.InvokeVoidAsync("window.open", Repo.PrUrl, "_blank");
    }

    private async Task CopyPath()
    {
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", Repo.Path);
        Snackbar.Add($"Ruta copiada: {Repo.Path}", Severity.Info, cfg => cfg.VisibleStateDuration = 2000);
    }

    private Task RemoveFromCatalog()
    {
        return OnRemoveRequested.InvokeAsync();
    }
}
