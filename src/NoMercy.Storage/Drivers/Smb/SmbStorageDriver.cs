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

using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SMBLibrary;
using SMBLibrary.Client;
using FileAttributes = SMBLibrary.FileAttributes;

namespace NoMercy.Storage.Drivers.Smb;

/// <summary>
/// <see cref="IStorageDriver"/> backed by SMBLibrary — a pure-C# SMB2/3 client,
/// so SMB shares work cross-platform with no native dependency. A SMBLibrary
/// session is not re-entrant, so short metadata operations run under a single
/// lock over one throwaway session. Read/write streams instead take their own
/// dedicated session (its own connection + open handle) for the whole transfer,
/// so a multi-GB read or write streams in fixed-size chunks and never buffers the
/// file in memory.
/// </summary>
public sealed class SmbStorageDriver : IStorageDriver, IDisposable
{
    public string BackendLabel => "SMB";

    private readonly SmbDriverConfig _config;
    private readonly ILogger _log;
    private readonly object _lock = new();
    private bool _disposed;

    // Per-request transfer size handed to the read/write streams, clamped by the
    // SMB2 client's negotiated MaxRead/WriteSize. Defaults to the streams' own
    // default; the throughput sweep sets it to measure the curve. Not exposed on
    // IStorageDriver — an internal tuning seam only.
    internal int StreamChunkSize { get; set; } = 1024 * 1024;

    public SmbStorageDriver(SmbDriverConfig config, ILogger? log = null)
    {
        _config = config ?? throw new ArgumentNullException(paramName: nameof(config));
        _log = log ?? NullLogger.Instance;
    }

    // ── path helpers ─────────────────────────────────────────────────────────

    // SMB paths inside a share use backslashes and no leading slash. Map a
    // driver-relative path (forward slashes, possibly leading slash) onto the
    // share, honouring the configured BasePath.
    private string ToSmbPath(string path)
    {
        string rel = (path ?? string.Empty).Replace(oldChar: '\\', newChar: '/').Trim(trimChar: '/');
        string combined =
            string.IsNullOrEmpty(value: _config.BasePath) ? rel
            : string.IsNullOrEmpty(value: rel) ? _config.BasePath
            : $"{_config.BasePath}/{rel}";
        return combined.Replace(oldChar: '/', newChar: '\\');
    }

    private static string FromSmbPath(string smbPath) => smbPath.Replace(oldChar: '\\', newChar: '/');

    // Relative parent of a path ("" when the path is top-level), in forward-slash
    // form so it can be fed straight back to CreateDirectory.
    private static string ParentOf(string path)
    {
        string rel = (path ?? string.Empty).Replace(oldChar: '\\', newChar: '/').Trim(trimChar: '/');
        int slash = rel.LastIndexOf(value: '/');
        return slash < 0 ? string.Empty : rel[..slash];
    }

    // ── connection ───────────────────────────────────────────────────────────

    private SmbSession Connect()
    {
        SMB2Client client = new();
        bool connected = client.Connect(
            serverAddress: ResolveHost(host: _config.Host),
            transport: SMBTransportType.DirectTCPTransport,
            port: _config.Port
        );
        if (!connected)
            throw new IOException(message: $"SMB connect failed for {_config.Host}:{_config.Port}");

        NTStatus login = client.Login(
            domainName: _config.Domain,
            userName: _config.Username ?? string.Empty,
            password: _config.Password ?? string.Empty
        );
        if (login != NTStatus.STATUS_SUCCESS)
        {
            client.Disconnect();
            throw new IOException(message: $"SMB login failed for {_config.Host} (status {login})");
        }

        ISMBFileStore store = client.TreeConnect(shareName: _config.Share, status: out NTStatus tree);
        if (tree != NTStatus.STATUS_SUCCESS)
        {
            client.Logoff();
            client.Disconnect();
            throw new IOException(
                message: $"SMB tree-connect to share '{_config.Share}' failed (status {tree})"
            );
        }

        return new() { Client = client, Store = store };
    }

    private static IPAddress ResolveHost(string host)
    {
        if (IPAddress.TryParse(ipString: host, address: out IPAddress? ip))
            return ip;
        IPAddress[] addresses = Dns.GetHostAddresses(hostNameOrAddress: host);
        return addresses.FirstOrDefault(predicate: a =>
                a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ) ?? addresses.First();
    }

    private T WithSession<T>(Func<SmbSession, T> action)
    {
        lock (_lock)
        {
            using SmbSession session = Connect();
            return action(arg: session);
        }
    }

    // ── existence / metadata ──────────────────────────────────────────────────

    public bool FileExists(string path) =>
        WithSession(action: s =>
            TryGetInfo(s: s, smbPath: ToSmbPath(path: path), info: out FileBasicInformation? info, isDirectory: out bool isDir)
            && info is not null
            && !isDir
        );

    public bool DirectoryExists(string path)
    {
        string smb = ToSmbPath(path: path);
        if (smb.Length == 0)
            return true; // share root
        return WithSession(action: s => TryGetInfo(s: s, smbPath: smb, info: out _, isDirectory: out bool isDir) && isDir);
    }

    public long GetFileSize(string path) =>
        WithSession(action: s =>
        {
            OpenForRead(s: s, smbPath: ToSmbPath(path: path), handle: out object handle);
            try
            {
                NTStatus st = s.Store.GetFileInformation(
                    result: out FileInformation info,
                    handle: handle,
                    informationClass: FileInformationClass.FileStandardInformation
                );
                SmbStatus.EnsureSuccess(status: st, what: $"stat '{path}'");
                return ((FileStandardInformation)info).EndOfFile;
            }
            finally
            {
                s.Store.CloseFile(handle: handle);
            }
        });

    public DateTime GetLastWriteTimeUtc(string path) => GetBasicTimes(path: path).LastWrite;

    public DateTime GetCreationTimeUtc(string path) => GetBasicTimes(path: path).Creation;

    public DateTime GetLastAccessTimeUtc(string path) => GetBasicTimes(path: path).LastAccess;

    private (DateTime Creation, DateTime LastAccess, DateTime LastWrite) GetBasicTimes(
        string path
    ) =>
        WithSession(action: s =>
        {
            OpenForRead(s: s, smbPath: ToSmbPath(path: path), handle: out object handle);
            try
            {
                NTStatus st = s.Store.GetFileInformation(
                    result: out FileInformation info,
                    handle: handle,
                    informationClass: FileInformationClass.FileBasicInformation
                );
                SmbStatus.EnsureSuccess(status: st, what: $"stat '{path}'");
                FileBasicInformation b = (FileBasicInformation)info;
                return (
                    (b.CreationTime.Time ?? DateTime.MinValue).ToUniversalTime(),
                    (b.LastAccessTime.Time ?? DateTime.MinValue).ToUniversalTime(),
                    (b.LastWriteTime.Time ?? DateTime.MinValue).ToUniversalTime()
                );
            }
            finally
            {
                s.Store.CloseFile(handle: handle);
            }
        });

    public bool IsHidden(string path) =>
        WithSession(action: s =>
        {
            if (
                !TryGetInfo(s: s, smbPath: ToSmbPath(path: path), info: out FileBasicInformation? info, isDirectory: out _)
                || info is null
            )
                return false;
            return info.FileAttributes.HasFlag(flag: FileAttributes.Hidden)
                || info.FileAttributes.HasFlag(flag: FileAttributes.System);
        });

    public string GetFullPath(string path) => FromSmbPath(smbPath: ToSmbPath(path: path));

    public string? ResolveLinkTarget(string path) => null; // SMB symlinks not modelled

    // ── streams ────────────────────────────────────────────────────────────────

    public Stream OpenRead(string path)
    {
        // A dedicated connection + open handle streamed for the caller's lifetime
        // (not under the shared metadata lock, and not buffered in memory) — a
        // multi-GB media read stays flat in memory. The stream owns the session.
        SmbSession session = Connect();
        try
        {
            OpenForRead(s: session, smbPath: ToSmbPath(path: path), handle: out object handle);
            return new SmbReadStream(session: session, handle: handle, path: path, chunkSize: StreamChunkSize);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public Stream OpenWrite(string path, bool overwrite)
    {
        if (!overwrite && FileExists(path: path))
            throw new IOException(
                message: $"Cannot write to '{path}': file already exists and overwrite is false."
            );

        // Match the other drivers: writing a nested path creates its parents.
        string parent = ParentOf(path: path);
        if (parent.Length > 0)
            CreateDirectory(path: parent);

        // A dedicated connection + open handle streamed for the caller's lifetime
        // — bytes go straight to the share via WriteFile at an advancing offset,
        // so a multi-GB write never buffers in memory. The stream owns the session.
        SmbSession session = Connect();
        try
        {
            NTStatus st = session.Store.CreateFile(
                handle: out object handle,
                fileStatus: out FileStatus _,
                path: ToSmbPath(path: path),
                desiredAccess: AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                fileAttributes: FileAttributes.Normal,
                shareAccess: ShareAccess.None,
                createDisposition: CreateDisposition.FILE_OVERWRITE_IF,
                createOptions: CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                securityContext: null
            );
            SmbStatus.EnsureSuccess(status: st, what: $"create '{path}'");
            return new SmbWriteStream(session: session, handle: handle, path: path, chunkSize: StreamChunkSize);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    // ── mutations ──────────────────────────────────────────────────────────────

    public void CreateDirectory(string path) =>
        WithSession(action: s =>
        {
            // Create each missing segment so nested dirs work like the other drivers.
            string[] segments = ToSmbPath(path: path).Split(separator: '\\', options: StringSplitOptions.RemoveEmptyEntries);
            string current = string.Empty;
            foreach (string segment in segments)
            {
                current = current.Length == 0 ? segment : $"{current}\\{segment}";
                NTStatus st = s.Store.CreateFile(
                    handle: out object handle,
                    fileStatus: out FileStatus _,
                    path: current,
                    desiredAccess: AccessMask.GENERIC_READ,
                    fileAttributes: FileAttributes.Directory,
                    shareAccess: ShareAccess.Read | ShareAccess.Write,
                    createDisposition: CreateDisposition.FILE_OPEN_IF,
                    createOptions: CreateOptions.FILE_DIRECTORY_FILE,
                    securityContext: null
                );
                if (st == NTStatus.STATUS_SUCCESS)
                    s.Store.CloseFile(handle: handle);
                else if (st != NTStatus.STATUS_OBJECT_NAME_COLLISION)
                    SmbStatus.EnsureSuccess(status: st, what: $"mkdir '{current}'");
            }
            return 0;
        });

    public void DeleteFile(string path) => Delete(smbPath: ToSmbPath(path: path), directory: false);

    public void DeleteDirectory(string path, bool recursive)
    {
        if (recursive)
            foreach (
                string child in EnumerateFileSystemEntries(directory: path, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
            )
            {
                if (DirectoryExists(path: child))
                    DeleteDirectory(path: child, recursive: true);
                else
                    DeleteFile(path: child);
            }
        Delete(smbPath: ToSmbPath(path: path), directory: true);
    }

    private void Delete(string smbPath, bool directory) =>
        WithSession(action: s =>
        {
            NTStatus st = s.Store.CreateFile(
                handle: out object handle,
                fileStatus: out FileStatus _,
                path: smbPath,
                desiredAccess: AccessMask.DELETE,
                fileAttributes: directory ? FileAttributes.Directory : FileAttributes.Normal,
                // Tolerate a still-open read stream (which shares Read | Delete):
                // the delete-open must itself allow those to coexist, otherwise a
                // delete-while-reading raises SHARING_VIOLATION.
                shareAccess: ShareAccess.Read
                             | ShareAccess.Write
                             | ShareAccess.Delete,
                createDisposition: CreateDisposition.FILE_OPEN,
                createOptions: directory
                    ? CreateOptions.FILE_DIRECTORY_FILE
                    : CreateOptions.FILE_NON_DIRECTORY_FILE,
                securityContext: null
            );
            SmbStatus.EnsureSuccess(status: st, what: $"open-for-delete '{FromSmbPath(smbPath: smbPath)}'");
            try
            {
                FileDispositionInformation disposition = new() { DeletePending = true };
                NTStatus dst = s.Store.SetFileInformation(handle: handle, information: disposition);
                SmbStatus.EnsureSuccess(status: dst, what: $"delete '{FromSmbPath(smbPath: smbPath)}'");
            }
            finally
            {
                s.Store.CloseFile(handle: handle);
            }
            return 0;
        });

    public void MoveFile(string source, string destination) => Rename(source: source, destination: destination);

    public void MoveDirectory(string source, string destination) => Rename(source: source, destination: destination);

    private void Rename(string source, string destination)
    {
        // SMB rename won't create the destination's parent; match the other
        // drivers and ensure it exists first.
        string destParent = ParentOf(path: destination);
        if (destParent.Length > 0)
            CreateDirectory(path: destParent);

        WithSession(action: s =>
        {
            string srcSmb = ToSmbPath(path: source);
            NTStatus st = s.Store.CreateFile(
                handle: out object handle,
                fileStatus: out FileStatus _,
                path: srcSmb,
                desiredAccess: AccessMask.GENERIC_ALL | AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                fileAttributes: FileAttributes.Normal,
                shareAccess: ShareAccess.None,
                createDisposition: CreateDisposition.FILE_OPEN,
                createOptions: CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                securityContext: null
            );
            SmbStatus.EnsureSuccess(status: st, what: $"open-for-rename '{source}'");
            try
            {
                FileRenameInformationType2 rename = new()
                {
                    ReplaceIfExists = true,
                    FileName = ToSmbPath(path: destination),
                };
                NTStatus rst = s.Store.SetFileInformation(handle: handle, information: rename);
                SmbStatus.EnsureSuccess(status: rst, what: $"rename '{source}' -> '{destination}'");
            }
            finally
            {
                s.Store.CloseFile(handle: handle);
            }
            return 0;
        });
    }

    public void CopyFile(string source, string destination, bool overwrite)
    {
        // SMB has no server-side copy in SMBLibrary's surface — stream the bytes
        // from source to destination without buffering the whole file.
        using Stream r = OpenRead(path: source);
        using Stream w = OpenWrite(path: destination, overwrite: overwrite);
        r.CopyTo(destination: w);
    }

    // ── enumeration ────────────────────────────────────────────────────────────

    public IEnumerable<string> EnumerateFileSystemEntries(
        string directory,
        string searchPattern,
        SearchOption option
    )
    {
        List<string> results = [];
        CollectEntries(directory: directory, searchPattern: searchPattern, option: option, results: results);
        return results;
    }

    private void CollectEntries(
        string directory,
        string searchPattern,
        SearchOption option,
        List<string> results
    )
    {
        string dirRel = (directory ?? string.Empty).Replace(oldChar: '\\', newChar: '/').Trim(trimChar: '/');
        List<(string Name, bool IsDir)> children = WithSession(action: s =>
        {
            string smbDir = ToSmbPath(path: dirRel);
            NTStatus st = s.Store.CreateFile(
                handle: out object handle,
                fileStatus: out FileStatus _,
                path: smbDir,
                desiredAccess: AccessMask.GENERIC_READ,
                fileAttributes: FileAttributes.Directory,
                shareAccess: ShareAccess.Read | ShareAccess.Write,
                createDisposition: CreateDisposition.FILE_OPEN,
                createOptions: CreateOptions.FILE_DIRECTORY_FILE,
                securityContext: null
            );
            // Enumerating a missing directory yields nothing, like the other
            // drivers — not an error.
            if (
                st is NTStatus.STATUS_OBJECT_NAME_NOT_FOUND or NTStatus.STATUS_OBJECT_PATH_NOT_FOUND
            )
                return [];
            SmbStatus.EnsureSuccess(status: st, what: $"open-dir '{directory}'");
            try
            {
                NTStatus qst = s.Store.QueryDirectory(
                    result: out List<QueryDirectoryFileInformation> entries,
                    handle: handle,
                    fileName: string.IsNullOrEmpty(value: searchPattern) ? "*" : searchPattern,
                    informationClass: FileInformationClass.FileDirectoryInformation
                );
                if (qst != NTStatus.STATUS_SUCCESS && qst != NTStatus.STATUS_NO_MORE_FILES)
                    SmbStatus.EnsureSuccess(status: qst, what: $"list '{directory}'");

                List<(string, bool)> found = [];
                foreach (QueryDirectoryFileInformation entry in entries)
                {
                    FileDirectoryInformation info = (FileDirectoryInformation)entry;
                    if (info.FileName is "." or "..")
                        continue;
                    bool isDir = info.FileAttributes.HasFlag(flag: FileAttributes.Directory);
                    found.Add(item: (info.FileName, isDir));
                }
                return found;
            }
            finally
            {
                s.Store.CloseFile(handle: handle);
            }
        });

        foreach ((string name, bool isDir) in children)
        {
            string childRel = dirRel.Length == 0 ? name : $"{dirRel}/{name}";
            results.Add(item: "/" + childRel);
            if (option == SearchOption.AllDirectories && isDir)
                CollectEntries(directory: childRel, searchPattern: searchPattern, option: option, results: results);
        }
    }

    // ── internals ──────────────────────────────────────────────────────────────

    private static void OpenForRead(SmbSession s, string smbPath, out object handle)
    {
        NTStatus st = s.Store.CreateFile(
            handle: out handle,
            fileStatus: out FileStatus _,
            path: smbPath,
            desiredAccess: AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
            fileAttributes: FileAttributes.Normal,
            // Allow a concurrent delete/rename while a read stream holds the file
            // open — the streaming read keeps the handle for its whole lifetime,
            // so without share-delete a delete-after-read raises SHARING_VIOLATION.
            shareAccess: ShareAccess.Read | ShareAccess.Delete,
            createDisposition: CreateDisposition.FILE_OPEN,
            createOptions: CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
            securityContext: null
        );
        SmbStatus.EnsureSuccess(status: st, what: $"open '{FromSmbPath(smbPath: smbPath)}'");
    }

    private static bool TryGetInfo(
        SmbSession s,
        string smbPath,
        out FileBasicInformation? info,
        out bool isDirectory
    )
    {
        info = null;
        isDirectory = false;
        NTStatus st = s.Store.CreateFile(
            handle: out object handle,
            fileStatus: out FileStatus _,
            path: smbPath,
            desiredAccess: AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
            fileAttributes: FileAttributes.Normal,
            shareAccess: ShareAccess.Read | ShareAccess.Write,
            createDisposition: CreateDisposition.FILE_OPEN,
            createOptions: CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
            securityContext: null
        );
        if (st != NTStatus.STATUS_SUCCESS)
            return false;
        try
        {
            NTStatus bst = s.Store.GetFileInformation(
                result: out FileInformation basic,
                handle: handle,
                informationClass: FileInformationClass.FileBasicInformation
            );
            if (bst != NTStatus.STATUS_SUCCESS)
                return false;
            info = (FileBasicInformation)basic;
            isDirectory = info.FileAttributes.HasFlag(flag: FileAttributes.Directory);
            return true;
        }
        finally
        {
            s.Store.CloseFile(handle: handle);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
    }
}
