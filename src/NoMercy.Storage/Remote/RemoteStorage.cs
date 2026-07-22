// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

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
        _driver = driver ?? throw new ArgumentNullException(paramName: nameof(driver));
    }

    public IStorageDriver Driver => _driver;

    /// <summary>
    /// Path Contract Rules 1/4 + 5 enforcement at the IStorage boundary.
    /// Normalises null to empty string (Rule 3 — empty = scope root) and
    /// rejects null bytes, ".." traversal, device paths, and OS-absolute or
    /// backend-absolute paths before the driver sees them.
    /// </summary>
    private static string V(string? path)
    {
        if (path is null || path.Length == 0)
            return string.Empty;
        StoragePathGuard.StructuralValidate(requestedPath: path);
        StoragePathGuard.RejectAbsolutePath(path: path);
        return path;
    }

    public async Task<byte[]> ReadAsync(string path, CancellationToken ct)
    {
        await using Stream stream = _driver.OpenRead(path: V(path: path));
        using MemoryStream ms = new();
        await stream.CopyToAsync(destination: ms, cancellationToken: ct);
        return ms.ToArray();
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct) =>
        Task.FromResult(result: _driver.OpenRead(path: V(path: path)));

    public async Task WriteAsync(string path, byte[] bytes, CancellationToken ct)
    {
        await using Stream stream = _driver.OpenWrite(path: V(path: path), overwrite: true);
        await stream.WriteAsync(buffer: bytes.AsMemory(), cancellationToken: ct);
    }

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct) =>
        Task.FromResult(result: _driver.OpenWrite(path: V(path: path), overwrite: overwrite));

    public Task<bool> ExistsAsync(string path, CancellationToken ct) =>
        Task.FromResult(result: _driver.FileExists(path: V(path: path)) || _driver.DirectoryExists(path: V(path: path)));

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        if (_driver.FileExists(path: V(path: path)))
            _driver.DeleteFile(path: V(path: path));
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken ct)
    {
        if (_driver.DirectoryExists(path: V(path: path)))
            _driver.DeleteDirectory(path: V(path: path), recursive: recursive);
        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string path, CancellationToken ct)
    {
        _driver.CreateDirectory(path: V(path: path));
        return Task.CompletedTask;
    }

    public Task MoveAsync(string from, string to, CancellationToken ct)
    {
        _driver.MoveFile(source: V(path: from), destination: V(path: to));
        return Task.CompletedTask;
    }

    public Task CopyAsync(string from, string to, CancellationToken ct)
    {
        _driver.CopyFile(source: V(path: from), destination: V(path: to), overwrite: true);
        return Task.CompletedTask;
    }

    public Task<long> SizeAsync(string path, CancellationToken ct) =>
        Task.FromResult(result: _driver.GetFileSize(path: V(path: path)));

    public Task<DateTimeOffset> LastModifiedAsync(string path, CancellationToken ct)
    {
        DateTime utc = _driver.GetLastWriteTimeUtc(path: V(path: path));
        return Task.FromResult(result: new DateTimeOffset(dateTime: utc, offset: TimeSpan.Zero));
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
        string effectivePattern = string.IsNullOrEmpty(value: pattern) ? "*" : pattern;

        // Use the driver's batched EnumerateEntries — drivers like S3 and
        // WebDAV pull size + mtime in the same listing call so we don't fan
        // out to N×HEAD/PROPFIND per file (which costs minutes on a 200-segment
        // HLS folder and trips per-request auth quirks on some WebDAV servers).
        foreach (
            StorageEntryInfo info in _driver.EnumerateEntries(directory: V(path: path), searchPattern: effectivePattern, option: option)
        )
        {
            ct.ThrowIfCancellationRequested();
            yield return new(
                Path: info.Path,
                IsDirectory: info.IsDirectory,
                SizeBytes: info.Size,
                LastModified: new(dateTime: info.LastWriteUtc, offset: TimeSpan.Zero)
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
                message: $"unsupported hash algorithm: {algorithm} (allowed: sha256, md5)",
                paramName: nameof(algorithm)
            ),
        };

        await using Stream stream = _driver.OpenRead(path: V(path: path));
        byte[] digest = await hasher.ComputeHashAsync(inputStream: stream, cancellationToken: ct);
        return Convert.ToHexString(inArray: digest).ToLowerInvariant();
    }

    /// <summary>
    /// Downloads the remote object to a local temp file and returns a
    /// <see cref="LocalPathLease"/> whose dispose deletes the temp file.
    /// </summary>
    public async Task<LocalPathLease> AcquireLocalPathAsync(string path, CancellationToken ct)
    {
        Directory.CreateDirectory(path: StoragePaths.TempRoot);
#pragma warning disable NMS001 // local temp file for a materialized remote object — OS-native by design
        string tmp = Path.Combine(path1: StoragePaths.TempRoot, path2: $"nomercy-remote-{Guid.NewGuid():N}");
#pragma warning restore NMS001

        await using Stream src = _driver.OpenReadIsolated(path: V(path: path));
        await using FileStream dst = new(
            path: tmp,
            mode: FileMode.Create,
            access: FileAccess.Write,
            share: FileShare.None,
            bufferSize: 65536,
            useAsync: true
        );
        await src.CopyToAsync(destination: dst, cancellationToken: ct);

        return new(
            path: tmp,
            onDispose: async () =>
            {
                await Task.Run(
                    action: () =>
                    {
                        if (File.Exists(path: tmp))
                            File.Delete(path: tmp);
                    },
                    cancellationToken: CancellationToken.None
                );
            }
        );
    }

    // --- Sync companions ----------------------------------------------------

    public bool Exists(string path) =>
        _driver.FileExists(path: V(path: path)) || _driver.DirectoryExists(path: V(path: path));

    public long SizeOrZero(string path) =>
        _driver.FileExists(path: V(path: path)) ? _driver.GetFileSize(path: V(path: path)) : 0L;

    public long Size(string path) => _driver.GetFileSize(path: V(path: path));

    public DateTimeOffset LastModified(string path) =>
        new(dateTime: _driver.GetLastWriteTimeUtc(path: V(path: path)), offset: TimeSpan.Zero);

    public void CreateDirectory(string path) => _driver.CreateDirectory(path: V(path: path));

    public void Delete(string path)
    {
        if (_driver.FileExists(path: V(path: path)))
            _driver.DeleteFile(path: V(path: path));
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        if (_driver.DirectoryExists(path: V(path: path)))
            _driver.DeleteDirectory(path: V(path: path), recursive: recursive);
    }

    public byte[] Read(string path)
    {
        using Stream stream = _driver.OpenRead(path: V(path: path));
        using MemoryStream ms = new();
        stream.CopyTo(destination: ms);
        return ms.ToArray();
    }

    public Stream OpenRead(string path) => _driver.OpenRead(path: V(path: path));

    public Stream OpenWrite(string path, bool overwrite) => _driver.OpenWrite(path: V(path: path), overwrite: overwrite);

    public void Write(string path, byte[] bytes)
    {
        using Stream stream = _driver.OpenWrite(path: V(path: path), overwrite: true);
        stream.Write(buffer: bytes, offset: 0, count: bytes.Length);
    }

    public void Move(string from, string to) => _driver.MoveFile(source: V(path: from), destination: V(path: to));

    public void Copy(string from, string to) => _driver.CopyFile(source: V(path: from), destination: V(path: to), overwrite: true);

    public IReadOnlyList<StorageEntry> List(string path, string? pattern, bool recursive)
    {
        SearchOption option = recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        string effectivePattern = string.IsNullOrEmpty(value: pattern) ? "*" : pattern;

        // Drivers with batched listing (e.g. S3 ListObjectsV2) override
        // EnumerateEntries to return size + mtime in the original page
        // instead of fanning out to N×HEAD per file.
        List<StorageEntry> entries = [];
        foreach (
            StorageEntryInfo info in _driver.EnumerateEntries(directory: V(path: path), searchPattern: effectivePattern, option: option)
        )
        {
            entries.Add(
                item: new(
                    Path: info.Path,
                    IsDirectory: info.IsDirectory,
                    SizeBytes: info.Size,
                    LastModified: new(dateTime: info.LastWriteUtc, offset: TimeSpan.Zero)
                )
            );
        }
        return entries;
    }

    public LocalPathLease AcquireLocalPath(string path)
    {
        Directory.CreateDirectory(path: StoragePaths.TempRoot);
#pragma warning disable NMS001 // local temp file for a materialized remote object — OS-native by design
        string tmp = Path.Combine(path1: StoragePaths.TempRoot, path2: $"nomercy-remote-{Guid.NewGuid():N}");
#pragma warning restore NMS001

        using Stream src = _driver.OpenReadIsolated(path: V(path: path));
        using FileStream dst = new(path: tmp, mode: FileMode.Create, access: FileAccess.Write, share: FileShare.None);
        src.CopyTo(destination: dst);

        return new(
            path: tmp,
            onDispose: async () =>
            {
                await Task.Run(
                    action: () =>
                    {
                        if (File.Exists(path: tmp))
                            File.Delete(path: tmp);
                    },
                    cancellationToken: CancellationToken.None
                );
            }
        );
    }

    public async Task<string> ReadAllTextAsync(string path, CancellationToken ct)
    {
        using StreamReader reader = new(stream: _driver.OpenRead(path: V(path: path)));
        return await reader.ReadToEndAsync(cancellationToken: ct);
    }

    public async Task WriteAllTextAsync(string path, string contents, CancellationToken ct)
    {
        await using StreamWriter writer = new(stream: _driver.OpenWrite(path: V(path: path), overwrite: true));
        await writer.WriteAsync(buffer: contents.AsMemory(), cancellationToken: ct);
        await writer.FlushAsync(cancellationToken: ct);
    }

    public Task MoveDirectoryAsync(string from, string to, CancellationToken ct)
    {
        _driver.MoveDirectory(source: V(path: from), destination: V(path: to));
        return Task.CompletedTask;
    }

    public void MoveDirectory(string from, string to) => _driver.MoveDirectory(source: V(path: from), destination: V(path: to));
}
