namespace DevHub.Services;

public class FolderPickerService
{
    public IReadOnlyList<string> GetDrives() =>
        DriveInfo.GetDrives()
                 .Where(d => d.IsReady)
                 .Select(d => d.RootDirectory.FullName)
                 .ToList();

    public IReadOnlyList<string> GetSubDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path)
                            .OrderBy(p => p)
                            .ToList();
        }
        catch
        {
            return [];
        }
    }

    public string? GetParent(string path) =>
        Directory.GetParent(path)?.FullName;
}