using System.Text.Json;
using DevHub.Models;

namespace DevHub.Services;

public class PackageJsonReader
{
    public IReadOnlyList<ProjectCommand> GetScripts(string repoPath)
    {
        var packageJsonPath = Path.Combine(repoPath, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(packageJsonPath);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("scripts", out var scripts))
            {
                return [];
            }

            return scripts.EnumerateObject()
                .Select(p => new ProjectCommand(p.Name, $"npm run {p.Name}", CommandSource.PackageJson))
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}