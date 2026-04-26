namespace DevHub.Models;

public record RepoGroup(string Name, string Color, IReadOnlyList<RepoInfo> Repos)
{
    public bool AllClean => Repos.All(r => !r.IsDirty && r.BehindCount == 0);
    public bool AllOnMaster => Repos.All(r => r.Branch is "master" or "main");
    public bool AutoCollapse => AllClean && AllOnMaster;
}
