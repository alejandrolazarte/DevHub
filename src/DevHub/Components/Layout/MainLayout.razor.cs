using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DevHub.Components.Layout;

public partial class MainLayout
{
    private bool _drawerOpen = true;

    private void ToggleDrawer() => _drawerOpen = !_drawerOpen;

    private readonly MudTheme _theme = new()
    {
        PaletteLight = new PaletteLight { Primary = "#c084fc" },
        PaletteDark = new PaletteDark
        {
            Primary = "#c084fc",
            Background = "#0f0f14",
            Surface = "#16161f",
            AppbarBackground = "#16161f",
            DrawerBackground = "#12121a"
        }
    };
}
