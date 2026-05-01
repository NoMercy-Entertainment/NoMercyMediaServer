namespace NoMercy.Storage.Local;

public sealed class SystemIoStorageBackend : IStorageBackend
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void DeleteFile(string path) => File.Delete(path);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    public long GetFileSize(string path) => new FileInfo(path).Length;

    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

    public Stream OpenRead(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

    public Stream OpenWrite(string path, bool overwrite) =>
        new FileStream(
            path,
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            useAsync: true
        );

    public void MoveFile(string source, string destination) =>
        File.Move(source, destination, overwrite: false);

    public void CopyFile(string source, string destination, bool overwrite) =>
        File.Copy(source, destination, overwrite);

    public IEnumerable<string> EnumerateFileSystemEntries(
        string directory,
        string searchPattern,
        SearchOption option
    ) => Directory.EnumerateFileSystemEntries(directory, searchPattern, option);

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public string? ResolveLinkTarget(string path)
    {
        try
        {
            FileSystemInfo? info =
                File.Exists(path) ? new FileInfo(path)
                : Directory.Exists(path) ? new DirectoryInfo(path)
                : null;
            if (info?.LinkTarget is null)
                return null;
            FileSystemInfo? real = info.ResolveLinkTarget(returnFinalTarget: true);
            return real is null ? null : Path.GetFullPath(real.FullName);
        }
        catch
        {
            return null;
        }
    }

    public bool IsHidden(string path)
    {
        try
        {
            FileAttributes attrs = File.GetAttributes(path);
            return (attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0;
        }
        catch
        {
            return false;
        }
    }

    public void MoveDirectory(string source, string destination) =>
        Directory.Move(source, destination);
}
