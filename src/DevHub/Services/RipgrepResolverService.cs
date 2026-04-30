using System.IO.Compression;
using System.Runtime.InteropServices;

namespace DevHub.Services;

public partial class RipgrepResolverService(
    IProcessRunner runner,
    IWebHostEnvironment env,
    ILogger<RipgrepResolverService> logger) : IRipgrepResolverService, IDisposable
{
    private const string Version = "14.1.1";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = true });
    private string? _resolvedPath;

    public async Task<string> GetRgPathAsync(CancellationToken ct = default)
    {
        if (_resolvedPath is not null)
        {
            return _resolvedPath;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_resolvedPath is not null)
            {
                return _resolvedPath;
            }

            _resolvedPath = await ResolveAsync(ct);
            return _resolvedPath;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string> ResolveAsync(CancellationToken ct)
    {
        if (await IsWorkingAsync("rg", ct))
        {
            LogFoundOnPath(logger);
            return "rg";
        }

        var localPath = LocalBinaryPath();
        if (File.Exists(localPath) && await IsWorkingAsync(localPath, ct))
        {
            LogFoundLocal(logger, localPath);
            return localPath;
        }

        LogDownloading(logger, Version);
        await DownloadAsync(localPath, ct);
        return localPath;
    }

    private async Task<bool> IsWorkingAsync(string binaryPath, CancellationToken ct)
    {
        try
        {
            var result = await runner.RunAsync(binaryPath, "--version", ct: ct);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private string LocalBinaryPath()
    {
        var toolsDir = Path.Combine(env.ContentRootPath, "tools");
        Directory.CreateDirectory(toolsDir);
        var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "rg.exe" : "rg";
        return Path.Combine(toolsDir, binaryName);
    }

    private async Task DownloadAsync(string targetPath, CancellationToken ct)
    {
        var (downloadUrl, isZip) = GetDownloadUrl();
        LogDownloadUrl(logger, downloadUrl);

        var tempFile = Path.GetTempFileName() + (isZip ? ".zip" : ".tar.gz");
        try
        {
            var bytes = await _http.GetByteArrayAsync(downloadUrl, ct);
            await File.WriteAllBytesAsync(tempFile, bytes, ct);

            if (isZip)
            {
                ExtractFromZip(tempFile, targetPath);
            }
            else
            {
                await ExtractFromTarGzAsync(tempFile, targetPath, ct);
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await runner.RunAsync("chmod", $"+x \"{targetPath}\"", ct: ct);
            }

            LogInstalled(logger, targetPath);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static (string Url, bool IsZip) GetDownloadUrl()
    {
        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "aarch64" : "x86_64";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ($"https://github.com/BurntSushi/ripgrep/releases/download/{Version}/ripgrep-{Version}-{arch}-pc-windows-msvc.zip", true);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ($"https://github.com/BurntSushi/ripgrep/releases/download/{Version}/ripgrep-{Version}-{arch}-apple-darwin.tar.gz", false);
        }

        return ($"https://github.com/BurntSushi/ripgrep/releases/download/{Version}/ripgrep-{Version}-{arch}-unknown-linux-musl.tar.gz", false);
    }

    private static void ExtractFromZip(string zipPath, string targetPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries.FirstOrDefault(e =>
            e.Name.Equals("rg.exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("rg.exe not found in downloaded zip");

        entry.ExtractToFile(targetPath, overwrite: true);
    }

    private async Task ExtractFromTarGzAsync(string tarPath, string targetPath, CancellationToken ct)
    {
        var toolsDir = Path.GetDirectoryName(targetPath)!;
        var result = await runner.RunAsync(
            "tar",
            $"-xzf \"{tarPath}\" --wildcards \"*/rg\" --strip-components=1 -C \"{toolsDir}\"",
            ct: ct);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"tar extraction failed: {result.StdErr}");
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ripgrep found on PATH")]
    private static partial void LogFoundOnPath(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "ripgrep found at {Path}")]
    private static partial void LogFoundLocal(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "ripgrep not found, downloading v{Version}…")]
    private static partial void LogDownloading(ILogger logger, string version);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloading ripgrep from {Url}")]
    private static partial void LogDownloadUrl(ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Information, Message = "ripgrep installed at {Path}")]
    private static partial void LogInstalled(ILogger logger, string path);
}
