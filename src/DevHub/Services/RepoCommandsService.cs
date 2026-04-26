using DevHub.Models;

namespace DevHub.Services;

public class RepoCommandsService(
    ProjectTypeDetector typeDetector,
    PackageJsonReader packageJsonReader,
    HiddenCommandService hiddenCommandService)
{
    public async Task<IReadOnlyList<ProjectCommand>> GetAutoCommandsAsync(
        string repoPath, CancellationToken ct = default)
    {
        var type = typeDetector.Detect(repoPath);
        var defaults = typeDetector.GetDefaultCommands(type, repoPath);
        var scripts = packageJsonReader.GetScripts(repoPath);
        var hidden = await hiddenCommandService.GetHiddenNamesAsync(repoPath, ct);

        return [
            .. defaults.Where(c => !hidden.Contains(c.Name)),
            .. scripts.Where(c => !hidden.Contains(c.Name))
        ];
    }
}
