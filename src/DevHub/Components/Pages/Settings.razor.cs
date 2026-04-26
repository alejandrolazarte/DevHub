using DevHub.Models;
using DevHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MudBlazor;

namespace DevHub.Components.Pages;

public partial class Settings
{
    private IReadOnlyList<RepoCatalogEntry> _catalogEntries = [];
    private List<GroupRule> _groupRules = [];
    private HashSet<GroupRule> _selectedRules = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadCatalogAsync();
        await LoadGroupRulesAsync();
    }

    private async Task LoadCatalogAsync()
    {
        _catalogEntries = await RepoCatalog.GetReposAsync();
    }

    private async Task LoadGroupRulesAsync()
    {
        _groupRules = [.. await GroupRuleService.GetAllAsync()];
    }

    private async Task RemoveRepoAsync(string repoPath)
    {
        try
        {
            await RepoCatalog.RemoveAsync(repoPath, CancellationToken.None);
            await LoadCatalogAsync();
            Snackbar.Add("Repo removed from catalog.", Severity.Info);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task OpenCreateDialog()
    {
        var parameters = new DialogParameters<GroupRuleDialog>
        {
            { x => x.Rule, new GroupRule() }
        };
        var dlg = await DialogService.ShowAsync<GroupRuleDialog>("Add Rule", parameters);
        var result = await dlg.Result;
        if (result is { Canceled: false })
        {
            await LoadGroupRulesAsync();
        }
    }

    private async Task OpenEditDialog(GroupRule rule)
    {
        var parameters = new DialogParameters<GroupRuleDialog>
        {
            { x => x.Rule, rule }
        };
        var dlg = await DialogService.ShowAsync<GroupRuleDialog>("Edit Rule", parameters);
        var result = await dlg.Result;
        if (result is { Canceled: false })
        {
            await LoadGroupRulesAsync();
        }
    }

    private async Task DeleteRuleAsync(int id)
    {
        try
        {
            await GroupRuleService.DeleteAsync(id);
            await LoadGroupRulesAsync();
            Snackbar.Add("Rule deleted.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task MoveSelectedUpAsync()
    {
        if (_selectedRules.Count == 0)
        {
            return;
        }
        var orderedIds = _groupRules
            .OrderBy(r => r.Order)
            .Select(r => r.Id)
            .ToList();
        var firstSelected = orderedIds.IndexOf(_selectedRules.First().Id);
        if (firstSelected <= 0)
        {
            return;
        }
        (orderedIds[firstSelected - 1], orderedIds[firstSelected]) = (orderedIds[firstSelected], orderedIds[firstSelected - 1]);
        await GroupRuleService.ReorderAsync([.. orderedIds]);
        await LoadGroupRulesAsync();
    }

    private async Task MoveSelectedDownAsync()
    {
        if (_selectedRules.Count == 0)
        {
            return;
        }
        var orderedIds = _groupRules
            .OrderBy(r => r.Order)
            .Select(r => r.Id)
            .ToList();
        var lastSelected = orderedIds.IndexOf(_selectedRules.Last().Id);
        if (lastSelected >= orderedIds.Count - 1)
        {
            return;
        }
        (orderedIds[lastSelected], orderedIds[lastSelected + 1]) = (orderedIds[lastSelected + 1], orderedIds[lastSelected]);
        await GroupRuleService.ReorderAsync([.. orderedIds]);
        await LoadGroupRulesAsync();
    }
}