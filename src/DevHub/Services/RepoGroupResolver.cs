using DevHub.Models;

namespace DevHub.Services;

public static class RepoGroupResolver
{
    public static (string Group, string Color) Resolve(
        string repoName, IReadOnlyList<GroupRule> rules)
    {
        foreach (var rule in rules)
        {
            if (rule.Prefixes.Any(p => repoName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                return (rule.Name, rule.Color);
            }
        }

        return ("Other", "default");
    }
}
