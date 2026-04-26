using DevHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace DevHub.Components;

public partial class ScanCountdown : IDisposable
{
    private int _seconds;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    protected override void OnInitialized()
    {
        _seconds = DevHubOptions.Value.ScanIntervalSeconds;
        Store.OnStateChanged += OnStoreChanged;
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _ = RunAsync(_timer, _cts.Token);
    }

    private void OnStoreChanged()
    {
        if (!Store.IsScanning)
        {
            _seconds = DevHubOptions.Value.ScanIntervalSeconds;
        }
    }

    private async Task RunAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (_seconds > 0)
                {
                    _seconds--;
                }

                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on dispose
        }
    }

    public void Dispose()
    {
        Store.OnStateChanged -= OnStoreChanged;
        _cts?.Cancel();
        _cts?.Dispose();
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
