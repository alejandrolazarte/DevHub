using DevHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DevHub.Components;

public partial class UpdateBanner
{
    private bool _visible;

    protected override void OnInitialized() => _visible = VersionSvc.IsUpdated;

    private void Dismiss() => _visible = false;

    private async Task Relaunch() => await JS.InvokeVoidAsync("eval", "location.reload()");
}
