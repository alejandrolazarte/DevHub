using DevHub.Models;

namespace DevHub.Services;

public class ProjectTypeDetector
{
    public ProjectType Detect(string repoPath)
    {
        if (File.Exists(Path.Combine(repoPath, "angular.json")))
        {
            return ProjectType.Angular;
        }

        var packageJson = Path.Combine(repoPath, "package.json");
        if (File.Exists(packageJson))
        {
            var content = File.ReadAllText(packageJson);
            if (content.Contains("\"react\"") || content.Contains("\"react-dom\""))
            {
                return ProjectType.React;
            }

            if (content.Contains("\"vue\""))
            {
                return ProjectType.Vue;
            }

            return ProjectType.Node;
        }

        if (Directory.GetFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly).Length > 0 ||
            SafeGetFiles(repoPath, "*.csproj").Any())
        {
            return ProjectType.DotNet;
        }

        return ProjectType.Unknown;
    }

    public IReadOnlyList<ProjectCommand> GetDefaultCommands(ProjectType type, string repoPath) =>
        type switch
        {
            ProjectType.DotNet => GetDotNetCommands(repoPath),
            ProjectType.Angular => [
                new("Serve", "ng serve", CommandSource.AutoDetected),
                new("Build", "ng build", CommandSource.AutoDetected),
                new("Test", "ng test", CommandSource.AutoDetected)
            ],
            ProjectType.React => [
                new("Start", "npm start", CommandSource.AutoDetected),
                new("Build", "npm run build", CommandSource.AutoDetected),
                new("Test", "npm test", CommandSource.AutoDetected)
            ],
            ProjectType.Vue => [
                new("Dev", "npm run dev", CommandSource.AutoDetected),
                new("Build", "npm run build", CommandSource.AutoDetected),
                new("Test", "npm test", CommandSource.AutoDetected)
            ],
            ProjectType.Node => [
                new("Start", "npm start", CommandSource.AutoDetected),
                new("Test", "npm test", CommandSource.AutoDetected)
            ],
            _ => []
        };

    private static List<ProjectCommand> GetDotNetCommands(string repoPath)
    {
        var commands = new List<ProjectCommand>();

        var sln = Directory.GetFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();
        var slnArg = sln is not null ? $" {Path.GetFileName(sln)}" : string.Empty;

        var allProjects = SafeGetFiles(repoPath, "*.csproj").ToList();
        var runnableProjects = allProjects.Where(p => !IsTestProject(p)).ToList();

        foreach (var proj in runnableProjects)
        {
            var name = Path.GetFileNameWithoutExtension(proj);
            var rel = Path.GetRelativePath(repoPath, proj).Replace('\\', '/');
            commands.Add(new($"Run {name}", $"dotnet run --project {rel}", CommandSource.AutoDetected));
        }

        commands.Add(new("Build", $"dotnet build{slnArg}", CommandSource.AutoDetected));
        commands.Add(new("Test", $"dotnet test{slnArg}", CommandSource.AutoDetected));

        return commands;
    }

    private static bool IsTestProject(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".Test", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".UnitTests", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".IntegrationTests", StringComparison.OrdinalIgnoreCase)
            || name.Contains(".Tests.", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SafeGetFiles(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"));
        }
        catch
        {
            return [];
        }
    }
}