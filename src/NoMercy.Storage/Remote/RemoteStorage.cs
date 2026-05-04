using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using NoMercy.Storage.Validation;

namespace NoMercy.Storage.Remote;

/// <summary>
/// <see cref="IStorage"/> implementation for remote object-store drivers
/// (S3, R2, MinIO, and NFS in-process). Identical to
/// <see cref="LocalStorage"/> except <see cref="AcquireLocalPathAsync"/>
/// downloads the object to a temp file and deletes it on lease dispose.
/// </summary>
public sealed class RemoteStorage : IStorage
{
    private readonly IStorageDriver _driver;

    public RemoteStorage(IStorageDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    public IStorageDriver Driver => _driver;

    /// <summary>
    /// Path Contract Rules 2 + 5 enforcement at the IStorage boundary —
    /// normalises null to empty string (Rule 3 — empty = root) and rejects
    /// null bytes / .. traversal / device paths structurally before the
    /// driver sees them. Drivers handle their own scope-prefixing, but every
    /// remote driver was previously trusting RemoteStorage to keep the input
    /// safe.
    /// </summary>
    private static string V(string? path)
    {
        if (path is null)
            return string.Empty;
        StoragePathGuard.StructuralValidate(path);
        return path;
    }

    public async Task<byte[]> ReadAsync(string path, CancellationToken ct)
    {
        await using Stream stream = _driver.OpenRead(V(path));
        using MemoryStream ms = new();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct) =>
        Task.FromResult(_driver.OpenRead(V(path)));

    public async Task WriteAsync(string path, byte[] bytes, CancellationToken ct)
    {
        await using Stream stream = _driver.OpenWrite(V(path), overwrite: true);
        await stream.WriteAsync(bytes.AsMemory(), ct);
    }

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct) =>
        Task.FromResult(_driver.OpenWrite(V(path), overwrite));

    public Task<bool> ExistsAsync(string path, CancellationToken ct) =>
        Task.FromResult(_driver.FileExists(V(path)) || _driver.DirectoryExists(V(path)));

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        if (_driver.FileExists(V(path)))
            _driver.DeleteFile(V(path));
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken ct)
    {
        if (_driver.DirectoryExists(V(path)))
            _driver.DeleteDirectory(V(path), recursive);
        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string path, CancellationToken ct)
    {
        _driver.CreateDirectory(V(path));
        return Task.CompletedTask;
    }

    public Task MoveAsync(string from, string to, CancellationToken ct)
    {
        _driver.MoveFile(V(from), V(to));
        return Task.CompletedTask;
    }

    public Task CopyAsync(string from, string to, CancellationToken ct)
    {
        _driver.CopyFile(V(from), V(to), overwrite: true);
        return Task.CompletedTask;
    }

    public Task<long> SizeAsync(string path, CancellationToken ct) =>
        Task.FromResult(_driver.GetFileSize(V(path)));

    public Task<DateTimeOffset> LastModifiedAsync(string path, CancellationToken ct)
    {
        DateTime utc = _driver.GetLastWriteTimeUtc(V(path));
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
            string entry in _driver.EnumerateFileSystemEntries(V(path), effectivePattern, option)
        )
        {
            ct.ThrowIfCancellationRequested();
            bool isDir = _driver.DirectoryExists(entry);
            long size = isDir ? 0L : _driver.GetFileSize(entry);
            DateTime utc = _driver.GetLastWriteTimeUtc(entry);
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

        await using Stream stream = _driver.OpenRead(V(path));
        byte[] digest = await hasher.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    /// <summary>
    /// Downloads the remote object to a local temp file and returns a
    /// <see cref="LocalPathLease"/> whose dispose deletes the temp file.
    /// </summary>
    public async Task<LocalPathLease> AcquireLocalPathAsync(string path, CancellationToken ct)
    {
        Directory.CreateDirectory(StoragePaths.TempRoot);
        string tmp = Path.Combine(StoragePaths.TempRoot, $"nomercy-remote-{Guid.NewGuid():N}");

        await using Stream src = _driver.OpenReadIsolated(V(path));
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

    public bool Exists(string path) =>
        _driver.FileExists(V(path)) || _driver.DirectoryExists(V(path));

    public long SizeOrZero(string path) =>
        _driver.FileExists(V(path)) ? _driver.GetFileSize(V(path)) : 0L;

    public long Size(string path) => _driver.GetFileSize(V(path));

    public DateTimeOffset LastModified(string path) =>
        new(_driver.GetLastWriteTimeUtc(V(path)), TimeSpan.Zero);

    public void CreateDirectory(string path) => _driver.CreateDirectory(V(path));

    public void Delete(string path)
    {
        if (_driver.FileExists(V(path)))
            _driver.DeleteFile(V(path));
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        if (_driver.DirectoryExists(V(path)))
            _driver.DeleteDirectory(V(path), recursive);
    }

    public byte[] Read(string path)
    {
        using Stream stream = _driver.OpenRead(V(path));
        using MemoryStream ms = new();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public Stream OpenRead(string path) => _driver.OpenRead(V(path));

    public Stream OpenWrite(string path, bool overwrite) => _driver.OpenWrite(V(path), overwrite);

    public void Write(string path, byte[] bytes)
    {
        using Stream stream = _driver.OpenWrite(V(path), overwrite: true);
        stream.Write(bytes, 0, bytes.Length);
    }

    public void Move(string from, string to) => _driver.MoveFile(V(from), V(to));

    public void Copy(string from, string to) => _driver.CopyFile(V(from), V(to), overwrite: true);

    public IReadOnlyList<StorageEntry> List(string path, string? pattern, bool recursive)
    {
        SearchOption option = recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        string effectivePattern = string.IsNullOrEmpty(pattern) ? "*" : pattern;

        // Drivers with batched listing (e.g. S3 ListObjectsV2) override
        // EnumerateEntries to return size + mtime in the original page
        // instead of fanning out to N×HEAD per file.
        List<StorageEntry> entries = [];
        foreach (
            StorageEntryInfo info in _driver.EnumerateEntries(V(path), effectivePattern, option)
        )
        {
            entries.Add(
                new StorageEntry(
                    info.Path,
                    info.IsDirectory,
                    info.Size,
                    new DateTimeOffset(info.LastWriteUtc, TimeSpan.Zero)
                )
            );
        }
        return entries;
    }

    public LocalPathLease AcquireLocalPath(string path)
    {
        Directory.CreateDirectory(StoragePaths.TempRoot);
        string tmp = Path.Combine(StoragePaths.TempRoot, $"nomercy-remote-{Guid.NewGuid():N}");

        using Stream src = _driver.OpenReadIsolated(V(path));
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
        using StreamReader reader = new(_driver.OpenRead(V(path)));
        return await reader.ReadToEndAsync(ct);
    }

    public async Task WriteAllTextAsync(string path, string contents, CancellationToken ct)
    {
        await using StreamWriter writer = new(_driver.OpenWrite(V(path), overwrite: true));
        await writer.WriteAsync(contents.AsMemory(), ct);
        await writer.FlushAsync(ct);
    }

    public Task MoveDirectoryAsync(string from, string to, CancellationToken ct)
    {
        _driver.MoveDirectory(V(from), V(to));
        return Task.CompletedTask;
    }

    public void MoveDirectory(string from, string to) => _driver.MoveDirectory(V(from), V(to));
}
