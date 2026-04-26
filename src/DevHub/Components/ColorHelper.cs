using MudBlazor;

namespace DevHub.Components;

public static class ColorHelper
{
    public static Color FromGroupColor(string color) => color.ToLowerInvariant() switch
    {
        "primary" => Color.Primary,
        "secondary" => Color.Secondary,
        "tertiary" => Color.Tertiary,
        "info" => Color.Info,
        "success" => Color.Success,
        "warning" => Color.Warning,
        "error" => Color.Error,
        _ => Color.Default
    };
}
