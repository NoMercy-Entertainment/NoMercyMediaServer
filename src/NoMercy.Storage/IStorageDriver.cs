namespace NoMercy.Storage;

/// <summary>
/// Low-level filesystem primitives that <see cref="LocalStorage"/>
/// depends on. Default implementation
/// <see cref="LocalStorageDriver"/> wraps <see cref="System.IO"/>;
/// tests inject a fake to exercise <see cref="LocalStorage"/>
/// contracts without touching real disk.
/// </summary>
public interface IStorageDriver
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void DeleteFile(string path);
    void DeleteDirectory(string path, bool recursive);
    long GetFileSize(string path);
    DateTime GetLastWriteTimeUtc(string path);
    Stream OpenRead(string path);
    Stream OpenWrite(string path, bool overwrite);
    void MoveFile(string source, string destination);
    void CopyFile(string source, string destination, bool overwrite);
    IEnumerable<string> EnumerateFileSystemEntries(
        string directory,
        string searchPattern,
        SearchOption option
    );

    /// <summary>
    /// Returns the canonical (absolute, normalized) path. Pure string
    /// transformation — does not touch the filesystem.
    /// </summary>
    string GetFullPath(string path);

    /// <summary>
    /// If <paramref name="path"/> exists and is a symlink, returns the
    /// canonicalized real target. Returns null when the path is not a
    /// symlink, does not exist, or cannot be resolved.
    /// </summary>
    string? ResolveLinkTarget(string path);

    /// <summary>
    /// Returns true when <paramref name="path"/> is flagged as hidden
    /// or system by the driver. Used by browse/picker code to skip
    /// OS-managed entries (Windows Hidden/System attributes, etc.).
    /// Returns false when the path does not exist or cannot be queried.
    /// </summary>
    bool IsHidden(string path);

    void MoveDirectory(string source, string destination);
}
