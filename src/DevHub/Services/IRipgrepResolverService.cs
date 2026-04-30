namespace DevHub.Services;

public interface IRipgrepResolverService
{
    Task<string> GetRgPathAsync(CancellationToken ct = default);
}
