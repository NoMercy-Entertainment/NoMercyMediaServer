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
using System.Text;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Bundle;

/// <summary>
/// In-memory <see cref="IStorage"/> test double shared across the encoder
/// bundle/reconciliation test suites — blueprint writer/builder, garbage
/// collector, slug renamer, and finalize-stage integration tests.
/// </summary>
internal sealed class TestStorage : IStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new(
        comparer: StringComparer.OrdinalIgnoreCase
    );

    /// <summary>Test-side helper: read stored bytes back as a UTF-8 string.</summary>
    public string? ReadString(string path) =>
        _files.TryGetValue(key: path, value: out byte[]? bytes) ? Encoding.UTF8.GetString(bytes: bytes) : null;

    /// <summary>Seed a file so tests can simulate on-disk state.</summary>
    public void Seed(string path, byte[] bytes) => _files[key: path] = bytes;

    /// <summary>Every path currently seeded/written, for assertion.</summary>
    public IReadOnlyList<string> AllPaths() => [.. _files.Keys];

    // -----------------------------------------------------------------------
    // IStorage — the members exercised by the Bundle test suites
    // -----------------------------------------------------------------------

    public Task WriteAsync(string path, byte[] bytes, CancellationToken ct)
    {
        _files[key: path] = bytes;
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadAsync(string path, CancellationToken ct)
    {
        if (!_files.TryGetValue(key: path, value: out byte[]? bytes))
            throw new FileNotFoundException(message: $"TestStorage: path not found: {path}");
        return Task.FromResult(result: bytes);
    }

    public bool Exists(string path) => _files.ContainsKey(key: path);

    public Task<bool> ExistsAsync(string path, CancellationToken ct) =>
        Task.FromResult(
            result: Exists(path: path)
                    || _files.Keys.Any(predicate: k =>
                        k.StartsWith(value: path.TrimEnd(trimChar: '/') + "/", comparisonType: StringComparison.OrdinalIgnoreCase)
                    )
        );

    public Task<string> ReadAllTextAsync(string path, CancellationToken ct)
    {
        if (!_files.TryGetValue(key: path, value: out byte[]? bytes))
            throw new FileNotFoundException(message: $"TestStorage: path not found: {path}");
        return Task.FromResult(result: Encoding.UTF8.GetString(bytes: bytes));
    }

    public Task WriteAllTextAsync(string path, string contents, CancellationToken ct)
    {
        _files[key: path] = Encoding.UTF8.GetBytes(s: contents);
        return Task.CompletedTask;
    }

    // Overrides IStorage's default (Driver.CombinePath) — Driver throws on this
    // double, so path-joining callers need a working stand-in.
    public string CombinePath(string parent, string child) =>
        string.IsNullOrEmpty(value: parent) ? child : $"{parent.TrimEnd(trimChar: '/')}/{child.TrimStart(trimChar: '/')}";

    public IReadOnlyList<StorageEntry> List(string path, string? pattern, bool recursive)
    {
        string prefix = string.IsNullOrEmpty(value: path) ? string.Empty : path.TrimEnd(trimChar: '/') + "/";
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<StorageEntry> entries = [];
        foreach (string key in _files.Keys)
        {
            if (!key.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase))
                continue;
            string rel = key[prefix.Length..];
            if (!recursive && rel.Contains(value: '/'))
                continue;
            entries.Add(
                item: new(Path: key, IsDirectory: false, SizeBytes: _files[key: key].Length, LastModified: now)
            );
        }
        return entries;
    }

    // -----------------------------------------------------------------------
    // Remaining IStorage members — not exercised by these test suites
    // -----------------------------------------------------------------------

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        _files.TryRemove(key: path, value: out _);
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task CreateDirectoryAsync(string path, CancellationToken ct) => Task.CompletedTask;

    public Task MoveAsync(string from, string to, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task CopyAsync(string from, string to, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<long> SizeAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<DateTimeOffset> LastModifiedAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<StorageEntry> ListAsync(
        string path,
        string? pattern,
        bool recursive,
        CancellationToken ct
    ) => throw new NotSupportedException();

    public Task<string> HashAsync(string path, string algorithm, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<LocalPathLease> AcquireLocalPathAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException();

    public long SizeOrZero(string path) => _files.TryGetValue(key: path, value: out byte[]? b) ? b.Length : 0;

    public long Size(string path) =>
        _files.TryGetValue(key: path, value: out byte[]? b) ? b.Length : throw new FileNotFoundException(message: path);

    public DateTimeOffset LastModified(string path) => DateTimeOffset.UtcNow;

    public void CreateDirectory(string path) { }

    public void Delete(string path) => _files.TryRemove(key: path, value: out _);

    public void DeleteDirectory(string path, bool recursive) => throw new NotSupportedException();

    public byte[] Read(string path) =>
        _files.TryGetValue(key: path, value: out byte[]? bytes) ? bytes : throw new FileNotFoundException(message: path);

    public Stream OpenRead(string path) => throw new NotSupportedException();

    public Stream OpenWrite(string path, bool overwrite) => throw new NotSupportedException();

    public void Write(string path, byte[] bytes) => _files[key: path] = bytes;

    public void Move(string from, string to) => throw new NotSupportedException();

    public void Copy(string from, string to) => throw new NotSupportedException();

    public LocalPathLease AcquireLocalPath(string path) => throw new NotSupportedException();

    public Task MoveDirectoryAsync(string from, string to, CancellationToken ct) =>
        throw new NotSupportedException();

    public void MoveDirectory(string from, string to) => throw new NotSupportedException();

    public IStorageDriver Driver => throw new NotSupportedException();
}
