using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage.Drivers.Nfs.Interop;

namespace NoMercy.Storage.Drivers.Nfs;

/// <summary>
/// <see cref="IStorageDriver"/> backed by libnfs P/Invoke — connects to an
/// NFS server in-process without requiring an OS-level mount. Supports NFS3
/// and NFS4 with optional AUTH_UNIX credentials.
///
/// Thread-safety: the libnfs context is not re-entrant. All operations are
/// protected by a <see cref="SemaphoreSlim"/> (max 1 concurrent call).
/// </summary>
public sealed class NfsStorageDriver : IStorageDriver, IDisposable
{
    private readonly NfsDriverConfig _config;
    private readonly IntPtr _nfs;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger _log;
    private bool _disposed;

    public NfsStorageDriver(NfsDriverConfig config, ILogger? log = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _log = log ?? NullLogger.Instance;
        _nfs = LibNfs.InitContext();
        if (_nfs == IntPtr.Zero)
            throw new InvalidOperationException(
                "nfs_init_context returned null — libnfs not available."
            );

        // Set NFS protocol version BEFORE mount. libnfs defaults to NFSv3
        // when not set; we call this even for v3 so the value is explicit
        // and surfaces a clear error if the server doesn't speak that version.
        int versionRc = LibNfs.SetVersion(_nfs, config.Version);
        if (versionRc != 0)
        {
            string err = LibNfs.GetError(_nfs);
            LibNfs.DestroyContext(_nfs);
            _nfs = IntPtr.Zero;
            throw new IOException($"nfs_set_version({config.Version}) failed — {err}");
        }

        if (config.Uid.HasValue)
            LibNfs.SetUid(_nfs, config.Uid.Value);
        if (config.Gid.HasValue)
            LibNfs.SetGid(_nfs, config.Gid.Value);

        int rc = LibNfs.Mount(_nfs, config.Server, config.Export);
        if (rc != 0)
        {
            string err = LibNfs.GetError(_nfs);
            LibNfs.DestroyContext(_nfs);
            _nfs = IntPtr.Zero;
            throw new IOException(
                $"NFS{config.Version} mount failed for {config.Server}:{config.Export} — {err}"
            );
        }
    }

    // -----------------------------------------------------------------------
    // IStorageDriver
    // -----------------------------------------------------------------------

    public bool FileExists(string path)
    {
        string nfsPath = ToNfsPath(path);
        _lock.Wait();
        try
        {
            int rc = LibNfs.Stat64(_nfs, nfsPath, out LibNfs.NfsStat64 stat);
            if (rc != 0)
            {
                _log.LogDebug(
                    "NFS stat (file) failed for '{Path}' on {Server}:{Export} (v{Version}, rc={Rc}): {Error}",
                    nfsPath,
                    _config.Server,
                    _config.Export,
                    _config.Version,
                    rc,
                    LibNfs.GetError(_nfs)
                );
                return false;
            }
            return stat.FileType == LibNfs.S_IFREG;
        }
        finally
        {
            _lock.Release();
        }
    }

    public bool DirectoryExists(string path)
    {
        string nfsPath = ToNfsPath(path);
        _lock.Wait();
        try
        {
            int rc = LibNfs.Stat64(_nfs, nfsPath, out LibNfs.NfsStat64 stat);
            if (rc != 0)
            {
                _log.LogDebug(
                    "NFS stat (dir) failed for '{Path}' on {Server}:{Export} (v{Version}, rc={Rc}): {Error}",
                    nfsPath,
                    _config.Server,
                    _config.Export,
                    _config.Version,
                    rc,
                    LibNfs.GetError(_nfs)
                );
                return false;
            }
            return stat.FileType == LibNfs.S_IFDIR;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void CreateDirectory(string path)
    {
        string nfsPath = ToNfsPath(path);
        // Walk from root and create each segment that doesn't exist
        _lock.Wait();
        try
        {
            EnsureDirectoryRecursive(nfsPath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void DeleteFile(string path)
    {
        string nfsPath = ToNfsPath(path);
        _lock.Wait();
        try
        {
            int rc = LibNfs.Unlink(_nfs, nfsPath);
            // Idempotent — ignore ENOENT (-2)
            if (
                rc != 0
                && rc != -2
                && !LibNfs.GetError(_nfs).Contains("ENOENT", StringComparison.OrdinalIgnoreCase)
            )
                throw new IOException($"NFS unlink failed for '{path}': {LibNfs.GetError(_nfs)}");
        }
        finally
        {
            _lock.Release();
        }
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        string nfsPath = ToNfsPath(path);
        _lock.Wait();
        try
        {
            if (recursive)
                DeleteDirectoryRecursive(nfsPath);
            else
            {
                int rc = LibNfs.RmDir(_nfs, nfsPath);
                if (
                    rc != 0
                    && rc != -2
                    && !LibNfs.GetError(_nfs).Contains("ENOENT", StringComparison.OrdinalIgnoreCase)
                )
                    throw new IOException(
                        $"NFS rmdir failed for '{path}': {LibNfs.GetError(_nfs)}"
                    );
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public long GetFileSize(string path)
    {
        string nfsPath = ToNfsPath(path);
        _lock.Wait();
        try
        {
            int rc = LibNfs.Stat64(_nfs, nfsPath, out LibNfs.NfsStat64 stat);
            if (rc != 0)
                throw new FileNotFoundException(
                    $"NFS stat failed for '{path}': {LibNfs.GetError(_nfs)}"
                );
            return (long)stat.Size;
        }
        finally
        {
            _lock.Release();
        }
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        string nfsPath = ToNfsPath(path);
        _lock.Wait();
        try
        {
            int rc = LibNfs.Stat64(_nfs, nfsPath, out LibNfs.NfsStat64 stat);
            if (rc != 0)
                throw new FileNotFoundException(
                    $"NFS stat failed for '{path}': {LibNfs.GetError(_nfs)}"
                );
            return DateTimeOffset
                .FromUnixTimeSeconds((long)stat.MtimeSec)
                .AddTicks((long)(stat.MtimeNsec / 100))
                .UtcDateTime;
        }
        finally
        {
            _lock.Release();
        }
    }

    public DateTime GetCreationTimeUtc(string path)
    {
        string nfsPath = ToNfsPath(path);
        _lock.Wait();
        try
        {
            int rc = LibNfs.Stat64(_nfs, nfsPath, out LibNfs.NfsStat64 stat);
            if (rc != 0)
                throw new FileNotFoundException(
                    $"NFS stat failed for '{path}': {LibNfs.GetError(_nfs)}"
                );
            return DateTimeOffset
                .FromUnixTimeSeconds((long)stat.CtimeSec)
                .AddTicks((long)(stat.CtimeNsec / 100))
                .UtcDateTime;
        }
        finally
        {
            _lock.Release();
        }
    }

    public DateTime GetLastAccessTimeUtc(string path)
    {
        string nfsPath = ToNfsPath(path);
        _lock.Wait();
        try
        {
            int rc = LibNfs.Stat64(_nfs, nfsPath, out LibNfs.NfsStat64 stat);
            if (rc != 0)
                throw new FileNotFoundException(
                    $"NFS stat failed for '{path}': {LibNfs.GetError(_nfs)}"
                );
            return DateTimeOffset
                .FromUnixTimeSeconds((long)stat.AtimeSec)
                .AddTicks((long)(stat.AtimeNsec / 100))
                .UtcDateTime;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Stream OpenRead(string path)
    {
        string nfsPath = ToNfsPath(path);
        _lock.Wait();
        try
        {
            int rc = LibNfs.Stat64(_nfs, nfsPath, out LibNfs.NfsStat64 stat);
            if (rc != 0)
                throw new FileNotFoundException($"NFS file not found: '{path}'");

            int openRc = LibNfs.Open(_nfs, nfsPath, LibNfs.O_RDONLY, out IntPtr fh);
            if (openRc != 0)
                throw new IOException(
                    $"NFS open (read) failed for '{path}': {LibNfs.GetError(_nfs)}"
                );

            return new NfsReadStream(_nfs, fh, (long)stat.Size, _lock);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Stream OpenWrite(string path, bool overwrite)
    {
        string nfsPath = ToNfsPath(path);
        _lock.Wait();
        try
        {
            if (!overwrite && FileExistsNoLock(nfsPath))
                throw new IOException(
                    $"Cannot write to '{path}': file already exists and overwrite is false."
                );

            // UNCHECKED (overwrite) or GUARDED (fail if exists) — creat truncates in both cases
            // since we already checked overwrite above; just use creat with GUARDED via O_EXCL
            int flags = overwrite
                ? LibNfs.O_WRONLY | LibNfs.O_CREAT | LibNfs.O_TRUNC
                : LibNfs.O_WRONLY | LibNfs.O_CREAT | LibNfs.O_EXCL;

            int rc = LibNfs.Open(_nfs, nfsPath, flags, out IntPtr fh);
            if (rc != 0)
            {
                // Fall back to creat() which always truncates
                rc = LibNfs.Creat(_nfs, nfsPath, LibNfs.DefaultFileMode, out fh);
                if (rc != 0)
                    throw new IOException(
                        $"NFS creat failed for '{path}': {LibNfs.GetError(_nfs)}"
                    );
            }

            return new NfsWriteStream(_nfs, fh, _lock);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void MoveFile(string source, string destination)
    {
        string srcPath = ToNfsPath(source);
        string dstPath = ToNfsPath(destination);
        _lock.Wait();
        try
        {
            int rc = LibNfs.Rename(_nfs, srcPath, dstPath);
            if (rc != 0)
                throw new IOException(
                    $"NFS rename '{source}' -> '{destination}' failed: {LibNfs.GetError(_nfs)}"
                );
        }
        finally
        {
            _lock.Release();
        }
    }

    public void CopyFile(string source, string destination, bool overwrite)
    {
        // NFS has no native copy RPC; read source, write destination
        if (!overwrite && FileExists(destination))
            throw new IOException(
                $"Cannot copy to '{destination}': file already exists and overwrite is false."
            );

        using Stream src = OpenRead(source);
        using Stream dst = OpenWrite(destination, overwrite: true);
        src.CopyTo(dst);
    }

    public IEnumerable<string> EnumerateFileSystemEntries(
        string directory,
        string searchPattern,
        SearchOption option
    )
    {
        string nfsPath = ToNfsPath(directory);
        List<string> results = [];
        _lock.Wait();
        try
        {
            CollectEntries(nfsPath, directory, searchPattern, option, results);
        }
        finally
        {
            _lock.Release();
        }
        return results;
    }

    public string GetFullPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith('/'))
            normalized = _config.Export.TrimEnd('/') + "/" + normalized.TrimStart('/');

        // Resolve ".." and "." segments
        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Stack<string> stack = new();
        foreach (string segment in segments)
        {
            if (segment == "..")
            {
                if (stack.Count > 0)
                    stack.Pop();
            }
            else if (segment != ".")
            {
                stack.Push(segment);
            }
        }
        return "/" + string.Join("/", stack.Reverse());
    }

    public string? ResolveLinkTarget(string path)
    {
        string nfsPath = ToNfsPath(path);
        _lock.Wait();
        try
        {
            int rc = LibNfs.Lstat64(_nfs, nfsPath, out LibNfs.NfsStat64 stat);
            if (rc != 0 || stat.FileType != LibNfs.S_IFLNK)
                return null;

            IntPtr buf = Marshal.AllocHGlobal(4096);
            try
            {
                int linkRc = LibNfs.Readlink(_nfs, nfsPath, buf, 4096);
                if (linkRc < 0)
                    return null;
                return Marshal.PtrToStringUTF8(buf);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public bool IsHidden(string path)
    {
        string name = System.IO.Path.GetFileName(path.Replace('\\', '/'));
        return name.StartsWith('.') && name.Length > 1;
    }

    public void MoveDirectory(string source, string destination)
    {
        // NFS RENAME works for directories too
        string srcPath = ToNfsPath(source);
        string dstPath = ToNfsPath(destination);
        _lock.Wait();
        try
        {
            int rc = LibNfs.Rename(_nfs, srcPath, dstPath);
            if (rc != 0)
                throw new IOException(
                    $"NFS rename directory '{source}' -> '{destination}' failed: {LibNfs.GetError(_nfs)}"
                );
        }
        finally
        {
            _lock.Release();
        }
    }

    // -----------------------------------------------------------------------
    // Export enumeration (mount-protocol, no file context needed)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Queries the NFS server's mount daemon for its export list.
    /// Returns an ordered list of export paths (e.g. ["/mnt/Vault/Media"]).
    /// Returns null when the server is unreachable or returns no exports.
    /// Wraps the blocking libnfs call in a Task with a timeout.
    /// </summary>
    public static async Task<List<string>?> GetExportsAsync(
        string server,
        int timeoutMs = 10_000,
        ILogger? logger = null
    )
    {
        ILogger log = logger ?? NullLogger.Instance;
        using CancellationTokenSource cts = new(timeoutMs);

        try
        {
            return await Task.Run(() => GetExportsBlocking(server, log), cts.Token);
        }
        catch (OperationCanceledException)
        {
            log.LogWarning(
                "NFS export discovery timed out after {Timeout}ms for {Server}",
                timeoutMs,
                server
            );
            return null;
        }
    }

    private static List<string>? GetExportsBlocking(string server, ILogger log)
    {
        // Path 1 — NFSv3 mount protocol (port 111 portmap → mountd).
        // This is the "official" way but requires the server to register
        // mountd with rpcbind. TrueNAS / vanilla NFSv4-only servers often
        // skip this, returning an empty list.
        List<string>? v3 = TryV3MountGetExports(server, log);
        if (v3 is { Count: > 0 })
        {
            log.LogInformation(
                "NFS export discovery: v3 mount-protocol returned {Count} exports for {Server}",
                v3.Count,
                server
            );
            return v3;
        }
        log.LogInformation(
            "NFS export discovery: v3 mount-protocol returned no exports for {Server}, falling back to v4 root walk",
            server
        );

        // Path 2 — NFSv4 pseudo-fs walk. Mount the server's root as v4 and
        // list immediate sub-directories. On TrueNAS/Linux with NFSv4 only,
        // this surfaces real exports (e.g. /mnt/Vault/Media) via the
        // pseudo-filesystem the v4 server exposes from /.
        return TryV4RootListing(server, log);
    }

    private static List<string>? TryV3MountGetExports(string server, ILogger log)
    {
        IntPtr head = LibNfs.MountGetExports(server);
        if (head == IntPtr.Zero)
            return null;

        List<string> exports = [];
        try
        {
            IntPtr current = head;
            while (current != IntPtr.Zero)
            {
                LibNfs.ExportEntry entry = Marshal.PtrToStructure<LibNfs.ExportEntry>(current);
                if (entry.ExDir != IntPtr.Zero)
                {
                    string? path = Marshal.PtrToStringUTF8(entry.ExDir);
                    if (!string.IsNullOrWhiteSpace(path))
                        exports.Add(path);
                }
                current = entry.ExNext;
            }
        }
        finally
        {
            LibNfs.MountFreeExportList(head);
        }

        return exports.Count > 0 ? exports : null;
    }

    /// <summary>
    /// Common NAS pseudo-fs roots to probe when the server's NFSv4 root ("/")
    /// returns an empty listing. TrueNAS / FreeNAS expose datasets under
    /// /mnt/&lt;pool&gt;; Synology under /volume1; Linux NFS servers commonly
    /// export /exports or /srv. Probed in order; first non-empty wins.
    /// </summary>
    private static readonly string[] CommonV4Roots =
    [
        "/",
        "/mnt",
        "/volume1",
        "/volume2",
        "/exports",
        "/srv",
        "/data",
    ];

    private static List<string>? TryV4RootListing(string server, ILogger log)
    {
        IntPtr ctx = LibNfs.InitContext();
        if (ctx == IntPtr.Zero)
        {
            log.LogWarning(
                "NFSv4 export discovery: nfs_init_context returned null for {Server}",
                server
            );
            return null;
        }

        try
        {
            if (LibNfs.SetVersion(ctx, 4) != 0)
            {
                log.LogWarning(
                    "NFSv4 export discovery: nfs_set_version(4) failed for {Server} — {Error}",
                    server,
                    LibNfs.GetError(ctx)
                );
                return null;
            }

            // Mount the v4 pseudo-root. libnfs accepts "/" as the export
            // path for v4 to land at the server's NFSv4 PUTROOTFH.
            if (LibNfs.Mount(ctx, server, "/") != 0)
            {
                log.LogWarning(
                    "NFSv4 export discovery: nfs_mount({Server}, '/') failed — {Error}",
                    server,
                    LibNfs.GetError(ctx)
                );
                return null;
            }

            // Walk well-known pseudo-fs roots. Many NFSv4 servers (TrueNAS in
            // particular) don't expose an enumerable namespace from "/" even
            // when the mount succeeds — they only surface explicitly configured
            // exports. Walking from /mnt, /volume1, etc. catches the common
            // NAS layouts.
            foreach (string probeRoot in CommonV4Roots)
            {
                List<string> roots = [];
                CollectV4Children(ctx, probeRoot, maxDepth: 3, roots);
                if (roots.Count > 0)
                {
                    log.LogInformation(
                        "NFSv4 export discovery: walked {Probe} on {Server}, found {Count} dirs",
                        probeRoot,
                        server,
                        roots.Count
                    );
                    return roots;
                }
            }

            log.LogWarning(
                "NFSv4 export discovery: walked v4 root + {Count} fallback paths on {Server}, all empty — server may only expose explicit export paths (try entering manually)",
                CommonV4Roots.Length - 1,
                server
            );
            return null;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "NFSv4 export discovery threw for {Server}", server);
            return null;
        }
        finally
        {
            LibNfs.DestroyContext(ctx);
        }
    }

    private static void CollectV4Children(IntPtr ctx, string path, int maxDepth, List<string> sink)
    {
        if (maxDepth <= 0)
            return;
        if (LibNfs.OpenDir(ctx, path, out IntPtr dir) != 0)
            return;

        try
        {
            while (true)
            {
                IntPtr entryPtr = LibNfs.ReadDir(ctx, dir);
                if (entryPtr == IntPtr.Zero)
                    break;

                LibNfs.NfsDirent entry = Marshal.PtrToStructure<LibNfs.NfsDirent>(entryPtr);
                string? name = LibNfs.ReadDirentName(entry);
                if (string.IsNullOrEmpty(name) || name == "." || name == "..")
                    continue;
                if (name.StartsWith('.'))
                    continue;
                if (entry.Type != LibNfs.NF3DIR)
                    continue;

                string child = path == "/" ? "/" + name : path + "/" + name;
                sink.Add(child);

                // Descend one more level so /mnt/Vault/Media-style mount
                // points are captured even when only /mnt is listed at root.
                CollectV4Children(ctx, child, maxDepth - 1, sink);
            }
        }
        finally
        {
            LibNfs.CloseDir(ctx, dir);
        }
    }

    // -----------------------------------------------------------------------
    // Directory listing helper (for storage browser)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Lists immediate subdirectories at <paramref name="relativePath"/> within
    /// this driver's mounted export. Hidden entries (dot-prefixed) are excluded.
    /// </summary>
    public List<(string Name, bool IsDirectory)> ListDirectories(string relativePath)
    {
        string nfsPath = string.IsNullOrWhiteSpace(relativePath) ? "/" : ToNfsPath(relativePath);

        List<(string, bool)> results = [];
        _lock.Wait();
        try
        {
            int openRc = LibNfs.OpenDir(_nfs, nfsPath, out IntPtr dir);
            if (openRc != 0)
                return results;

            try
            {
                while (true)
                {
                    IntPtr entryPtr = LibNfs.ReadDir(_nfs, dir);
                    if (entryPtr == IntPtr.Zero)
                        break;

                    LibNfs.NfsDirent entry = Marshal.PtrToStructure<LibNfs.NfsDirent>(entryPtr);
                    string? name = LibNfs.ReadDirentName(entry);
                    if (
                        string.IsNullOrEmpty(name)
                        || name == "."
                        || name == ".."
                        || name.StartsWith('.')
                    )
                        continue;

                    if (entry.Type == LibNfs.NF3DIR)
                        results.Add((name, true));
                }
            }
            finally
            {
                LibNfs.CloseDir(_nfs, dir);
            }
        }
        finally
        {
            _lock.Release();
        }

        return results;
    }

    // -----------------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        LibNfs.Umount(_nfs);
        LibNfs.DestroyContext(_nfs);
        _lock.Dispose();
    }

    // -----------------------------------------------------------------------
    // Internals
    // -----------------------------------------------------------------------

    private string ToNfsPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith('/'))
            return "/" + normalized;
        return normalized;
    }

    private bool FileExistsNoLock(string nfsPath)
    {
        int rc = LibNfs.Stat64(_nfs, nfsPath, out LibNfs.NfsStat64 stat);
        return rc == 0 && stat.FileType == LibNfs.S_IFREG;
    }

    private void EnsureDirectoryRecursive(string nfsPath)
    {
        if (nfsPath == "/" || nfsPath == string.Empty)
            return;

        int checkRc = LibNfs.Stat64(_nfs, nfsPath, out LibNfs.NfsStat64 existing);
        if (checkRc == 0 && existing.FileType == LibNfs.NF3DIR)
            return;

        string parent = System.IO.Path.GetDirectoryName(nfsPath)?.Replace('\\', '/') ?? "/";
        if (parent != nfsPath)
            EnsureDirectoryRecursive(parent);

        int mkRc = LibNfs.MkDir(_nfs, nfsPath);
        if (
            mkRc != 0
            && !LibNfs.GetError(_nfs).Contains("EEXIST", StringComparison.OrdinalIgnoreCase)
        )
            throw new IOException($"NFS mkdir failed for '{nfsPath}': {LibNfs.GetError(_nfs)}");
    }

    private void DeleteDirectoryRecursive(string nfsPath)
    {
        int openRc = LibNfs.OpenDir(_nfs, nfsPath, out IntPtr dir);
        if (openRc != 0)
        {
            if (LibNfs.GetError(_nfs).Contains("ENOENT", StringComparison.OrdinalIgnoreCase))
                return;
            throw new IOException($"NFS opendir failed for '{nfsPath}': {LibNfs.GetError(_nfs)}");
        }

        try
        {
            while (true)
            {
                IntPtr entryPtr = LibNfs.ReadDir(_nfs, dir);
                if (entryPtr == IntPtr.Zero)
                    break;

                LibNfs.NfsDirent entry = Marshal.PtrToStructure<LibNfs.NfsDirent>(entryPtr);
                string? name = LibNfs.ReadDirentName(entry);
                if (string.IsNullOrEmpty(name) || name == "." || name == "..")
                    continue;

                string childPath = nfsPath.TrimEnd('/') + "/" + name;

                if (entry.Type == LibNfs.NF3DIR)
                    DeleteDirectoryRecursive(childPath);
                else
                {
                    int unlinkRc = LibNfs.Unlink(_nfs, childPath);
                    if (unlinkRc != 0)
                        throw new IOException(
                            $"NFS unlink failed for '{childPath}': {LibNfs.GetError(_nfs)}"
                        );
                }
            }
        }
        finally
        {
            LibNfs.CloseDir(_nfs, dir);
        }

        int rmdirRc = LibNfs.RmDir(_nfs, nfsPath);
        if (
            rmdirRc != 0
            && !LibNfs.GetError(_nfs).Contains("ENOENT", StringComparison.OrdinalIgnoreCase)
        )
            throw new IOException($"NFS rmdir failed for '{nfsPath}': {LibNfs.GetError(_nfs)}");
    }

    private void CollectEntries(
        string nfsDir,
        string virtualDir,
        string searchPattern,
        SearchOption option,
        List<string> results
    )
    {
        int openRc = LibNfs.OpenDir(_nfs, nfsDir, out IntPtr dir);
        if (openRc != 0)
        {
            _log.LogWarning(
                "NFS opendir failed for '{Path}' on {Server}:{Export} (v{Version}, rc={Rc}): {Error}",
                nfsDir,
                _config.Server,
                _config.Export,
                _config.Version,
                openRc,
                LibNfs.GetError(_nfs)
            );
            return;
        }

        try
        {
            while (true)
            {
                IntPtr entryPtr = LibNfs.ReadDir(_nfs, dir);
                if (entryPtr == IntPtr.Zero)
                    break;

                LibNfs.NfsDirent entry = Marshal.PtrToStructure<LibNfs.NfsDirent>(entryPtr);
                string? name = LibNfs.ReadDirentName(entry);
                if (string.IsNullOrEmpty(name) || name == "." || name == "..")
                    continue;

                string virtualPath = virtualDir.TrimEnd('/') + "/" + name;
                string childNfsPath = nfsDir.TrimEnd('/') + "/" + name;

                if (MatchesPattern(name, searchPattern))
                    results.Add(virtualPath);

                if (option == SearchOption.AllDirectories && entry.Type == LibNfs.NF3DIR)
                    CollectEntries(childNfsPath, virtualPath, searchPattern, option, results);
            }
        }
        finally
        {
            LibNfs.CloseDir(_nfs, dir);
        }
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        if (pattern == "*" || string.IsNullOrEmpty(pattern))
            return true;

        string regexPattern =
            "^"
            + string.Concat(
                pattern.Select(c =>
                    c switch
                    {
                        '*' => ".*",
                        '?' => ".",
                        '.' => "\\.",
                        _ => Regex.Escape(c.ToString()),
                    }
                )
            )
            + "$";

        return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
    }
}
