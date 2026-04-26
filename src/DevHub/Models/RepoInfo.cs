namespace DevHub.Models;

public record RepoInfo
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string Group { get; init; }
    public string GroupColor { get; init; } = "default";
    public string Branch { get; init; } = string.Empty;
    public bool IsDirty { get; init; }
    public int DirtyFileCount { get; init; }
    public int AheadCount { get; init; }
    public int BehindCount { get; init; }
    public string LastCommitMessage { get; init; } = string.Empty;
    public DateTime LastCommitDate { get; init; }
    public string RemoteUrl { get; init; } = string.Empty;
    public DateTime LastScanned { get; init; }
    public bool IsGitRepo { get; init; } = true;
    public string? ScanError { get; init; }

    public string? PrUrl => BuildPrUrl();

    private string? BuildPrUrl()
    {
        if (string.IsNullOrEmpty(Branch) || Branch is "master" or "main")
        {
            return null;
        }

        if (string.IsNullOrEmpty(RemoteUrl))
        {
            return null;
        }

        var url = RemoteUrl.Trim();

        // GitHub HTTPS: https://github.com/org/repo.git
        // GitHub SSH:   git@github.com:org/repo.git
        if (url.Contains("github.com"))
        {
            url = url.Replace("git@github.com:", "https://github.com/");
            if (url.EndsWith(".git", StringComparison.Ordinal))
            {
                url = url[..^4];
            }
            // Shows all PRs (open + merged) for this branch
            return $"{url}/pulls?q=is%3Apr+head%3A{Uri.EscapeDataString(Branch)}";
        }

        // Azure DevOps HTTPS: https://org@dev.azure.com/org/project/_git/repo
        if (url.Contains("dev.azure.com"))
        {
            url = System.Text.RegularExpressions.Regex.Replace(url, @"https?://[^@]+@", "https://");
            return $"{url}/pullrequests?_a=active&sourceRef={Uri.EscapeDataString($"refs/heads/{Branch}")}";
        }

        return null;
    }
}
