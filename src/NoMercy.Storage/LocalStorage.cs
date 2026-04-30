using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace NoMercy.Storage;

/// <summary>
/// Local-disk implementation of <see cref="IStorage"/>. Every path is
/// validated through <see cref="StoragePathGuard"/> in the constructor
/// before reaching <see cref="IStorageBackend"/>. Stream-returning
/// methods hand out <see cref="FileStream"/> objects with
/// <c>useAsync: true</c> so callers can await reads/writes naturally.
/// </summary>
public sealed class LocalStorage : IStorage
{
    private readonly IStorageBackend _backend;
    private readonly StoragePathGuard _guard;

    public LocalStorage(IStorageBackend backend, StoragePathGuard guard)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
    }

    public async Task<byte[]> ReadAsync(string path, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        await using Stream stream = _backend.OpenRead(safe);
        using MemoryStream ms = new();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        return Task.FromResult(_backend.OpenRead(safe));
    }

    public async Task WriteAsync(string path, byte[] bytes, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        EnsureParentDirectory(safe);
        await using Stream stream = _backend.OpenWrite(safe, overwrite: true);
        await stream.WriteAsync(bytes.AsMemory(), ct);
    }

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        EnsureParentDirectory(safe);
        return Task.FromResult(_backend.OpenWrite(safe, overwrite));
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        return Task.FromResult(_backend.FileExists(safe) || _backend.DirectoryExists(safe));
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        if (_backend.FileExists(safe))
            _backend.DeleteFile(safe);
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        if (_backend.DirectoryExists(safe))
            _backend.DeleteDirectory(safe, recursive);
        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string path, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        _backend.CreateDirectory(safe);
        return Task.CompletedTask;
    }

    public Task MoveAsync(string from, string to, CancellationToken ct)
    {
        string safeFrom = _guard.Validate(from);
        string safeTo = _guard.Validate(to);
        EnsureParentDirectory(safeTo);
        _backend.MoveFile(safeFrom, safeTo);
        return Task.CompletedTask;
    }

    public Task CopyAsync(string from, string to, CancellationToken ct)
    {
        string safeFrom = _guard.Validate(from);
        string safeTo = _guard.Validate(to);
        EnsureParentDirectory(safeTo);
        _backend.CopyFile(safeFrom, safeTo, overwrite: true);
        return Task.CompletedTask;
    }

    public Task<long> SizeAsync(string path, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        return Task.FromResult(_backend.GetFileSize(safe));
    }

    public Task<DateTimeOffset> LastModifiedAsync(string path, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        DateTime utc = _backend.GetLastWriteTimeUtc(safe);
        return Task.FromResult(new DateTimeOffset(utc, TimeSpan.Zero));
    }

    public async IAsyncEnumerable<StorageEntry> ListAsync(
        string path,
        string? pattern,
        bool recursive,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        string safe = _guard.Validate(path);
        SearchOption option = recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        string effectivePattern = string.IsNullOrEmpty(pattern) ? "*" : pattern;

        foreach (
            string entry in _backend.EnumerateFileSystemEntries(safe, effectivePattern, option)
        )
        {
            ct.ThrowIfCancellationRequested();
            bool isDir = _backend.DirectoryExists(entry);
            long size = isDir ? 0L : _backend.GetFileSize(entry);
            DateTime utc = _backend.GetLastWriteTimeUtc(entry);
            yield return new StorageEntry(
                entry,
                isDir,
                size,
                new DateTimeOffset(utc, TimeSpan.Zero)
            );
            await Task.Yield();
        }
    }

    public async Task<string> HashAsync(string path, string algorithm, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        using HashAlgorithm hasher = algorithm.ToLowerInvariant() switch
        {
            "sha256" => SHA256.Create(),
            "md5" => MD5.Create(),
            _ => throw new ArgumentException(
                $"unsupported hash algorithm: {algorithm} (allowed: sha256, md5)",
                nameof(algorithm)
            ),
        };

        await using Stream stream = _backend.OpenRead(safe);
        byte[] digest = await hasher.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    public Task<LocalPathLease> AcquireLocalPathAsync(string path, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        return Task.FromResult(new LocalPathLease(safe));
    }

    // --- Sync companions ----------------------------------------------------

    public bool Exists(string path)
    {
        string safe = _guard.Validate(path);
        return _backend.FileExists(safe) || _backend.DirectoryExists(safe);
    }

    public long SizeOrZero(string path)
    {
        string safe = _guard.Validate(path);
        return _backend.FileExists(safe) ? _backend.GetFileSize(safe) : 0L;
    }

    public long Size(string path)
    {
        string safe = _guard.Validate(path);
        return _backend.GetFileSize(safe);
    }

    public DateTimeOffset LastModified(string path)
    {
        string safe = _guard.Validate(path);
        return new DateTimeOffset(_backend.GetLastWriteTimeUtc(safe), TimeSpan.Zero);
    }

    public void CreateDirectory(string path)
    {
        string safe = _guard.Validate(path);
        _backend.CreateDirectory(safe);
    }

    public void Delete(string path)
    {
        string safe = _guard.Validate(path);
        if (_backend.FileExists(safe))
            _backend.DeleteFile(safe);
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        string safe = _guard.Validate(path);
        if (_backend.DirectoryExists(safe))
            _backend.DeleteDirectory(safe, recursive);
    }

    public byte[] Read(string path)
    {
        string safe = _guard.Validate(path);
        using Stream stream = _backend.OpenRead(safe);
        using MemoryStream ms = new();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public Stream OpenRead(string path)
    {
        string safe = _guard.Validate(path);
        return _backend.OpenRead(safe);
    }

    public Stream OpenWrite(string path, bool overwrite)
    {
        string safe = _guard.Validate(path);
        EnsureParentDirectory(safe);
        return _backend.OpenWrite(safe, overwrite);
    }

    public void Write(string path, byte[] bytes)
    {
        string safe = _guard.Validate(path);
        EnsureParentDirectory(safe);
        using Stream stream = _backend.OpenWrite(safe, overwrite: true);
        stream.Write(bytes, 0, bytes.Length);
    }

    public void Move(string from, string to)
    {
        string safeFrom = _guard.Validate(from);
        string safeTo = _guard.Validate(to);
        EnsureParentDirectory(safeTo);
        _backend.MoveFile(safeFrom, safeTo);
    }

    public void Copy(string from, string to)
    {
        string safeFrom = _guard.Validate(from);
        string safeTo = _guard.Validate(to);
        EnsureParentDirectory(safeTo);
        _backend.CopyFile(safeFrom, safeTo, overwrite: true);
    }

    public IReadOnlyList<StorageEntry> List(string path, string? pattern, bool recursive)
    {
        string safe = _guard.Validate(path);
        SearchOption option = recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        string effectivePattern = string.IsNullOrEmpty(pattern) ? "*" : pattern;

        List<StorageEntry> entries = [];
        foreach (
            string entry in _backend.EnumerateFileSystemEntries(safe, effectivePattern, option)
        )
        {
            bool isDir = _backend.DirectoryExists(entry);
            long size = isDir ? 0L : _backend.GetFileSize(entry);
            DateTime utc = _backend.GetLastWriteTimeUtc(entry);
            entries.Add(
                new StorageEntry(entry, isDir, size, new DateTimeOffset(utc, TimeSpan.Zero))
            );
        }
        return entries;
    }

    public LocalPathLease AcquireLocalPath(string path)
    {
        string safe = _guard.Validate(path);
        return new LocalPathLease(safe);
    }

    private void EnsureParentDirectory(string path)
    {
        string? parent = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(parent))
            return;
        if (!_backend.DirectoryExists(parent))
            _backend.CreateDirectory(parent);
    }

    public async Task<string> ReadAllTextAsync(string path, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        using StreamReader reader = new(_backend.OpenRead(safe));
        return await reader.ReadToEndAsync(ct);
    }

    public async Task WriteAllTextAsync(string path, string contents, CancellationToken ct)
    {
        string safe = _guard.Validate(path);
        EnsureParentDirectory(safe);
        await using StreamWriter writer = new(_backend.OpenWrite(safe, overwrite: true));
        await writer.WriteAsync(contents.AsMemory(), ct);
        await writer.FlushAsync(ct);
    }

    public Task MoveDirectoryAsync(string from, string to, CancellationToken ct)
    {
        string safeFrom = _guard.Validate(from);
        string safeTo = _guard.Validate(to);
        _backend.MoveDirectory(safeFrom, safeTo);
        return Task.CompletedTask;
    }

    public void MoveDirectory(string from, string to)
    {
        string safeFrom = _guard.Validate(from);
        string safeTo = _guard.Validate(to);
        _backend.MoveDirectory(safeFrom, safeTo);
    }
}
