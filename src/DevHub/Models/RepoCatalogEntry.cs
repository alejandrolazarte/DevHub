namespace DevHub.Models;

public class RepoCatalogEntry
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public DateTime AddedUtc { get; set; }

    public RepoCatalogEntry() { }

    public RepoCatalogEntry(string path, DateTime addedUtc)
    {
        Path = path;
        AddedUtc = addedUtc;
    }
}
