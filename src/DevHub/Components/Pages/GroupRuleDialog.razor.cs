using DevHub.Models;
using DevHub.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DevHub.Components.Pages;

public partial class GroupRuleDialog
{
    [CascadingParameter] private IMudDialogInstance Mud { get; set; } = default!;
    [Parameter] public GroupRule Rule { get; set; } = new();

    private GroupRule? _editingRule;
    private string _prefixesInput = string.Empty;
    private readonly string[] _availableColors = ["primary", "secondary", "tertiary", "info", "success", "warning", "error"];

    protected override void OnInitialized()
    {
        _editingRule = new GroupRule
        {
            Id = Rule.Id,
            Name = Rule.Name,
            Color = Rule.Color,
            Prefixes = [.. Rule.Prefixes]
        };
        _prefixesInput = string.Join(", ", Rule.Prefixes);
    }

    private async Task SaveAsync()
    {
        if (_editingRule == null || string.IsNullOrWhiteSpace(_editingRule.Name))
        {
            return;
        }

        _editingRule.Prefixes = _prefixesInput
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        try
        {
            if (_editingRule.Id == 0)
            {
                await GroupRuleService.CreateAsync(_editingRule);
                Snackbar.Add("Rule created.", Severity.Success);
            }
            else
            {
                await GroupRuleService.UpdateAsync(_editingRule);
                Snackbar.Add("Rule updated.", Severity.Success);
            }
            Mud.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private void Cancel() => Mud.Cancel();
}
