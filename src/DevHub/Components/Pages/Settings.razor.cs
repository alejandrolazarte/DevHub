using DevHub.Helpers;
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
        if (await Snackbar.TryAsync(
            () => RepoCatalog.RemoveAsync(repoPath, CancellationToken.None),
            "Repo removed from catalog.",
            Severity.Info))
        {
            await LoadCatalogAsync();
        }
    }

    private async Task OpenCreateDialog()
    {
        var parameters = new DialogParameters<GroupRuleDialog>
        {
            { x => x.Rule, new GroupRule() }
        };
        if (await DialogService.ShowAndWaitAsync<GroupRuleDialog>("Add Rule", parameters))
            await LoadGroupRulesAsync();
    }

    private async Task OpenEditDialog(GroupRule rule)
    {
        var parameters = new DialogParameters<GroupRuleDialog>
        {
            { x => x.Rule, rule }
        };
        if (await DialogService.ShowAndWaitAsync<GroupRuleDialog>("Edit Rule", parameters))
            await LoadGroupRulesAsync();
    }

    private async Task DeleteRuleAsync(int id)
    {
        if (await Snackbar.TryAsync(
            () => GroupRuleService.DeleteAsync(id),
            "Rule deleted."))
        {
            await LoadGroupRulesAsync();
        }
    }

    private async Task MoveSelectedUpAsync()
    {
        if (_selectedRules.Count == 0)
            return;

        var orderedIds = _groupRules
            .OrderBy(r => r.Order)
            .Select(r => r.Id)
            .ToList();
        var firstSelected = orderedIds.IndexOf(_selectedRules.First().Id);
        if (firstSelected <= 0)
            return;

        (orderedIds[firstSelected - 1], orderedIds[firstSelected]) = (orderedIds[firstSelected], orderedIds[firstSelected - 1]);
        await GroupRuleService.ReorderAsync([.. orderedIds]);
        await LoadGroupRulesAsync();
    }

    private async Task MoveSelectedDownAsync()
    {
        if (_selectedRules.Count == 0)
            return;

        var orderedIds = _groupRules
            .OrderBy(r => r.Order)
            .Select(r => r.Id)
            .ToList();
        var lastSelected = orderedIds.IndexOf(_selectedRules.Last().Id);
        if (lastSelected >= orderedIds.Count - 1)
            return;

        (orderedIds[lastSelected], orderedIds[lastSelected + 1]) = (orderedIds[lastSelected + 1], orderedIds[lastSelected]);
        await GroupRuleService.ReorderAsync([.. orderedIds]);
        await LoadGroupRulesAsync();
    }
}
