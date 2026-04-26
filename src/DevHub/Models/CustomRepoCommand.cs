namespace DevHub.Models;

public class CustomRepoCommand
{
    public int Id { get; set; }
    public string RepoPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Icon { get; set; } = "terminal";
}