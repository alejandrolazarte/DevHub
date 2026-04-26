using System.Reflection;

namespace DevHub.Services;

public class VersionService
{
    private static readonly string StateDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevHub");

    private static readonly string VersionFile = Path.Combine(StateDir, "last-version.txt");

    public string Current { get; } =
        Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion.Split('+')[0]   // strip commit hash suffix
        ?? "1.0.0";

    public bool IsUpdated { get; }
    public string? PreviousVersion { get; }

    public VersionService()
    {
        Directory.CreateDirectory(StateDir);

        var stored = File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : null;

        if (stored is not null && stored != Current)
        {
            IsUpdated = true;
            PreviousVersion = stored;
        }

        File.WriteAllText(VersionFile, Current);
    }
}
