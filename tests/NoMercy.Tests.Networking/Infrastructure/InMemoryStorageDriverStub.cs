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

using NoMercy.Storage;

namespace NoMercy.Tests.Networking.Infrastructure;

/// <summary>
/// In-memory <see cref="IStorageDriver"/> test double so NetworkDiscovery's
/// external-IP cache round trip (CacheExternalIp / LoadCachedExternalIp) can
/// be exercised without ever touching the real machine's AppFiles.ConfigPath.
/// Only the members NetworkDiscovery actually calls (FileExists/OpenRead/
/// OpenWrite) have real behavior; everything else this driver doesn't use is
/// intentionally unsupported rather than silently faked.
/// </summary>
public sealed class InMemoryStorageDriverStub : IStorageDriver
{
    private readonly Dictionary<string, byte[]> _files = new();

    public bool FileExists(string path) => _files.ContainsKey(path);

    public bool DirectoryExists(string path) => throw new NotSupportedException();

    public void CreateDirectory(string path) => throw new NotSupportedException();

    public void DeleteFile(string path) => _files.Remove(path);

    public void DeleteDirectory(string path, bool recursive) => throw new NotSupportedException();

    public long GetFileSize(string path) => throw new NotSupportedException();

    public DateTime GetLastWriteTimeUtc(string path) => throw new NotSupportedException();

    public DateTime GetCreationTimeUtc(string path) => throw new NotSupportedException();

    public DateTime GetLastAccessTimeUtc(string path) => throw new NotSupportedException();

    public Stream OpenRead(string path)
    {
        if (!_files.TryGetValue(path, out byte[]? bytes))
            throw new FileNotFoundException(path);
        return new MemoryStream(bytes, false);
    }

    public Stream OpenWrite(string path, bool overwrite)
    {
        if (!overwrite && _files.ContainsKey(path))
            throw new IOException($"'{path}' already exists.");
        return new CaptureOnDisposeStream(bytes => _files[path] = bytes);
    }

    public void MoveFile(string source, string destination) => throw new NotSupportedException();

    public void CopyFile(string source, string destination, bool overwrite) =>
        throw new NotSupportedException();

    public IEnumerable<string> EnumerateFileSystemEntries(
        string directory,
        string searchPattern,
        SearchOption option
    ) => throw new NotSupportedException();

    public string GetFullPath(string path) => path;

    public string? ResolveLinkTarget(string path) => null;

    public bool IsHidden(string path) => false;

    public void MoveDirectory(string source, string destination) =>
        throw new NotSupportedException();

    /// <summary>
    /// Wraps a MemoryStream so writes made through the caller's StreamWriter
    /// (which disposes the stream when it's done) land back in the fake
    /// filesystem instead of vanishing.
    /// </summary>
    private sealed class CaptureOnDisposeStream(Action<byte[]> onDispose) : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                onDispose(ToArray());
            base.Dispose(disposing);
        }
    }
}
