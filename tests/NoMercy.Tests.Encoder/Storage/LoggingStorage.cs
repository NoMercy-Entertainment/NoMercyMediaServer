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

using System.Collections.Concurrent;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Storage;

/// <summary>
/// IStorage decorator that records every call into <see cref="Calls"/>
/// before delegating to an inner implementation. Used by Phase 0.3
/// verification tests to prove encoder code reaches the storage
/// abstraction (instead of dropping to raw System.IO).
/// </summary>
public sealed class LoggingStorage(IStorage inner) : IStorage
{
    public ConcurrentBag<string> Calls { get; } = [];

    public Task<byte[]> ReadAsync(string path, CancellationToken ct)
    {
        Calls.Add(item: $"ReadAsync:{path}");
        return inner.ReadAsync(path: path, ct: ct);
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct)
    {
        Calls.Add(item: $"OpenReadAsync:{path}");
        return inner.OpenReadAsync(path: path, ct: ct);
    }

    public Task WriteAsync(string path, byte[] bytes, CancellationToken ct)
    {
        Calls.Add(item: $"WriteAsync:{path}");
        return inner.WriteAsync(path: path, bytes: bytes, ct: ct);
    }

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct)
    {
        Calls.Add(item: $"OpenWriteAsync:{path}");
        return inner.OpenWriteAsync(path: path, overwrite: overwrite, ct: ct);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct)
    {
        Calls.Add(item: $"ExistsAsync:{path}");
        return inner.ExistsAsync(path: path, ct: ct);
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        Calls.Add(item: $"DeleteAsync:{path}");
        return inner.DeleteAsync(path: path, ct: ct);
    }

    public Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken ct)
    {
        Calls.Add(item: $"DeleteDirectoryAsync:{path}");
        return inner.DeleteDirectoryAsync(path: path, recursive: recursive, ct: ct);
    }

    public Task CreateDirectoryAsync(string path, CancellationToken ct)
    {
        Calls.Add(item: $"CreateDirectoryAsync:{path}");
        return inner.CreateDirectoryAsync(path: path, ct: ct);
    }

    public Task MoveAsync(string from, string to, CancellationToken ct)
    {
        Calls.Add(item: $"MoveAsync:{from}→{to}");
        return inner.MoveAsync(from: from, to: to, ct: ct);
    }

    public Task CopyAsync(string from, string to, CancellationToken ct)
    {
        Calls.Add(item: $"CopyAsync:{from}→{to}");
        return inner.CopyAsync(from: from, to: to, ct: ct);
    }

    public Task<long> SizeAsync(string path, CancellationToken ct)
    {
        Calls.Add(item: $"SizeAsync:{path}");
        return inner.SizeAsync(path: path, ct: ct);
    }

    public Task<DateTimeOffset> LastModifiedAsync(string path, CancellationToken ct)
    {
        Calls.Add(item: $"LastModifiedAsync:{path}");
        return inner.LastModifiedAsync(path: path, ct: ct);
    }

    public IAsyncEnumerable<StorageEntry> ListAsync(
        string path,
        string? pattern,
        bool recursive,
        CancellationToken ct
    )
    {
        Calls.Add(item: $"ListAsync:{path}:{pattern}:{recursive}");
        return inner.ListAsync(path: path, pattern: pattern, recursive: recursive, ct: ct);
    }

    public Task<string> HashAsync(string path, string algorithm, CancellationToken ct)
    {
        Calls.Add(item: $"HashAsync:{path}:{algorithm}");
        return inner.HashAsync(path: path, algorithm: algorithm, ct: ct);
    }

    public Task<LocalPathLease> AcquireLocalPathAsync(string path, CancellationToken ct)
    {
        Calls.Add(item: $"AcquireLocalPathAsync:{path}");
        return inner.AcquireLocalPathAsync(path: path, ct: ct);
    }

    // --- Sync companions ----------------------------------------------------

    public bool Exists(string path)
    {
        Calls.Add(item: $"Exists:{path}");
        return inner.Exists(path: path);
    }

    public long SizeOrZero(string path)
    {
        Calls.Add(item: $"SizeOrZero:{path}");
        return inner.SizeOrZero(path: path);
    }

    public long Size(string path)
    {
        Calls.Add(item: $"Size:{path}");
        return inner.Size(path: path);
    }

    public DateTimeOffset LastModified(string path)
    {
        Calls.Add(item: $"LastModified:{path}");
        return inner.LastModified(path: path);
    }

    public void CreateDirectory(string path)
    {
        Calls.Add(item: $"CreateDirectory:{path}");
        inner.CreateDirectory(path: path);
    }

    public void Delete(string path)
    {
        Calls.Add(item: $"Delete:{path}");
        inner.Delete(path: path);
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        Calls.Add(item: $"DeleteDirectory:{path}");
        inner.DeleteDirectory(path: path, recursive: recursive);
    }

    public byte[] Read(string path)
    {
        Calls.Add(item: $"Read:{path}");
        return inner.Read(path: path);
    }

    public Stream OpenRead(string path)
    {
        Calls.Add(item: $"OpenRead:{path}");
        return inner.OpenRead(path: path);
    }

    public Stream OpenWrite(string path, bool overwrite)
    {
        Calls.Add(item: $"OpenWrite:{path}");
        return inner.OpenWrite(path: path, overwrite: overwrite);
    }

    public void Write(string path, byte[] bytes)
    {
        Calls.Add(item: $"Write:{path}");
        inner.Write(path: path, bytes: bytes);
    }

    public void Move(string from, string to)
    {
        Calls.Add(item: $"Move:{from}→{to}");
        inner.Move(from: from, to: to);
    }

    public void Copy(string from, string to)
    {
        Calls.Add(item: $"Copy:{from}→{to}");
        inner.Copy(from: from, to: to);
    }

    public IReadOnlyList<StorageEntry> List(string path, string? pattern, bool recursive)
    {
        Calls.Add(item: $"List:{path}:{pattern}:{recursive}");
        return inner.List(path: path, pattern: pattern, recursive: recursive);
    }

    public LocalPathLease AcquireLocalPath(string path)
    {
        Calls.Add(item: $"AcquireLocalPath:{path}");
        return inner.AcquireLocalPath(path: path);
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken ct)
    {
        Calls.Add(item: $"ReadAllTextAsync:{path}");
        return inner.ReadAllTextAsync(path: path, ct: ct);
    }

    public Task WriteAllTextAsync(string path, string contents, CancellationToken ct)
    {
        Calls.Add(item: $"WriteAllTextAsync:{path}");
        return inner.WriteAllTextAsync(path: path, contents: contents, ct: ct);
    }

    public Task MoveDirectoryAsync(string from, string to, CancellationToken ct)
    {
        Calls.Add(item: $"MoveDirectoryAsync:{from}→{to}");
        return inner.MoveDirectoryAsync(from: from, to: to, ct: ct);
    }

    public void MoveDirectory(string from, string to)
    {
        Calls.Add(item: $"MoveDirectory:{from}→{to}");
        inner.MoveDirectory(from: from, to: to);
    }

    public IStorageDriver Driver => inner.Driver;
}
