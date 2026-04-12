namespace NoMercy.Encoder.V3.Infrastructure;

public class FileSystemAdapter : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void DeleteFile(string path) => File.Delete(path);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    public long GetFileSize(string path) => new FileInfo(path).Length;

    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

    public Stream OpenRead(string path) => File.OpenRead(path);

    public Stream OpenWrite(string path) => File.OpenWrite(path);

    public string[] GetFiles(string directory, string searchPattern, SearchOption option) =>
        Directory.GetFiles(directory, searchPattern, option);

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public long GetAvailableDiskSpace(string path)
    {
        DriveInfo drive = new(Path.GetPathRoot(path) ?? path);
        return drive.AvailableFreeSpace;
    }
}
