using System.Collections.Immutable;
using DevHub.Models;

namespace DevHub.Services;

public class RepoStateStore
{
    private volatile bool _isScanning;
    private ImmutableArray<RepoInfo> _repos = [];
    private long _lastScanCompletedTicks;

    public bool IsScanning => _isScanning;
    public IReadOnlyList<RepoInfo> Repos => _repos;
    public DateTime LastScanCompleted => new DateTime(
        Interlocked.Read(ref _lastScanCompletedTicks), DateTimeKind.Utc);

    public event Action? OnStateChanged;

    public void SetScanning(bool scanning)
    {
        _isScanning = scanning;
        OnStateChanged?.Invoke();
    }

    public void SetRepos(IReadOnlyList<RepoInfo> repos, bool resetScanning = true)
    {
        _repos = [.. repos];
        if (resetScanning)
        {
            _isScanning = false;
        }

        Interlocked.Exchange(ref _lastScanCompletedTicks, DateTime.UtcNow.Ticks);
        OnStateChanged?.Invoke();
    }
}
