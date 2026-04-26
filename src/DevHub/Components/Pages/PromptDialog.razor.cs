using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace DevHub.Components.Pages;

public partial class PromptDialog
{
    [CascadingParameter] private IMudDialogInstance Mud { get; set; } = default!;
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string HelperText { get; set; } = "";
    [Parameter] public string Value { get; set; } = "";

    private void Submit() => Mud.Close(DialogResult.Ok(Value));
    private void Cancel() => Mud.Cancel();

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(Value))
        {
            Submit();
        }
    }
}
