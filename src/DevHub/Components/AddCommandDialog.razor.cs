using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DevHub.Components;

public partial class AddCommandDialog
{
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;

    private string _name = string.Empty;
    private string _command = string.Empty;
    private string _icon = "terminal";

    internal static readonly (string Key, string Icon, string Label)[] IconOptions =
    [
        ("terminal",  Icons.Material.Filled.Terminal,     "Terminal"),
        ("play",      Icons.Material.Filled.PlayArrow,    "Run"),
        ("rocket",    Icons.Material.Filled.RocketLaunch, "Deploy"),
        ("build",     Icons.Material.Filled.Build,        "Build"),
        ("bug",       Icons.Material.Filled.BugReport,    "Test"),
        ("code",      Icons.Material.Filled.Code,         "Code"),
        ("settings",  Icons.Material.Filled.Settings,     "Config"),
        ("database",  Icons.Material.Filled.Storage,      "DB"),
        ("web",       Icons.Material.Filled.Language,     "Web"),
        ("refresh",   Icons.Material.Filled.Refresh,      "Sync"),
        ("star",      Icons.Material.Filled.Star,         "Fav"),
        ("api",       Icons.Material.Filled.Api,          "API"),
    ];

    private void Confirm() =>
        MudDialog.Close(DialogResult.Ok((_name.Trim(), _command.Trim(), _icon)));

    private void Cancel() => MudDialog.Cancel();
}
