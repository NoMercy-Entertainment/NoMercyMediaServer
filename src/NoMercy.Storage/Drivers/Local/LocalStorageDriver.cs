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

    public string CombinePath(string parent, string child) => Path.Combine(path1: parent, path2: child);

    public bool FileExists(string path) => File.Exists(path: path);

    public bool DirectoryExists(string path) => Directory.Exists(path: path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path: path);

    public void DeleteFile(string path) => File.Delete(path: path);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path: path, recursive: recursive);

    public long GetFileSize(string path) => new FileInfo(fileName: path).Length;

    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path: path);

    // File.GetCreation*/Directory.GetCreation* are only permitted here — all callers must go through IStorageDriver.
    public DateTime GetCreationTimeUtc(string path) =>
        Directory.Exists(path: path) ? Directory.GetCreationTimeUtc(path: path) : File.GetCreationTimeUtc(path: path);

    // Remote backends (S3, NFS, WebDAV) don't reliably expose atime — they return LastWriteTime as fallback.
    public DateTime GetLastAccessTimeUtc(string path) =>
        Directory.Exists(path: path)
            ? Directory.GetLastAccessTimeUtc(path: path)
            : File.GetLastAccessTimeUtc(path: path);

    // FileShare.ReadWrite | FileShare.Delete is critical on Windows. Default
    // FileShare.Read blocks concurrent writes/deletes for the stream's entire
    // lifetime — long ExoPlayer Range reads keep this open for hours, which
    // would block encoder output, library reorg, replace-on-merge, etc.
    public Stream OpenRead(string path) =>
        new FileStream(
            path: path,
            mode: FileMode.Open,
            access: FileAccess.Read,
            share: FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024,
            useAsync: true
        );

    public Stream OpenWrite(string path, bool overwrite) =>
        new FileStream(
            path: path,
            mode: overwrite ? FileMode.Create : FileMode.CreateNew,
            access: FileAccess.Write,
            share: FileShare.None,
            bufferSize: 4096,
            useAsync: true
        );

    // Local files are already on local FS; no staging needed.
    public Task<LocalPathLease> AcquireLocalPathAsync(string path, CancellationToken ct) =>
        Task.FromResult(result: new LocalPathLease(path: path));

    public void MoveFile(string source, string destination) =>
        File.Move(sourceFileName: source, destFileName: destination, overwrite: false);

    public void CopyFile(string source, string destination, bool overwrite) =>
        File.Copy(sourceFileName: source, destFileName: destination, overwrite: overwrite);

    public IEnumerable<string> EnumerateFileSystemEntries(
        string directory,
        string searchPattern,
        SearchOption option
    ) =>
        // Path Contract: List on a non-existent directory returns empty,
        // never throws. Directory.EnumerateFileSystemEntries throws
        // DirectoryNotFoundException on a missing directory; guard it.
        Directory.Exists(path: directory)
            ? Directory.EnumerateFileSystemEntries(path: directory, searchPattern: searchPattern, searchOption: option)
            : [];

    public IEnumerable<StorageEntryInfo> EnumerateEntries(
        string directory,
        string searchPattern,
        SearchOption option
    )
    {
        // Single-pass metadata: DirectoryInfo enumeration carries size,
        // last-write and is-directory from the OS readdir, so there is no extra
        // stat per entry (the default IStorageDriver implementation does N extra
        // DirectoryExists/GetFileSize/GetLastWriteTimeUtc calls). Same empty-on-
        // missing-directory contract as EnumerateFileSystemEntries.
        if (!Directory.Exists(path: directory))
            yield break;

        DirectoryInfo root = new(path: directory);
        foreach (FileSystemInfo info in root.EnumerateFileSystemInfos(searchPattern: searchPattern, searchOption: option))
        {
            bool isDir = info is DirectoryInfo;
            long size = info is FileInfo file ? file.Length : 0L;
            yield return new(Path: info.FullName, IsDirectory: isDir, Size: size, LastWriteUtc: info.LastWriteTimeUtc);
        }
    }

    public string GetFullPath(string path) => Path.GetFullPath(path: path);

    public string? ResolveLinkTarget(string path)
    {
        try
        {
            FileSystemInfo? info =
                File.Exists(path: path) ? new FileInfo(fileName: path)
                : Directory.Exists(path: path) ? new DirectoryInfo(path: path)
                : null;
            if (info?.LinkTarget is null)
                return null;
            FileSystemInfo? real = info.ResolveLinkTarget(returnFinalTarget: true);
            return real is null ? null : Path.GetFullPath(path: real.FullName);
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
            FileAttributes attrs = File.GetAttributes(path: path);
            return (attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0;
        }
        catch
        {
            return false;
        }
    }

    public void MoveDirectory(string source, string destination) =>
        Directory.Move(sourceDirName: source, destDirName: destination);
}
