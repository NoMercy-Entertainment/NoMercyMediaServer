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

namespace NoMercy.Storage.Drivers.Local;

public sealed class LocalStorageDriver : IStorageDriver
{
    public string BackendLabel => "Local";

    public char DirectorySeparator => Path.DirectorySeparatorChar;

    public string CombinePath(string parent, string child) => Path.Combine(parent, child);

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void DeleteFile(string path) => File.Delete(path);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    public long GetFileSize(string path) => new FileInfo(path).Length;

    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

    // File.GetCreation*/Directory.GetCreation* are only permitted here — all callers must go through IStorageDriver.
    public DateTime GetCreationTimeUtc(string path) =>
        Directory.Exists(path) ? Directory.GetCreationTimeUtc(path) : File.GetCreationTimeUtc(path);

    // Remote backends (S3, NFS, WebDAV) don't reliably expose atime — they return LastWriteTime as fallback.
    public DateTime GetLastAccessTimeUtc(string path) =>
        Directory.Exists(path)
            ? Directory.GetLastAccessTimeUtc(path)
            : File.GetLastAccessTimeUtc(path);

    // FileShare.ReadWrite | FileShare.Delete is critical on Windows. Default
    // FileShare.Read blocks concurrent writes/deletes for the stream's entire
    // lifetime — long ExoPlayer Range reads keep this open for hours, which
    // would block encoder output, library reorg, replace-on-merge, etc.
    public Stream OpenRead(string path) =>
        new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024,
            useAsync: true
        );

    public Stream OpenWrite(string path, bool overwrite) =>
        new FileStream(
            path,
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            useAsync: true
        );

    // Local files are already on local FS; no staging needed.
    public Task<LocalPathLease> AcquireLocalPathAsync(string path, CancellationToken ct) =>
        Task.FromResult(new LocalPathLease(path));

    public void MoveFile(string source, string destination) =>
        File.Move(source, destination, overwrite: false);

    public void CopyFile(string source, string destination, bool overwrite) =>
        File.Copy(source, destination, overwrite);

    public IEnumerable<string> EnumerateFileSystemEntries(
        string directory,
        string searchPattern,
        SearchOption option
    ) =>
        // Path Contract: List on a non-existent directory returns empty,
        // never throws. Directory.EnumerateFileSystemEntries throws
        // DirectoryNotFoundException on a missing directory; guard it.
        Directory.Exists(directory)
            ? Directory.EnumerateFileSystemEntries(directory, searchPattern, option)
            : [];

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public string? ResolveLinkTarget(string path)
    {
        try
        {
            FileSystemInfo? info =
                File.Exists(path) ? new FileInfo(path)
                : Directory.Exists(path) ? new DirectoryInfo(path)
                : null;
            if (info?.LinkTarget is null)
                return null;
            FileSystemInfo? real = info.ResolveLinkTarget(returnFinalTarget: true);
            return real is null ? null : Path.GetFullPath(real.FullName);
        }
        catch
        {
            return null;
        }
    }

    public bool IsHidden(string path)
    {
        try
        {
            FileAttributes attrs = File.GetAttributes(path);
            return (attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0;
        }
        catch
        {
            return false;
        }
    }

    public void MoveDirectory(string source, string destination) =>
        Directory.Move(source, destination);
}
