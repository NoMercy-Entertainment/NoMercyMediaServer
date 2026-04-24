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
}
