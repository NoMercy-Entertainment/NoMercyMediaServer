namespace NoMercy.Storage;

/// <summary>
/// Minimum-viable filesystem abstraction shared across the NoMercy
/// codebase. A single concrete driver (<see cref="LocalStorage"/>)
/// ships today; remote drivers (SMB / NFS / S3 / R2) land post-merge
/// without touching consumer call sites.
/// </summary>
public interface IStorage
{
    Task<byte[]> ReadAsync(string path, CancellationToken ct);

    Task<Stream> OpenReadAsync(string path, CancellationToken ct);

    Task WriteAsync(string path, byte[] bytes, CancellationToken ct);

    Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct);

    Task<bool> ExistsAsync(string path, CancellationToken ct);

    Task DeleteAsync(string path, CancellationToken ct);

    Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken ct);

    Task CreateDirectoryAsync(string path, CancellationToken ct);

    Task MoveAsync(string from, string to, CancellationToken ct);

    Task CopyAsync(string from, string to, CancellationToken ct);

    Task<long> SizeAsync(string path, CancellationToken ct);

    Task<DateTimeOffset> LastModifiedAsync(string path, CancellationToken ct);

    IAsyncEnumerable<StorageEntry> ListAsync(
        string path,
        string? pattern,
        bool recursive,
        CancellationToken ct
    );

    /// <summary>
    /// Compute a content hash. <paramref name="algorithm"/> is
    /// <c>sha256</c> or <c>md5</c> (case-insensitive). Returns a
    /// lowercase hex-encoded digest.
    /// </summary>
    Task<string> HashAsync(string path, string algorithm, CancellationToken ct);

    /// <summary>
    /// Acquire a real local path suitable for child-process consumption
    /// (ffmpeg, fpcalc, whisper, etc.). <see cref="LocalStorage"/>
    /// returns the validated path as-is with a no-op dispose; remote
    /// drivers stage to a temp file and clean up on dispose.
    /// </summary>
    Task<LocalPathLease> AcquireLocalPathAsync(string path, CancellationToken ct);

    // --- Sync companions ----------------------------------------------------
    //
    // The async surface above is the forward-compatible API — remote
    // drivers (SMB / NFS / S3 / R2) will do real I/O during these calls.
    // The sync companions below exist for call sites where the op is a
    // one-shot string/metadata check and async buys nothing. LocalStorage
    // executes them directly on the backend; remote drivers will block on
    // their own thread.

    bool Exists(string path);

    /// <summary>
    /// File size in bytes, or 0 when the file does not exist. Replaces
    /// the <c>(await Exists) ? await Size : 0</c> double-await pattern at
    /// call sites that only need the size for logging / reporting.
    /// </summary>
    long SizeOrZero(string path);

    long Size(string path);

    DateTimeOffset LastModified(string path);

    void CreateDirectory(string path);

    void Delete(string path);

    void DeleteDirectory(string path, bool recursive);

    byte[] Read(string path);

    Stream OpenRead(string path);

    Stream OpenWrite(string path, bool overwrite);

    void Write(string path, byte[] bytes);

    void Move(string from, string to);

    void Copy(string from, string to);

    IReadOnlyList<StorageEntry> List(string path, string? pattern, bool recursive);

    LocalPathLease AcquireLocalPath(string path);

    Task<string> ReadAllTextAsync(string path, CancellationToken ct);

    Task WriteAllTextAsync(string path, string contents, CancellationToken ct);

    Task MoveDirectoryAsync(string from, string to, CancellationToken ct);

    void MoveDirectory(string from, string to);

    /// <summary>
    /// The underlying low-level driver. Used by consumers (e.g. MediaScan)
    /// that accept an <see cref="IStorageDriver"/> directly.
    /// </summary>
    IStorageDriver Driver { get; }
}
