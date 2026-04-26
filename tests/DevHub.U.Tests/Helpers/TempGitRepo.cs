namespace DevHub.U.Tests.Helpers;

public sealed class TempGitRepo : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public TempGitRepo()
    {
        Directory.CreateDirectory(Path);
        Run("git init");
        Run("git config user.email test@test.com");
        Run("git config user.name Test");
        Run("git commit --allow-empty -m \"initial\"");
    }

    public void CreateFile(string name, string content = "content")
    {
        File.WriteAllText(System.IO.Path.Combine(Path, name), content);
    }

    public void StageAndCommit(string message = "commit")
    {
        Run("git add -A");
        Run($"git commit -m \"{message}\"");
    }

    private void Run(string command)
    {
        var parts = command.Split(' ', 2);
        using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = parts[0],
            Arguments = parts.Length > 1 ? parts[1] : string.Empty,
            WorkingDirectory = Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        p.WaitForExit();
    }

    public void Dispose() => ForceDeleteDirectory(Path);

    internal static void ForceDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(path, recursive: true);
    }
}

public sealed class TempGitRepoAt : IDisposable
{
    public string Path { get; }

    public TempGitRepoAt(string path)
    {
        Path = path;
        Directory.CreateDirectory(path);
        Run("git init");
        Run("git config user.email test@test.com");
        Run("git config user.name Test");
        Run("git commit --allow-empty -m \"initial\"");
    }

    private void Run(string command)
    {
        var parts = command.Split(' ', 2);
        using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = parts[0],
            Arguments = parts.Length > 1 ? parts[1] : string.Empty,
            WorkingDirectory = Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        p.WaitForExit();
    }

    public void Dispose() => TempGitRepo.ForceDeleteDirectory(Path);
}
