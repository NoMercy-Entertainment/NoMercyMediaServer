using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace NoMercy.Storage.Remote;

/// <summary>
/// <see cref="IStorage"/> implementation for remote object-store backends
/// (S3, R2, MinIO, and future NFS in-process driver). Identical to
/// <see cref="LocalStorage"/> except <see cref="AcquireLocalPathAsync"/>
/// downloads the object to a temp file and deletes it on lease dispose.
/// </summary>
public sealed class RemoteStorage : IStorage
{
    private readonly IStorageBackend _backend;

    public RemoteStorage(IStorageBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public async Task<byte[]> ReadAsync(string path, CancellationToken ct)
    {
        await using Stream stream = _backend.OpenRead(path);
        using MemoryStream ms = new();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct) =>
        Task.FromResult(_backend.OpenRead(path));

    public async Task WriteAsync(string path, byte[] bytes, CancellationToken ct)
    {
        await using Stream stream = _backend.OpenWrite(path, overwrite: true);
        await stream.WriteAsync(bytes.AsMemory(), ct);
    }

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct) =>
        Task.FromResult(_backend.OpenWrite(path, overwrite));

    public Task<bool> ExistsAsync(string path, CancellationToken ct) =>
        Task.FromResult(_backend.FileExists(path) || _backend.DirectoryExists(path));

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        if (_backend.FileExists(path))
            _backend.DeleteFile(path);
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken ct)
    {
        if (_backend.DirectoryExists(path))
            _backend.DeleteDirectory(path, recursive);
        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string path, CancellationToken ct)
    {
        _backend.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public Task MoveAsync(string from, string to, CancellationToken ct)
    {
        _backend.MoveFile(from, to);
        return Task.CompletedTask;
    }

    public Task CopyAsync(string from, string to, CancellationToken ct)
    {
        _backend.CopyFile(from, to, overwrite: true);
        return Task.CompletedTask;
    }

    public Task<long> SizeAsync(string path, CancellationToken ct) =>
        Task.FromResult(_backend.GetFileSize(path));

    public Task<DateTimeOffset> LastModifiedAsync(string path, CancellationToken ct)
    {
        DateTime utc = _backend.GetLastWriteTimeUtc(path);
        return Task.FromResult(new DateTimeOffset(utc, TimeSpan.Zero));
    }

    public async IAsyncEnumerable<StorageEntry> ListAsync(
        string path,
        string? pattern,
        bool recursive,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        SearchOption option = recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        string effectivePattern = string.IsNullOrEmpty(pattern) ? "*" : pattern;

        foreach (
            string entry in _backend.EnumerateFileSystemEntries(path, effectivePattern, option)
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
        using HashAlgorithm hasher = algorithm.ToLowerInvariant() switch
        {
            "sha256" => SHA256.Create(),
            "md5" => MD5.Create(),
            _ => throw new ArgumentException(
                $"unsupported hash algorithm: {algorithm} (allowed: sha256, md5)",
                nameof(algorithm)
            ),
        };

        await using Stream stream = _backend.OpenRead(path);
        byte[] digest = await hasher.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    /// <summary>
    /// Downloads the remote object to a local temp file and returns a
    /// <see cref="LocalPathLease"/> whose dispose deletes the temp file.
    /// </summary>
    public async Task<LocalPathLease> AcquireLocalPathAsync(string path, CancellationToken ct)
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"nomercy-remote-{Guid.NewGuid():N}");

        await using Stream src = _backend.OpenRead(path);
        await using FileStream dst = new(
            tmp,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            65536,
            useAsync: true
        );
        await src.CopyToAsync(dst, ct);

        return new LocalPathLease(
            tmp,
            async () =>
            {
                await Task.Run(
                    () =>
                    {
                        if (File.Exists(tmp))
                            File.Delete(tmp);
                    },
                    CancellationToken.None
                );
            }
        );
    }

    // --- Sync companions ----------------------------------------------------

    public bool Exists(string path) => _backend.FileExists(path) || _backend.DirectoryExists(path);

    public long SizeOrZero(string path) =>
        _backend.FileExists(path) ? _backend.GetFileSize(path) : 0L;

    public long Size(string path) => _backend.GetFileSize(path);

    public DateTimeOffset LastModified(string path) =>
        new(_backend.GetLastWriteTimeUtc(path), TimeSpan.Zero);

    public void CreateDirectory(string path) => _backend.CreateDirectory(path);

    public void Delete(string path)
    {
        if (_backend.FileExists(path))
            _backend.DeleteFile(path);
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        if (_backend.DirectoryExists(path))
            _backend.DeleteDirectory(path, recursive);
    }

    public byte[] Read(string path)
    {
        using Stream stream = _backend.OpenRead(path);
        using MemoryStream ms = new();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public Stream OpenRead(string path) => _backend.OpenRead(path);

    public Stream OpenWrite(string path, bool overwrite) => _backend.OpenWrite(path, overwrite);

    public void Write(string path, byte[] bytes)
    {
        using Stream stream = _backend.OpenWrite(path, overwrite: true);
        stream.Write(bytes, 0, bytes.Length);
    }

    public void Move(string from, string to) => _backend.MoveFile(from, to);

    public void Copy(string from, string to) => _backend.CopyFile(from, to, overwrite: true);

    public IReadOnlyList<StorageEntry> List(string path, string? pattern, bool recursive)
    {
        SearchOption option = recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        string effectivePattern = string.IsNullOrEmpty(pattern) ? "*" : pattern;

        List<StorageEntry> entries = [];
        foreach (
            string entry in _backend.EnumerateFileSystemEntries(path, effectivePattern, option)
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
        string tmp = Path.Combine(Path.GetTempPath(), $"nomercy-remote-{Guid.NewGuid():N}");

        using Stream src = _backend.OpenRead(path);
        using FileStream dst = new(tmp, FileMode.Create, FileAccess.Write, FileShare.None);
        src.CopyTo(dst);

        return new LocalPathLease(
            tmp,
            async () =>
            {
                await Task.Run(
                    () =>
                    {
                        if (File.Exists(tmp))
                            File.Delete(tmp);
                    },
                    CancellationToken.None
                );
            }
        );
    }

    public async Task<string> ReadAllTextAsync(string path, CancellationToken ct)
    {
        using StreamReader reader = new(_backend.OpenRead(path));
        return await reader.ReadToEndAsync(ct);
    }

    public async Task WriteAllTextAsync(string path, string contents, CancellationToken ct)
    {
        await using StreamWriter writer = new(_backend.OpenWrite(path, overwrite: true));
        await writer.WriteAsync(contents.AsMemory(), ct);
        await writer.FlushAsync(ct);
    }

    public Task MoveDirectoryAsync(string from, string to, CancellationToken ct)
    {
        _backend.MoveDirectory(from, to);
        return Task.CompletedTask;
    }

    public void MoveDirectory(string from, string to) => _backend.MoveDirectory(from, to);
}
