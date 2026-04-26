namespace DevHub.Services.SecretProfiles;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct);
    Task WriteAllBytesAsync(string path, byte[] contents, CancellationToken ct);
    void Move(string source, string dest, bool overwrite);
    void Delete(string path);
    IEnumerable<string> EnumerateFiles(string path, string pattern);
    DateTime GetLastWriteTimeUtc(string path);
    long GetFileSize(string path);
}
