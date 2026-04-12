namespace NoMercy.Encoder.V3.Infrastructure;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void DeleteFile(string path);
    void DeleteDirectory(string path, bool recursive);
    long GetFileSize(string path);
    DateTime GetLastWriteTimeUtc(string path);
    Stream OpenRead(string path);
    Stream OpenWrite(string path);
    string[] GetFiles(string directory, string searchPattern, SearchOption option);
    string GetFullPath(string path);
    long GetAvailableDiskSpace(string path);
}
