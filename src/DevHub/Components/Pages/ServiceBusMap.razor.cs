using DevHub.Services;
using Microsoft.AspNetCore.Components;

namespace DevHub.Components.Pages;

public partial class ServiceBusMap
{
    private bool _isRegenerating;
    private ServiceBusMapResult? _lastResult;
    private string _mapUrl = "/maps/servicebus-map.html";
    private bool _mapExists;

    protected override void OnInitialized()
    {
        RefreshMapStatus();
    }

    private void RefreshMapStatus()
    {
        var path = Path.Combine(Env.WebRootPath, "maps", "servicebus-map.html");
        _mapExists = File.Exists(path);
        _mapUrl = $"/maps/servicebus-map.html?v={DateTime.UtcNow.Ticks}";
    }

    private async Task RegenerateAsync()
    {
        _isRegenerating = true;
        try
        {
            _lastResult = await MapService.RegenerateAsync();
            RefreshMapStatus();
        }
        finally
        {
            _isRegenerating = false;
        }
    }
}
