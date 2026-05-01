using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
    private bool _disposed;

    internal NfsStorageDriver(NfsDriverConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _nfs = LibNfs.InitContext();
        if (_nfs == IntPtr.Zero)
            throw new InvalidOperationException(
                "nfs_init_context returned null — libnfs not available."
            );

        if (config.Uid.HasValue)
            LibNfs.SetUid(_nfs, config.Uid.Value);
        if (config.Gid.HasValue)
            LibNfs.SetGid(_nfs, config.Gid.Value);

        int rc = LibNfs.Mount(_nfs, config.Server, config.Export);
        if (rc != 0)
            throw new IOException(
                $"NFS mount failed for {config.Server}:{config.Export} — {LibNfs.GetError(_nfs)}"
            );
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
                return false;
            return stat.FileType == LibNfs.NF3REG;
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
                return false;
            return stat.FileType == LibNfs.NF3DIR;
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

            return new NfsReadStream(_nfs, fh, (long)stat.Size);
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

            return new NfsWriteStream(_nfs, fh);
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
            if (rc != 0 || stat.FileType != LibNfs.NF3LNK)
                return null;

            IntPtr buf = Marshal.AllocHGlobal(4096);
            try
            {
                int linkRc = LibNfs.Readlink(_nfs, nfsPath, buf, 4096);
                if (linkRc < 0)
                    return null;
                return Marshal.PtrToStringAnsi(buf);
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
        return rc == 0 && stat.FileType == LibNfs.NF3REG;
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
                string name = entry.Name;
                if (name == "." || name == "..")
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
            return;

        try
        {
            while (true)
            {
                IntPtr entryPtr = LibNfs.ReadDir(_nfs, dir);
                if (entryPtr == IntPtr.Zero)
                    break;

                LibNfs.NfsDirent entry = Marshal.PtrToStructure<LibNfs.NfsDirent>(entryPtr);
                string name = entry.Name;
                if (name == "." || name == "..")
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
