using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace DevHub.Components.Pages;

public partial class JsonDiffDialog
{
    [CascadingParameter] private IMudDialogInstance Mud { get; set; } = default!;
    [Parameter] public string Original { get; set; } = "";
    [Parameter] public string Modified { get; set; } = "";

    private StandaloneDiffEditor? _editor;

    private static StandaloneDiffEditorConstructionOptions Options(StandaloneDiffEditor _) => new()
    {
        Theme = "vs-dark",
        ReadOnly = true,
        AutomaticLayout = true,
        RenderSideBySide = true,
        OriginalEditable = false,
    };

    private async Task OnInit()
    {
        if (_editor is null)
        {
            return;
        }
        var original = await BlazorMonaco.Editor.Global.CreateModel(JS, Original, "json");
        var modified = await BlazorMonaco.Editor.Global.CreateModel(JS, Modified, "json");
        await _editor.SetModel(new DiffEditorModel { Original = original, Modified = modified });
        await Task.Delay(80);
        await _editor.Layout();
    }

    private void Close() => Mud.Close();
}
