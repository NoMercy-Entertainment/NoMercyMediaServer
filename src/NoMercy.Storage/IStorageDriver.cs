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

    DateTime GetCreationTimeUtc(string path);

    DateTime GetLastAccessTimeUtc(string path);
    Stream OpenRead(string path);
    Stream OpenWrite(string path, bool overwrite);
    void MoveFile(string source, string destination);
    void CopyFile(string source, string destination, bool overwrite);

    /// <summary>
    /// Returns entries as driver-relative paths in the same form that
    /// <see cref="FileExists"/> and <see cref="DirectoryExists"/> accept on this driver.
    /// Implementations MUST NOT include the driver-configured prefix / base URL / mount root.
    /// </summary>
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

    /// <summary>
    /// Opens a read stream that is safe for concurrent bulk-copy use.
    /// The default implementation delegates to <see cref="OpenRead"/>; drivers
    /// that manage shared protocol state (e.g. NFS seqid) override this to
    /// return a stream backed by a dedicated, short-lived connection.
    /// </summary>
    Stream OpenReadIsolated(string path) => OpenRead(path);

    /// <summary>
    /// Optionally return a time-limited presigned URL the client can fetch
    /// directly from the backend, bypassing the server. Drivers that don't
    /// support this (local, nfs, webdav by default) return null and the
    /// middleware falls back to streaming through the server.
    /// TTL is clamped to [60s, 24h] by the implementing driver.
    /// </summary>
    Task<Uri?> TryGetPresignedUrlAsync(string path, TimeSpan ttl, CancellationToken ct) =>
        Task.FromResult<Uri?>(null);

    /// <summary>
    /// Path separator the driver speaks. Local on Windows is '\\'; remote
    /// drivers (NFS / S3 / R2 / WebDAV) all use '/' regardless of host OS.
    /// Callers building paths for storage operations should use this instead
    /// of <see cref="Path.DirectorySeparatorChar"/>.
    /// </summary>
    char DirectorySeparator => '/';

    /// <summary>
    /// Joins a parent path and a child segment using the driver's
    /// <see cref="DirectorySeparator"/>. Trims redundant separators on both
    /// sides so the result is canonical. Replaces every storage-path
    /// <c>Path.Combine</c> in the codebase.
    /// </summary>
    string CombinePath(string parent, string child)
    {
        char sep = DirectorySeparator;
        if (string.IsNullOrEmpty(child))
            return parent;
        if (string.IsNullOrEmpty(parent))
            return child;
        string trimmedParent = parent.TrimEnd('/', '\\');
        string trimmedChild = child.TrimStart('/', '\\');
        return $"{trimmedParent}{sep}{trimmedChild}";
    }
}
