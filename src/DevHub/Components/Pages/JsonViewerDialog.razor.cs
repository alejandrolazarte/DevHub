using BlazorMonaco.Editor;
using DevHub.Services.SecretProfiles;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace DevHub.Components.Pages;

public partial class JsonViewerDialog
{
    [CascadingParameter] private IMudDialogInstance Mud { get; set; } = default!;
    [Parameter] public string Content { get; set; } = "";
    [Parameter] public string? ServiceName { get; set; }
    [Parameter] public string? ProfileName { get; set; }

    private StandaloneCodeEditor? _editor;
    private bool _busy;
    private bool _editMode;

    private bool CanEdit => !string.IsNullOrEmpty(ServiceName) && !string.IsNullOrEmpty(ProfileName);

    private StandaloneEditorConstructionOptions Options(StandaloneCodeEditor _) => new()
    {
        Language = "json",
        Theme = "vs-dark",
        ReadOnly = true,
        AutomaticLayout = true,
        Minimap = new EditorMinimapOptions { Enabled = false },
        Value = Content,
    };

    private async Task OnEditModeChanged(bool value)
    {
        _editMode = value;
        if (_editor is null)
        {
            return;
        }
        await _editor.UpdateOptions(new EditorUpdateOptions { ReadOnly = !value });
    }

    private async Task OnInit()
    {
        if (_editor is null)
        {
            return;
        }
        await Task.Delay(80);
        await _editor.Layout();
    }

    private async Task FormatAsync()
    {
        if (_editor is null)
        {
            return;
        }
        var current = await _editor.GetValue();
        var formatted = JsonPrettyFormatter.Format(current);
        if (formatted == current)
        {
            Snackbar.Add("Ya estaba formateado.", Severity.Info);
            return;
        }
        await _editor.SetValue(formatted);
        Snackbar.Add("Formateado en el editor. Pulsa Guardar para persistir.", Severity.Info);
    }

    private async Task SaveAsync()
    {
        if (!CanEdit || _editor is null)
        {
            return;
        }
        _busy = true;
        try
        {
            var content = await _editor.GetValue();
            await ProfileService.SaveAsync(ServiceName!, ProfileName!, content, CancellationToken.None);
            Snackbar.Add($"Perfil '{ProfileName}' guardado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    private void Close() => Mud.Close();
}
