using DevHub.Models;
using DevHub.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DevHub.Components;

public partial class BulkActions
{
    [Parameter, EditorRequired] public IReadOnlyList<RepoInfo> SelectedRepos { get; set; } = [];
    [Parameter] public IReadOnlyList<RepoInfo> AllRepos { get; set; } = [];
    [Parameter] public EventCallback<bool> OnSelectAllChanged { get; set; }
    [Parameter] public EventCallback OnOperationCompleted { get; set; }

    private bool AllSelected => AllRepos.Any() && SelectedRepos.Count == AllRepos.Count;
    private bool _busy;
    private bool _checkoutDialogVisible;
    private bool _loadingBranches;

    private string _checkoutBranch = string.Empty;
    private string _newBranchInput = string.Empty;
    private List<string> _existingBranches = [];

    private async Task PullSelected()
    {
        _busy = true;
        try
        {
            int ok = 0, failed = 0;
            foreach (var repo in SelectedRepos)
            {
                var (success, error) = await GitService.PullAsync(repo.Path);
                if (success)
                {
                    ok++;
                }
                else
                {
                    failed++;
                    Snackbar.Add($"{repo.Name}: {error}", Severity.Error);
                }
            }
            Snackbar.Add($"Pull: {ok} OK, {failed} errores", failed > 0 ? Severity.Warning : Severity.Success);
        }
        finally
        {
            _busy = false;
        }
        await OnOperationCompleted.InvokeAsync();
    }

    private async Task ShowCheckoutDialog()
    {
        _checkoutBranch = string.Empty;
        _newBranchInput = string.Empty;
        _existingBranches = [];
        _checkoutDialogVisible = true;
        _loadingBranches = true;

        var branchSets = await Task.WhenAll(
            SelectedRepos.Select(r => GitService.GetBranchesAsync(r.Path)));

        _existingBranches = branchSets
            .SelectMany(b => b)
            .GroupBy(b => b)
            .OrderByDescending(g => g.Key is "master" or "main")
            .ThenByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .ToList();

        _loadingBranches = false;
    }

    private void SelectBranch(string branch)
    {
        _checkoutBranch = branch;
        _newBranchInput = string.Empty;
    }

    private void CloseDialog()
    {
        _checkoutDialogVisible = false;
        _checkoutBranch = string.Empty;
        _newBranchInput = string.Empty;
    }

    private async Task CheckoutSelected()
    {
        if (string.IsNullOrWhiteSpace(_checkoutBranch))
        {
            return;
        }
        _busy = true;
        try
        {
            int ok = 0, failed = 0;
            foreach (var repo in SelectedRepos)
            {
                var (success, error) = await GitService.CheckoutAsync(repo.Path, _checkoutBranch);
                if (success)
                {
                    ok++;
                }
                else
                {
                    failed++;
                    Snackbar.Add($"{repo.Name}: {error}", Severity.Error);
                }
            }
            Snackbar.Add($"Checkout '{_checkoutBranch}': {ok} OK, {failed} errores",
                failed > 0 ? Severity.Warning : Severity.Success);
            CloseDialog();
        }
        finally
        {
            _busy = false;
        }
        await OnOperationCompleted.InvokeAsync();
    }
}
