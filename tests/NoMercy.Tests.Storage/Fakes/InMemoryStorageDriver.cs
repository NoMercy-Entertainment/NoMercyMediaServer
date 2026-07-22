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

namespace NoMercy.Tests.Storage.Fakes;

/// <summary>
/// In-memory <see cref="IStorageDriver"/> that behaves like a remote object-
/// store backend (S3 / NFS / WebDAV shape): keys are '/'-joined, there is no
/// OS-rooted "scope", and the driver deliberately does NOT override any of
/// the interface's default members (<c>BackendLabel</c>,
/// <c>DirectorySeparator</c>, <c>CombinePath</c>, <c>EnumerateEntries</c>,
/// <c>OpenReadIsolated</c>, <c>TryGetPresignedUrlAsync</c>,
/// <c>AcquireLocalPathAsync</c>) so tests exercising it through
/// <see cref="NoMercy.Storage.Remote.RemoteStorage"/> also exercise the
/// production default-interface-member code paths that real remote drivers
/// (S3 / SMB / WebDAV) inherit unchanged.
///
/// Used as a real collaborator (not a mock of the unit under test) so
/// RemoteStorage tests exercise real stream copies, real byte content, and
/// real path-normalization behavior instead of asserting against a mock's
/// recorded calls.
/// </summary>
internal sealed class InMemoryStorageDriver : IStorageDriver
{
    private readonly Dictionary<string, byte[]> _files = new(comparer: StringComparer.Ordinal);
    private readonly HashSet<string> _dirs = new(comparer: StringComparer.Ordinal) { string.Empty };
    private readonly Dictionary<string, DateTime> _mtimes = new(comparer: StringComparer.Ordinal);

    public int MoveDirectoryCallCount { get; private set; }

    private static string Key(string path) => path.Replace(oldChar: '\\', newChar: '/').Trim(trimChar: '/');

    public void SeedFile(string path, byte[] content)
    {
        string key = Key(path: path);
        _files[key: key] = content;
        _mtimes[key: key] = DateTime.UtcNow;
    }

    public void SeedDirectory(string path) => _dirs.Add(item: Key(path: path));

    public bool FileExists(string path) => _files.ContainsKey(key: Key(path: path));

    public bool DirectoryExists(string path) => _dirs.Contains(item: Key(path: path));

    public void CreateDirectory(string path) => _dirs.Add(item: Key(path: path));

    public void DeleteFile(string path) => _files.Remove(key: Key(path: path));

    public void DeleteDirectory(string path, bool recursive)
    {
        string key = Key(path: path);
        _dirs.Remove(item: key);
        if (!recursive)
            return;
        string prefix = key.Length == 0 ? string.Empty : key + "/";
        foreach (
            string fileKey in _files
                .Keys.Where(predicate: k => k.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal))
                .ToList()
        )
            _files.Remove(key: fileKey);
        foreach (
            string dirKey in _dirs
                .Where(predicate: k => k.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal))
                .ToList()
        )
            _dirs.Remove(item: dirKey);
    }

    public long GetFileSize(string path) =>
        _files.TryGetValue(key: Key(path: path), value: out byte[]? bytes) ? bytes.Length : 0L;

    public DateTime GetLastWriteTimeUtc(string path) =>
        _mtimes.TryGetValue(key: Key(path: path), value: out DateTime t) ? t : DateTime.UnixEpoch;

    public DateTime GetCreationTimeUtc(string path) => GetLastWriteTimeUtc(path: path);

    public DateTime GetLastAccessTimeUtc(string path) => GetLastWriteTimeUtc(path: path);

    public Stream OpenRead(string path)
    {
        string key = Key(path: path);
        if (!_files.TryGetValue(key: key, value: out byte[]? bytes))
            throw new FileNotFoundException(message: $"no such object: {path}", fileName: path);
        return new MemoryStream(buffer: bytes, writable: false);
    }

    public Stream OpenWrite(string path, bool overwrite)
    {
        string key = Key(path: path);
        if (!overwrite && _files.ContainsKey(key: key))
            throw new IOException(message: $"object already exists: {path}");
        return new CommitOnDisposeStream(onCommit: bytes => SeedFile(path: key, content: bytes));
    }

    public void MoveFile(string source, string destination)
    {
        string from = Key(path: source);
        string to = Key(path: destination);
        if (!_files.TryGetValue(key: from, value: out byte[]? bytes))
            throw new FileNotFoundException(message: $"no such object: {source}", fileName: source);
        _files.Remove(key: from);
        _files[key: to] = bytes;
        _mtimes[key: to] = DateTime.UtcNow;
    }

    public void CopyFile(string source, string destination, bool overwrite)
    {
        string from = Key(path: source);
        string to = Key(path: destination);
        if (!_files.TryGetValue(key: from, value: out byte[]? bytes))
            throw new FileNotFoundException(message: $"no such object: {source}", fileName: source);
        if (!overwrite && _files.ContainsKey(key: to))
            throw new IOException(message: $"object already exists: {destination}");
        _files[key: to] = bytes.ToArray();
        _mtimes[key: to] = DateTime.UtcNow;
    }

    public IEnumerable<string> EnumerateFileSystemEntries(
        string directory,
        string searchPattern,
        SearchOption option
    )
    {
        string prefix = Key(path: directory);
        string withSlash = prefix.Length == 0 ? string.Empty : prefix + "/";

        foreach (string fileKey in _files.Keys)
        {
            if (!fileKey.StartsWith(value: withSlash, comparisonType: StringComparison.Ordinal))
                continue;
            string remainder = fileKey[withSlash.Length..];
            if (remainder.Length == 0)
                continue;
            if (option == SearchOption.TopDirectoryOnly && remainder.Contains(value: '/'))
                continue;
            yield return fileKey;
        }
    }

    public string GetFullPath(string path) => Key(path: path);

    public string? ResolveLinkTarget(string path) => null;

    public bool IsHidden(string path) => false;

    public void MoveDirectory(string source, string destination)
    {
        MoveDirectoryCallCount++;
        string from = Key(path: source);
        string to = Key(path: destination);
        _dirs.Remove(item: from);
        _dirs.Add(item: to);
    }

    /// <summary>
    /// Buffers writes and only commits them to the backing dictionary when
    /// disposed — mirrors how a real upload stream (S3 multipart, SMB write
    /// handle) only becomes visible to subsequent reads after completion.
    /// </summary>
    private sealed class CommitOnDisposeStream : MemoryStream
    {
        private readonly Action<byte[]> _onCommit;
        private bool _committed;

        public CommitOnDisposeStream(Action<byte[]> onCommit)
        {
            _onCommit = onCommit;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_committed)
            {
                _committed = true;
                _onCommit(obj: ToArray());
            }
            base.Dispose(disposing: disposing);
        }
    }
}
