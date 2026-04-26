namespace DevHub.Services.SecretProfiles;

public class FileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct) => File.ReadAllBytesAsync(path, ct);
    public Task WriteAllBytesAsync(string path, byte[] contents, CancellationToken ct) => File.WriteAllBytesAsync(path, contents, ct);
    public void Move(string source, string dest, bool overwrite) => File.Move(source, dest, overwrite);
    public void Delete(string path) => File.Delete(path);
    public IEnumerable<string> EnumerateFiles(string path, string pattern) => Directory.EnumerateFiles(path, pattern);
    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);
    public long GetFileSize(string path) => new FileInfo(path).Length;
}
