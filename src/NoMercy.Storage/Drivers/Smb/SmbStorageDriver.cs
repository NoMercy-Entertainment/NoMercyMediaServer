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
/// so SMB shares work cross-platform with no native dependency. The SMBLibrary
/// session is not re-entrant, so every operation runs under a single lock and
/// connects/logs-in/tree-connects on demand (cheap on a LAN; the simplest path
/// to correctness — matching how the NFS driver serialises its context).
/// </summary>
public sealed class SmbStorageDriver : IStorageDriver, IDisposable
{
    public string BackendLabel => "SMB";

    private readonly SmbDriverConfig _config;
    private readonly ILogger _log;
    private readonly object _lock = new();
    private bool _disposed;

    public SmbStorageDriver(SmbDriverConfig config, ILogger? log = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _log = log ?? NullLogger.Instance;
    }

    // ── path helpers ─────────────────────────────────────────────────────────

    // SMB paths inside a share use backslashes and no leading slash. Map a
    // driver-relative path (forward slashes, possibly leading slash) onto the
    // share, honouring the configured BasePath.
    private string ToSmbPath(string path)
    {
        string rel = (path ?? string.Empty).Replace('\\', '/').Trim('/');
        string combined =
            string.IsNullOrEmpty(_config.BasePath) ? rel
            : string.IsNullOrEmpty(rel) ? _config.BasePath
            : $"{_config.BasePath}/{rel}";
        return combined.Replace('/', '\\');
    }

    private static string FromSmbPath(string smbPath) => smbPath.Replace('\\', '/');

    // Relative parent of a path ("" when the path is top-level), in forward-slash
    // form so it can be fed straight back to CreateDirectory.
    private static string ParentOf(string path)
    {
        string rel = (path ?? string.Empty).Replace('\\', '/').Trim('/');
        int slash = rel.LastIndexOf('/');
        return slash < 0 ? string.Empty : rel[..slash];
    }

    // ── connection ───────────────────────────────────────────────────────────

    private sealed class Session : IDisposable
    {
        public required SMB2Client Client { get; init; }
        public required ISMBFileStore Store { get; init; }

        public void Dispose()
        {
            try
            {
                Store.Disconnect();
            }
            catch
            {
                // ignore
            }
            try
            {
                Client.Logoff();
            }
            catch
            {
                // ignore
            }
            try
            {
                Client.Disconnect();
            }
            catch
            {
                // ignore
            }
        }
    }

    private Session Connect()
    {
        SMB2Client client = new();
        bool connected = client.Connect(
            ResolveHost(_config.Host),
            SMBTransportType.DirectTCPTransport,
            _config.Port
        );
        if (!connected)
            throw new IOException($"SMB connect failed for {_config.Host}:{_config.Port}");

        NTStatus login = client.Login(
            _config.Domain,
            _config.Username ?? string.Empty,
            _config.Password ?? string.Empty
        );
        if (login != NTStatus.STATUS_SUCCESS)
        {
            client.Disconnect();
            throw new IOException($"SMB login failed for {_config.Host} (status {login})");
        }

        ISMBFileStore store = client.TreeConnect(_config.Share, out NTStatus tree);
        if (tree != NTStatus.STATUS_SUCCESS)
        {
            client.Logoff();
            client.Disconnect();
            throw new IOException(
                $"SMB tree-connect to share '{_config.Share}' failed (status {tree})"
            );
        }

        return new Session { Client = client, Store = store };
    }

    private static IPAddress ResolveHost(string host)
    {
        if (IPAddress.TryParse(host, out IPAddress? ip))
            return ip;
        IPAddress[] addresses = Dns.GetHostAddresses(host);
        return addresses.FirstOrDefault(a =>
                a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ) ?? addresses.First();
    }

    private T WithSession<T>(Func<Session, T> action)
    {
        lock (_lock)
        {
            using Session session = Connect();
            return action(session);
        }
    }

    // ── existence / metadata ──────────────────────────────────────────────────

    public bool FileExists(string path) =>
        WithSession(s =>
            TryGetInfo(s, ToSmbPath(path), out FileBasicInformation? info, out bool isDir)
            && info is not null
            && !isDir
        );

    public bool DirectoryExists(string path)
    {
        string smb = ToSmbPath(path);
        if (smb.Length == 0)
            return true; // share root
        return WithSession(s => TryGetInfo(s, smb, out _, out bool isDir) && isDir);
    }

    public long GetFileSize(string path) =>
        WithSession(s =>
        {
            OpenForRead(s, ToSmbPath(path), out object handle);
            try
            {
                NTStatus st = s.Store.GetFileInformation(
                    out FileInformation info,
                    handle,
                    FileInformationClass.FileStandardInformation
                );
                EnsureSuccess(st, $"stat '{path}'");
                return ((FileStandardInformation)info).EndOfFile;
            }
            finally
            {
                s.Store.CloseFile(handle);
            }
        });

    public DateTime GetLastWriteTimeUtc(string path) => GetBasicTimes(path).LastWrite;

    public DateTime GetCreationTimeUtc(string path) => GetBasicTimes(path).Creation;

    public DateTime GetLastAccessTimeUtc(string path) => GetBasicTimes(path).LastAccess;

    private (DateTime Creation, DateTime LastAccess, DateTime LastWrite) GetBasicTimes(
        string path
    ) =>
        WithSession(s =>
        {
            OpenForRead(s, ToSmbPath(path), out object handle);
            try
            {
                NTStatus st = s.Store.GetFileInformation(
                    out FileInformation info,
                    handle,
                    FileInformationClass.FileBasicInformation
                );
                EnsureSuccess(st, $"stat '{path}'");
                FileBasicInformation b = (FileBasicInformation)info;
                return (
                    (b.CreationTime.Time ?? DateTime.MinValue).ToUniversalTime(),
                    (b.LastAccessTime.Time ?? DateTime.MinValue).ToUniversalTime(),
                    (b.LastWriteTime.Time ?? DateTime.MinValue).ToUniversalTime()
                );
            }
            finally
            {
                s.Store.CloseFile(handle);
            }
        });

    public bool IsHidden(string path) =>
        WithSession(s =>
        {
            if (
                !TryGetInfo(s, ToSmbPath(path), out FileBasicInformation? info, out _)
                || info is null
            )
                return false;
            return info.FileAttributes.HasFlag(FileAttributes.Hidden)
                || info.FileAttributes.HasFlag(FileAttributes.System);
        });

    public string GetFullPath(string path) => FromSmbPath(ToSmbPath(path));

    public string? ResolveLinkTarget(string path) => null; // SMB symlinks not modelled

    // ── streams ────────────────────────────────────────────────────────────────

    public Stream OpenRead(string path)
    {
        // Read the whole object into memory under the lock — keeps the SMB
        // session single-threaded and matches the small-object access pattern
        // of metadata scans. Large media reads go through AcquireLocalPathAsync.
        byte[] data = WithSession(s =>
        {
            OpenForRead(s, ToSmbPath(path), out object handle);
            try
            {
                using MemoryStream ms = new();
                long offset = 0;
                while (true)
                {
                    NTStatus st = s.Store.ReadFile(out byte[] chunk, handle, offset, 65536);
                    if (st == NTStatus.STATUS_END_OF_FILE || chunk is null || chunk.Length == 0)
                        break;
                    EnsureSuccess(st, $"read '{path}'");
                    ms.Write(chunk, 0, chunk.Length);
                    offset += chunk.Length;
                }
                return ms.ToArray();
            }
            finally
            {
                s.Store.CloseFile(handle);
            }
        });
        return new MemoryStream(data, writable: false);
    }

    public Stream OpenWrite(string path, bool overwrite)
    {
        if (!overwrite && FileExists(path))
            throw new IOException(
                $"Cannot write to '{path}': file already exists and overwrite is false."
            );
        return new SmbUploadStream(this, path);
    }

    // Called by SmbUploadStream on dispose with the fully-buffered bytes.
    internal void WriteAllBytes(string path, byte[] content)
    {
        // Match the other drivers: writing a nested path creates its parents.
        string parent = ParentOf(path);
        if (parent.Length > 0)
            CreateDirectory(parent);

        WithSession(s =>
        {
            string smb = ToSmbPath(path);
            NTStatus st = s.Store.CreateFile(
                out object handle,
                out FileStatus _,
                smb,
                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OVERWRITE_IF,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                null
            );
            EnsureSuccess(st, $"create '{path}'");
            try
            {
                long offset = 0;
                while (offset < content.Length)
                {
                    int len = Math.Min(65536, content.Length - (int)offset);
                    byte[] chunk = new byte[len];
                    Array.Copy(content, offset, chunk, 0, len);
                    NTStatus wst = s.Store.WriteFile(out int written, handle, offset, chunk);
                    EnsureSuccess(wst, $"write '{path}'");
                    offset += written;
                }
                return 0;
            }
            finally
            {
                s.Store.CloseFile(handle);
            }
        });
    }

    // ── mutations ──────────────────────────────────────────────────────────────

    public void CreateDirectory(string path) =>
        WithSession(s =>
        {
            // Create each missing segment so nested dirs work like the other drivers.
            string[] segments = ToSmbPath(path).Split('\\', StringSplitOptions.RemoveEmptyEntries);
            string current = string.Empty;
            foreach (string segment in segments)
            {
                current = current.Length == 0 ? segment : $"{current}\\{segment}";
                NTStatus st = s.Store.CreateFile(
                    out object handle,
                    out FileStatus _,
                    current,
                    AccessMask.GENERIC_READ,
                    FileAttributes.Directory,
                    ShareAccess.Read | ShareAccess.Write,
                    CreateDisposition.FILE_OPEN_IF,
                    CreateOptions.FILE_DIRECTORY_FILE,
                    null
                );
                if (st == NTStatus.STATUS_SUCCESS)
                    s.Store.CloseFile(handle);
                else if (st != NTStatus.STATUS_OBJECT_NAME_COLLISION)
                    EnsureSuccess(st, $"mkdir '{current}'");
            }
            return 0;
        });

    public void DeleteFile(string path) => Delete(ToSmbPath(path), directory: false);

    public void DeleteDirectory(string path, bool recursive)
    {
        if (recursive)
            foreach (
                string child in EnumerateFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly)
            )
            {
                if (DirectoryExists(child))
                    DeleteDirectory(child, recursive: true);
                else
                    DeleteFile(child);
            }
        Delete(ToSmbPath(path), directory: true);
    }

    private void Delete(string smbPath, bool directory) =>
        WithSession(s =>
        {
            NTStatus st = s.Store.CreateFile(
                out object handle,
                out FileStatus _,
                smbPath,
                AccessMask.DELETE,
                directory ? FileAttributes.Directory : FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OPEN,
                directory
                    ? CreateOptions.FILE_DIRECTORY_FILE
                    : CreateOptions.FILE_NON_DIRECTORY_FILE,
                null
            );
            EnsureSuccess(st, $"open-for-delete '{FromSmbPath(smbPath)}'");
            try
            {
                FileDispositionInformation disposition = new() { DeletePending = true };
                NTStatus dst = s.Store.SetFileInformation(handle, disposition);
                EnsureSuccess(dst, $"delete '{FromSmbPath(smbPath)}'");
            }
            finally
            {
                s.Store.CloseFile(handle);
            }
            return 0;
        });

    public void MoveFile(string source, string destination) => Rename(source, destination);

    public void MoveDirectory(string source, string destination) => Rename(source, destination);

    private void Rename(string source, string destination)
    {
        // SMB rename won't create the destination's parent; match the other
        // drivers and ensure it exists first.
        string destParent = ParentOf(destination);
        if (destParent.Length > 0)
            CreateDirectory(destParent);

        WithSession(s =>
        {
            string srcSmb = ToSmbPath(source);
            NTStatus st = s.Store.CreateFile(
                out object handle,
                out FileStatus _,
                srcSmb,
                AccessMask.GENERIC_ALL | AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                null
            );
            EnsureSuccess(st, $"open-for-rename '{source}'");
            try
            {
                FileRenameInformationType2 rename = new()
                {
                    ReplaceIfExists = true,
                    FileName = ToSmbPath(destination),
                };
                NTStatus rst = s.Store.SetFileInformation(handle, rename);
                EnsureSuccess(rst, $"rename '{source}' -> '{destination}'");
            }
            finally
            {
                s.Store.CloseFile(handle);
            }
            return 0;
        });
    }

    public void CopyFile(string source, string destination, bool overwrite)
    {
        if (!overwrite && FileExists(destination))
            throw new IOException(
                $"Cannot copy to '{destination}': already exists and overwrite is false."
            );
        // SMB has no server-side copy in SMBLibrary's surface — round-trip bytes.
        using Stream r = OpenRead(source);
        using MemoryStream ms = new();
        r.CopyTo(ms);
        WriteAllBytes(destination, ms.ToArray());
    }

    // ── enumeration ────────────────────────────────────────────────────────────

    public IEnumerable<string> EnumerateFileSystemEntries(
        string directory,
        string searchPattern,
        SearchOption option
    )
    {
        List<string> results = [];
        CollectEntries(directory, searchPattern, option, results);
        return results;
    }

    private void CollectEntries(
        string directory,
        string searchPattern,
        SearchOption option,
        List<string> results
    )
    {
        string dirRel = (directory ?? string.Empty).Replace('\\', '/').Trim('/');
        List<(string Name, bool IsDir)> children = WithSession(s =>
        {
            string smbDir = ToSmbPath(dirRel);
            NTStatus st = s.Store.CreateFile(
                out object handle,
                out FileStatus _,
                smbDir,
                AccessMask.GENERIC_READ,
                FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null
            );
            // Enumerating a missing directory yields nothing, like the other
            // drivers — not an error.
            if (
                st is NTStatus.STATUS_OBJECT_NAME_NOT_FOUND or NTStatus.STATUS_OBJECT_PATH_NOT_FOUND
            )
                return [];
            EnsureSuccess(st, $"open-dir '{directory}'");
            try
            {
                NTStatus qst = s.Store.QueryDirectory(
                    out List<QueryDirectoryFileInformation> entries,
                    handle,
                    string.IsNullOrEmpty(searchPattern) ? "*" : searchPattern,
                    FileInformationClass.FileDirectoryInformation
                );
                if (qst != NTStatus.STATUS_SUCCESS && qst != NTStatus.STATUS_NO_MORE_FILES)
                    EnsureSuccess(qst, $"list '{directory}'");

                List<(string, bool)> found = [];
                foreach (QueryDirectoryFileInformation entry in entries)
                {
                    FileDirectoryInformation info = (FileDirectoryInformation)entry;
                    if (info.FileName is "." or "..")
                        continue;
                    bool isDir = info.FileAttributes.HasFlag(FileAttributes.Directory);
                    found.Add((info.FileName, isDir));
                }
                return found;
            }
            finally
            {
                s.Store.CloseFile(handle);
            }
        });

        foreach ((string name, bool isDir) in children)
        {
            string childRel = dirRel.Length == 0 ? name : $"{dirRel}/{name}";
            results.Add("/" + childRel);
            if (option == SearchOption.AllDirectories && isDir)
                CollectEntries(childRel, searchPattern, option, results);
        }
    }

    // ── internals ──────────────────────────────────────────────────────────────

    private static void OpenForRead(Session s, string smbPath, out object handle)
    {
        NTStatus st = s.Store.CreateFile(
            out handle,
            out FileStatus _,
            smbPath,
            AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
            FileAttributes.Normal,
            ShareAccess.Read,
            CreateDisposition.FILE_OPEN,
            CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
            null
        );
        EnsureSuccess(st, $"open '{FromSmbPath(smbPath)}'");
    }

    private static bool TryGetInfo(
        Session s,
        string smbPath,
        out FileBasicInformation? info,
        out bool isDirectory
    )
    {
        info = null;
        isDirectory = false;
        NTStatus st = s.Store.CreateFile(
            out object handle,
            out FileStatus _,
            smbPath,
            AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
            FileAttributes.Normal,
            ShareAccess.Read | ShareAccess.Write,
            CreateDisposition.FILE_OPEN,
            CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
            null
        );
        if (st != NTStatus.STATUS_SUCCESS)
            return false;
        try
        {
            NTStatus bst = s.Store.GetFileInformation(
                out FileInformation basic,
                handle,
                FileInformationClass.FileBasicInformation
            );
            if (bst != NTStatus.STATUS_SUCCESS)
                return false;
            info = (FileBasicInformation)basic;
            isDirectory = info.FileAttributes.HasFlag(FileAttributes.Directory);
            return true;
        }
        finally
        {
            s.Store.CloseFile(handle);
        }
    }

    private static void EnsureSuccess(NTStatus status, string what)
    {
        if (status != NTStatus.STATUS_SUCCESS)
            throw new IOException($"SMB {what} failed (status {status}).");
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
    }
}
