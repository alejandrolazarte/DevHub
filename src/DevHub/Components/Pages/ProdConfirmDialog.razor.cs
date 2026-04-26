using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DevHub.Components.Pages;

public partial class ProdConfirmDialog
{
    [CascadingParameter] private IMudDialogInstance Mud { get; set; } = default!;
    [Parameter] public string ServiceName { get; set; } = "";
    [Parameter] public string ProfileName { get; set; } = "";
    private string Typed { get; set; } = "";
    private void Confirm() => Mud.Close(DialogResult.Ok(true));
    private void Cancel() => Mud.Cancel();
}
