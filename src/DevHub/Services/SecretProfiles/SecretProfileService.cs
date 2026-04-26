using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DevHub.Services.SecretProfiles;

public partial class SecretProfileService(
    IFileSystem fs,
    IOptions<SecretProfileOptions> options,
    IHostEnvironment env,
    ILogger<SecretProfileService> logger)
{
    private readonly SecretProfileOptions _options = options.Value;

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$")]
    private static partial Regex ValidProfileName();

    public Task<IReadOnlyList<ServiceProfileView>> GetServicesAsync(CancellationToken ct)
    {
        IReadOnlyList<ServiceProfileView> list = _options.Services
            .Select(s => new ServiceProfileView(s.Name, s.UserSecretsId))
            .ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<ProfileInfo>> GetProfilesAsync(string serviceName, CancellationToken ct)
    {
        var cfg = GetConfig(serviceName);
        var dir = GetProfilesDir(cfg);

        IReadOnlyList<ProfileInfo> list = [];
        if (!fs.DirectoryExists(dir))
        {
            return Task.FromResult(list);
        }

        list = fs.EnumerateFiles(dir, "*.json")
            .Select(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                return new ProfileInfo(
                    Name: name,
                    IsProd: IsProd(cfg, name),
                    SizeBytes: fs.GetFileSize(path),
                    ModifiedUtc: fs.GetLastWriteTimeUtc(path));
            })
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(list);
    }

    public async Task<ActiveProfileInfo> GetActiveProfileAsync(string serviceName, CancellationToken ct)
    {
        var cfg = GetConfig(serviceName);
        var livePath = GetLiveSecretsPath(cfg);

        if (!fs.FileExists(livePath))
        {
            return new ActiveProfileInfo(null, IsDirty: false, IsProd: false, LiveSecretsExists: false);
        }

        var liveHash = await HashAsync(livePath, ct);
        var dir = GetProfilesDir(cfg);
        if (!fs.DirectoryExists(dir))
        {
            return new ActiveProfileInfo(null, IsDirty: true, IsProd: false, LiveSecretsExists: true);
        }

        foreach (var profilePath in fs.EnumerateFiles(dir, "*.json"))
        {
            var profileHash = await HashAsync(profilePath, ct);
            if (profileHash.SequenceEqual(liveHash))
            {
                var name = Path.GetFileNameWithoutExtension(profilePath);
                return new ActiveProfileInfo(name, IsDirty: false, IsProd: IsProd(cfg, name), LiveSecretsExists: true);
            }
        }

        return new ActiveProfileInfo(null, IsDirty: true, IsProd: false, LiveSecretsExists: true);
    }

    public async Task<string> ReadProfileContentAsync(string serviceName, string profileName, CancellationToken ct)
    {
        var cfg = GetConfig(serviceName);
        EnsureValidName(profileName);
        var path = Path.Combine(GetProfilesDir(cfg), profileName + ".json");
        if (!fs.FileExists(path))
        {
            throw new ProfileNotFoundException(profileName);
        }

        var bytes = await fs.ReadAllBytesAsync(path, ct);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public async Task<string> ReadActiveContentAsync(string serviceName, CancellationToken ct)
    {
        var cfg = GetConfig(serviceName);
        var path = GetLiveSecretsPath(cfg);
        if (!fs.FileExists(path))
        {
            return "";
        }

        var bytes = await fs.ReadAllBytesAsync(path, ct);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public async Task CaptureAsync(string serviceName, string profileName, CancellationToken ct)
    {
        var cfg = GetConfig(serviceName);
        EnsureValidName(profileName);

        var livePath = GetLiveSecretsPath(cfg);
        if (!fs.FileExists(livePath))
        {
            throw new LiveSecretsMissingException(cfg.UserSecretsId);
        }

        var dir = GetProfilesDir(cfg);
        if (!fs.DirectoryExists(dir))
        {
            fs.CreateDirectory(dir);
        }

        var bytes = await fs.ReadAllBytesAsync(livePath, ct);
        var destPath = Path.Combine(dir, profileName + ".json");
        await fs.WriteAllBytesAsync(destPath, bytes, ct);

        LogCaptured(logger, profileName, serviceName, bytes.Length);
    }

    public async Task ApplyAsync(string serviceName, string profileName, bool prodConfirmed, CancellationToken ct)
    {
        var cfg = GetConfig(serviceName);
        EnsureValidName(profileName);

        if (IsProd(cfg, profileName) && !prodConfirmed)
        {
            throw new ProdConfirmationRequiredException(profileName);
        }

        var profilePath = Path.Combine(GetProfilesDir(cfg), profileName + ".json");
        if (!fs.FileExists(profilePath))
        {
            throw new ProfileNotFoundException(profileName);
        }

        var livePath = GetLiveSecretsPath(cfg);
        var liveDir = Path.GetDirectoryName(livePath)!;
        if (!fs.DirectoryExists(liveDir))
        {
            fs.CreateDirectory(liveDir);
        }

        var bytes = await fs.ReadAllBytesAsync(profilePath, ct);
        var tmpPath = livePath + ".tmp";
        await fs.WriteAllBytesAsync(tmpPath, bytes, ct);
        fs.Move(tmpPath, livePath, overwrite: true);

        LogApplied(logger, profileName, serviceName);
    }

    public async Task SaveAsync(string serviceName, string profileName, string content, CancellationToken ct)
    {
        var cfg = GetConfig(serviceName);
        EnsureValidName(profileName);

        var profilePath = Path.Combine(GetProfilesDir(cfg), profileName + ".json");
        if (!fs.FileExists(profilePath))
        {
            throw new ProfileNotFoundException(profileName);
        }

        var active = await GetActiveProfileAsync(serviceName, ct);
        var wasActive = string.Equals(active.MatchedProfileName, profileName, StringComparison.OrdinalIgnoreCase);

        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        await fs.WriteAllBytesAsync(profilePath, bytes, ct);

        if (wasActive)
        {
            var livePath = GetLiveSecretsPath(cfg);
            var tmpPath = livePath + ".tmp";
            await fs.WriteAllBytesAsync(tmpPath, bytes, ct);
            fs.Move(tmpPath, livePath, overwrite: true);
        }

        LogSaved(logger, profileName, serviceName, wasActive);
    }

    public async Task FormatAsync(string serviceName, string profileName, CancellationToken ct)
    {
        var cfg = GetConfig(serviceName);
        EnsureValidName(profileName);

        var profilePath = Path.Combine(GetProfilesDir(cfg), profileName + ".json");
        if (!fs.FileExists(profilePath))
        {
            throw new ProfileNotFoundException(profileName);
        }

        var active = await GetActiveProfileAsync(serviceName, ct);
        var wasActive = string.Equals(active.MatchedProfileName, profileName, StringComparison.OrdinalIgnoreCase);

        var rawBytes = await fs.ReadAllBytesAsync(profilePath, ct);
        var raw = System.Text.Encoding.UTF8.GetString(rawBytes);
        var formatted = JsonPrettyFormatter.Format(raw);
        var newBytes = System.Text.Encoding.UTF8.GetBytes(formatted);

        await fs.WriteAllBytesAsync(profilePath, newBytes, ct);

        if (wasActive)
        {
            var livePath = GetLiveSecretsPath(cfg);
            var tmpPath = livePath + ".tmp";
            await fs.WriteAllBytesAsync(tmpPath, newBytes, ct);
            fs.Move(tmpPath, livePath, overwrite: true);
        }

        LogFormatted(logger, profileName, serviceName, wasActive);
    }

    public async Task DeleteAsync(string serviceName, string profileName, CancellationToken ct)
    {
        var cfg = GetConfig(serviceName);
        EnsureValidName(profileName);

        var active = await GetActiveProfileAsync(serviceName, ct);
        if (string.Equals(active.MatchedProfileName, profileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new CannotDeleteActiveProfileException(profileName);
        }

        var path = Path.Combine(GetProfilesDir(cfg), profileName + ".json");
        if (!fs.FileExists(path))
        {
            throw new ProfileNotFoundException(profileName);
        }

        fs.Delete(path);
        LogDeleted(logger, profileName, serviceName);
    }

    private SecretProfileServiceConfig GetConfig(string serviceName) =>
        _options.Services.FirstOrDefault(s => string.Equals(s.Name, serviceName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ServiceNotConfiguredException(serviceName);

    private string GetProfilesDir(SecretProfileServiceConfig cfg) =>
        Path.GetFullPath(Path.Combine(env.ContentRootPath, _options.ProfilesRoot, cfg.Name));

    private static string GetLiveSecretsPath(SecretProfileServiceConfig cfg)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Microsoft", "UserSecrets", cfg.UserSecretsId, "secrets.json");
    }

    private static bool IsProd(SecretProfileServiceConfig cfg, string profileName) =>
        cfg.ProdProfileNames.Any(p => string.Equals(p, profileName, StringComparison.OrdinalIgnoreCase));

    private static void EnsureValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !ValidProfileName().IsMatch(name))
        {
            throw new InvalidProfileNameException(name);
        }
    }

    private async Task<byte[]> HashAsync(string path, CancellationToken ct)
    {
        var bytes = await fs.ReadAllBytesAsync(path, ct);
        return SHA256.HashData(bytes);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Captured profile '{Profile}' for service '{Service}' ({Bytes} bytes)")]
    private static partial void LogCaptured(ILogger logger, string profile, string service, int bytes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied profile '{Profile}' to service '{Service}'")]
    private static partial void LogApplied(ILogger logger, string profile, string service);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved profile '{Profile}' for service '{Service}' (live-synced: {Synced})")]
    private static partial void LogSaved(ILogger logger, string profile, string service, bool synced);

    [LoggerMessage(Level = LogLevel.Information, Message = "Formatted profile '{Profile}' for service '{Service}' (live-synced: {Synced})")]
    private static partial void LogFormatted(ILogger logger, string profile, string service, bool synced);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted profile '{Profile}' for service '{Service}'")]
    private static partial void LogDeleted(ILogger logger, string profile, string service);
}
