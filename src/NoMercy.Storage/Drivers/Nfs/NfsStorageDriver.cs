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

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage.Common;
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
    public string BackendLabel => "NFS";

    // NFS4 servers reap idle client state after ~90s by default
    // (CB_PATH_DOWN / NFS4ERR_EXPIRED). Keep-alive at 30s leaves headroom for
    // packet loss and clock skew. NFS3 has no lease state so the ping is a
    // harmless GETATTR there.
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(seconds: 30);

    // libnfs's mount RPC can transiently time out ("command timed out") when the
    // NFS server is momentarily busy — e.g. mid-encode — even though it is
    // healthy and answering on 2049. Since the driver is constructed lazily the
    // first time an operation touches a folder, a single blip otherwise aborts
    // whatever triggered construction: a rescan lands in FailedJobs and the user
    // sees "nothing happened". Every other libnfs call in this driver already
    // retries on transient failure; the initial mount was the one gap. Rebuild a
    // fresh context per attempt (a failed v4 mount can leave partial client
    // state) with a short escalating backoff.
    private const int MountAttempts = 3;

    private readonly NfsDriverConfig _config;
    private IntPtr _nfs;
    private readonly SemaphoreSlim _lock = new(initialCount: 1, maxCount: 1);
    private readonly ILogger _log;
    private readonly Timer? _keepAlive;
    private readonly ILibNfs _libNfs;
    private bool _disposed;

    // Per-native-call transfer size handed to the read/write streams. Defaults to
    // the streams' own default; the throughput sweep sets it to measure the curve.
    // Not exposed on IStorageDriver — an internal tuning seam only.
    internal int StreamChunkSize { get; set; } = 1024 * 1024;

    // Per-instance NFSv4 client identity. libnfs defaults the client-id to the
    // hostname, so two drivers in one process share open-owner seqid state and
    // collide with NFS4ERR_BAD_SEQID. A unique id per driver makes them coexist.
    private readonly string _clientId = $"nomercy-{Environment.ProcessId}-{Guid.NewGuid():N}";

    // On Windows the libnfs build resolves hostnames with getaddrinfo, which
    // only works once Winsock has been initialised (WSAStartup) for the
    // process. The .NET socket stack calls WSAStartup the first time
    // System.Net.Sockets is touched and never tears it down, so forcing a
    // throwaway Socket here guarantees libnfs can resolve the server. Without
    // it, every mount fails with "Can not resolv into IPv4/v6 structure" before
    // a single packet is sent — i.e. NFS is unusable on Windows.
    private static int _winsockReady;

    private static void EnsureWinsockInitialized()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        if (Interlocked.Exchange(location1: ref _winsockReady, value: 1) == 1)
            return;

        try
        {
            using System.Net.Sockets.Socket socket = new(
                addressFamily: System.Net.Sockets.AddressFamily.InterNetwork,
                socketType: System.Net.Sockets.SocketType.Stream,
                protocolType: System.Net.Sockets.ProtocolType.Tcp
            );
        }
        catch
        {
            // Even a failed construction has run the static Winsock init.
        }
    }

    public NfsStorageDriver(NfsDriverConfig config, ILogger? log = null)
        : this(config: config, libNfs: LibNfsPInvoke.Instance, log: log) { }

    /// <summary>
    /// Test-friendly constructor. Production code uses the parameterless overload
    /// which forwards <see cref="LibNfsPInvoke.Instance"/>; tests pass a fake
    /// <see cref="ILibNfs"/> to inject deterministic rc/error-string sequences.
    /// </summary>
    internal NfsStorageDriver(NfsDriverConfig config, ILibNfs libNfs, ILogger? log = null)
    {
        _config = config ?? throw new ArgumentNullException(paramName: nameof(config));
        _libNfs = libNfs ?? throw new ArgumentNullException(paramName: nameof(libNfs));
        _log = log ?? NullLogger.Instance;

        // Guarantee Winsock is up before libnfs resolves the server (Windows).
        EnsureWinsockInitialized();

        InitAndConfigureContext();
        MountWithRetry();

        // Idle client state on NFS4 expires after ~90s, after which any open
        // returns NFS4ERR_EXPIRED. Stat the export root every 30s so the
        // server treats us as alive even when no streaming reads are in
        // flight (typical between user sessions).
        _keepAlive = new(callback: KeepAliveTick, state: null, dueTime: KeepAliveInterval, period: KeepAliveInterval);
    }

    private void KeepAliveTick(object? _)
    {
        if (_disposed)
            return;
        if (!_lock.Wait(timeout: TimeSpan.Zero))
            return; // real work in progress — no need to renew separately
        try
        {
            int rc = _libNfs.Stat64(nfs: _nfs, path: "/", stat: out LibNfs.NfsStat64 stat);
            _ = stat;
            if (rc != 0)
            {
                string err = _libNfs.GetError(nfs: _nfs);
                if (IsExpiredStateError(rc: rc, err: err))
                {
                    // Self-heal proactively so the next user-facing op finds a
                    // live session instead of having to remount mid-stream.
                    try
                    {
                        Remount();
                    }
                    catch (Exception ex)
                    {
                        _log.LogDebug(
                            exception: ex,
                            message: "NFS keep-alive remount failed on {Server}:{Export}", args: [_config.Server, _config.Export]
                        );
                    }
                }
                else
                {
                    _log.LogDebug(
                        message: "NFS keep-alive stat / failed on {Server}:{Export} (v{Version}, rc={Rc}): {Error}", args: [_config.Server, _config.Export, _config.Version, rc, err]
                    );
                }
            }
        }
        catch
        {
            // Best effort — never let the timer crash the process.
        }
        finally
        {
            _lock.Release();
        }
    }

    // NFSv4 reaps idle client state after the lease (~90s). When that happens,
    // libnfs starts returning NFS4ERR_EXPIRED(-11), NFS4ERR_BAD_SESSION,
    // NFS4ERR_BAD_STATEID, NFS4ERR_STALE_CLIENTID, or NFS4ERR_BAD_SEQID for every
    // operation. The keep-alive timer prevents this most of the time, but a
    // paused client (laptop sleep, network blip) — or a server still settling
    // its client table just after (re)mount — can still trip it. Detect the
    // stale-client-state family and tear down + remount the libnfs context so the
    // next attempt succeeds with a fresh client id and sequence.
    // Give this libnfs context a unique NFSv4 client name + verifier before
    // mount so concurrent/sequential drivers in one process don't share seqid
    // state. NFSv4 only — libnfs ignores these for v3.
    private void ApplyClientIdentity() => ApplyClientIdentity(ctx: _nfs, clientId: _clientId);

    // Stamp a libnfs context with a unique NFSv4 client name + verifier. Each
    // distinct clientId becomes an independent clientid/open-owner on the
    // server, so its open-seqid sequence is tracked separately and cannot
    // collide with another context's. NFSv4 only — libnfs ignores these for v3.
    private void ApplyClientIdentity(IntPtr ctx, string clientId)
    {
        if (_config.Version != 4)
            return;
        _libNfs.SetClientName(nfs: ctx, id: clientId);
        _libNfs.SetVerifier(nfs: ctx, verifier: clientId);
    }

    private static bool IsExpiredStateError(int rc, string err)
    {
        if (rc == -11)
            return true;
        return err.Contains(value: "NFS4ERR_EXPIRED", comparisonType: StringComparison.OrdinalIgnoreCase)
            || err.Contains(value: "NFS4ERR_BAD_SESSION", comparisonType: StringComparison.OrdinalIgnoreCase)
            || err.Contains(value: "NFS4ERR_BAD_STATEID", comparisonType: StringComparison.OrdinalIgnoreCase)
            || err.Contains(value: "NFS4ERR_STALE_CLIENTID", comparisonType: StringComparison.OrdinalIgnoreCase)
            || err.Contains(value: "NFS4ERR_BAD_SEQID", comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    // Init a fresh libnfs context and apply protocol version, credentials, and
    // NFSv4 client identity — everything the mount needs, short of the mount
    // itself. Shared by construction and by the per-attempt rebuild in
    // MountWithRetry. Throws if libnfs is unavailable or rejects the version.
    private void InitAndConfigureContext()
    {
        _nfs = _libNfs.InitContext();
        if (_nfs == IntPtr.Zero)
            throw new InvalidOperationException(
                message: "nfs_init_context returned null — libnfs not available."
            );

        // Set NFS protocol version BEFORE mount. libnfs defaults to NFSv3
        // when not set; we call this even for v3 so the value is explicit
        // and surfaces a clear error if the server doesn't speak that version.
        int versionRc = _libNfs.SetVersion(nfs: _nfs, version: _config.Version);
        if (versionRc != 0)
        {
            string err = _libNfs.GetError(nfs: _nfs);
            _libNfs.DestroyContext(nfs: _nfs);
            _nfs = IntPtr.Zero;
            throw new IOException(message: $"nfs_set_version({_config.Version}) failed — {err}");
        }

        if (_config.Uid.HasValue)
            _libNfs.SetUid(nfs: _nfs, uid: _config.Uid.Value);
        if (_config.Gid.HasValue)
            _libNfs.SetGid(nfs: _nfs, gid: _config.Gid.Value);

        ApplyClientIdentity();
    }

    // Mount the configured context, retrying transient failures on a fresh
    // context each time. Assumes InitAndConfigureContext() has already run.
    // Throws once the attempt budget is exhausted — a persistently unreachable
    // server is a real error, not something to swallow.
    private void MountWithRetry()
    {
        for (int attempt = 1; ; attempt++)
        {
            int rc = _libNfs.Mount(nfs: _nfs, server: _config.Server, exportPath: _config.Export);
            if (rc == 0)
                return;

            string err = _libNfs.GetError(nfs: _nfs);
            _libNfs.DestroyContext(nfs: _nfs);
            _nfs = IntPtr.Zero;

            if (attempt >= MountAttempts)
                throw new IOException(
                    message: $"NFS{_config.Version} mount failed for {_config.Server}:{_config.Export} after {attempt} attempts — {err}"
                );

            _log.LogWarning(
                message: "NFS{Version} mount attempt {Attempt}/{Max} failed for {Server}:{Export} — {Error}; retrying", args: [_config.Version, attempt, MountAttempts, _config.Server, _config.Export, err]
            );

            Thread.Sleep(timeout: TimeSpan.FromMilliseconds(milliseconds: 250 * attempt));
            InitAndConfigureContext();
        }
    }

    // Caller MUST hold _lock. Disposes the existing context and re-runs the
    // init/version/uid/gid/mount sequence used by the constructor. Throws on
    // failure — the remount path is the recovery; if it can't reconnect, the
    // server really is unreachable.
    private void Remount()
    {
        if (_nfs != IntPtr.Zero)
        {
            _libNfs.DestroyContext(nfs: _nfs);
            _nfs = IntPtr.Zero;
        }

        IntPtr ctx = _libNfs.InitContext();
        if (ctx == IntPtr.Zero)
            throw new IOException(message: "NFS remount: nfs_init_context returned null");

        int versionRc = _libNfs.SetVersion(nfs: ctx, version: _config.Version);
        if (versionRc != 0)
        {
            string err = _libNfs.GetError(nfs: ctx);
            _libNfs.DestroyContext(nfs: ctx);
            throw new IOException(
                message: $"NFS remount: nfs_set_version({_config.Version}) failed — {err}"
            );
        }

        if (_config.Uid.HasValue)
            _libNfs.SetUid(nfs: ctx, uid: _config.Uid.Value);
        if (_config.Gid.HasValue)
            _libNfs.SetGid(nfs: ctx, gid: _config.Gid.Value);

        if (_config.Version == 4)
        {
            // Reuse this driver's stable client id so the server sees the same
            // client across the remount (recovers the lease cleanly).
            _libNfs.SetClientName(nfs: ctx, id: _clientId);
            _libNfs.SetVerifier(nfs: ctx, verifier: _clientId);
        }

        int rc = _libNfs.Mount(nfs: ctx, server: _config.Server, exportPath: _config.Export);
        if (rc != 0)
        {
            string err = _libNfs.GetError(nfs: ctx);
            _libNfs.DestroyContext(nfs: ctx);
            throw new IOException(
                message: $"NFS remount failed for {_config.Server}:{_config.Export} — {err}"
            );
        }

        _nfs = ctx;
        _log.LogWarning(
            message: "NFS session expired; reconnected to {Server}:{Export} (v{Version})", args: [_config.Server, _config.Export, _config.Version]
        );
    }

    // -----------------------------------------------------------------------
    // EXPIRED-retry helpers — caller MUST hold _lock. Each helper performs a
    // single libnfs call, and on EXPIRED-class failure tears down + remounts
    // the context once before retrying. Used by every IStorageDriver entry
    // point that touches state-bearing NFSv4 operations so a stale lease
    // never propagates to callers.
    // -----------------------------------------------------------------------

    private int Stat64WithRetry(string nfsPath, out LibNfs.NfsStat64 stat)
    {
        int rc = _libNfs.Stat64(nfs: _nfs, path: nfsPath, stat: out stat);
        if (rc != 0 && IsExpiredStateError(rc: rc, err: _libNfs.GetError(nfs: _nfs)))
        {
            try
            {
                Remount();
            }
            catch
            {
                return rc;
            }
            rc = _libNfs.Stat64(nfs: _nfs, path: nfsPath, stat: out stat);
        }
        return rc;
    }

    private int OpenDirWithRetry(string nfsPath, out IntPtr dir)
    {
        int rc = _libNfs.OpenDir(nfs: _nfs, path: nfsPath, dir: out dir);
        if (rc != 0 && IsExpiredStateError(rc: rc, err: _libNfs.GetError(nfs: _nfs)))
        {
            try
            {
                Remount();
            }
            catch
            {
                return rc;
            }
            rc = _libNfs.OpenDir(nfs: _nfs, path: nfsPath, dir: out dir);
        }
        return rc;
    }

    private int UnlinkWithRetry(string nfsPath)
    {
        int rc = _libNfs.Unlink(nfs: _nfs, path: nfsPath);
        if (rc != 0 && IsExpiredStateError(rc: rc, err: _libNfs.GetError(nfs: _nfs)))
        {
            try
            {
                Remount();
            }
            catch
            {
                return rc;
            }
            rc = _libNfs.Unlink(nfs: _nfs, path: nfsPath);
        }
        return rc;
    }

    private int RenameWithRetry(string oldPath, string newPath)
    {
        int rc = _libNfs.Rename(nfs: _nfs, oldPath: oldPath, newPath: newPath);
        if (rc != 0 && IsExpiredStateError(rc: rc, err: _libNfs.GetError(nfs: _nfs)))
        {
            try
            {
                Remount();
            }
            catch
            {
                return rc;
            }
            rc = _libNfs.Rename(nfs: _nfs, oldPath: oldPath, newPath: newPath);
        }
        return rc;
    }

    private int RmDirWithRetry(string nfsPath)
    {
        int rc = _libNfs.RmDir(nfs: _nfs, path: nfsPath);
        if (rc != 0 && IsExpiredStateError(rc: rc, err: _libNfs.GetError(nfs: _nfs)))
        {
            try
            {
                Remount();
            }
            catch
            {
                return rc;
            }
            rc = _libNfs.RmDir(nfs: _nfs, path: nfsPath);
        }
        return rc;
    }

    private int MkDirWithRetry(string nfsPath)
    {
        int rc = _libNfs.MkDir(nfs: _nfs, path: nfsPath);
        if (rc != 0 && IsExpiredStateError(rc: rc, err: _libNfs.GetError(nfs: _nfs)))
        {
            try
            {
                Remount();
            }
            catch
            {
                return rc;
            }
            rc = _libNfs.MkDir(nfs: _nfs, path: nfsPath);
        }
        return rc;
    }

    // -----------------------------------------------------------------------
    // IStorageDriver
    // -----------------------------------------------------------------------

    public bool FileExists(string path)
    {
        string nfsPath = ToNfsPath(path: path);
        _lock.Wait();
        try
        {
            int rc = Stat64WithRetry(nfsPath: nfsPath, stat: out LibNfs.NfsStat64 stat);
            if (rc != 0)
            {
                // NFS4ERR_NOENT (-2) is the expected "no, that path doesn't
                // exist" outcome of a probe call — not an error. Real failures
                // (permission denied, RPC trouble, server gone) keep logging.
                if (rc != -2)
                {
                    _log.LogDebug(
                        message: "NFS stat (file) failed for '{Path}' on {Server}:{Export} (v{Version}, rc={Rc}): {Error}", args: [nfsPath, _config.Server, _config.Export, _config.Version, rc, _libNfs.GetError(nfs: _nfs)]
                    );
                }
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
        string nfsPath = ToNfsPath(path: path);
        _lock.Wait();
        try
        {
            int rc = Stat64WithRetry(nfsPath: nfsPath, stat: out LibNfs.NfsStat64 stat);
            if (rc != 0)
            {
                // NFS4ERR_NOENT (-2) is the expected "no, that path doesn't
                // exist" outcome of a probe call — not an error. Real failures
                // keep logging.
                if (rc != -2)
                {
                    _log.LogDebug(
                        message: "NFS stat (dir) failed for '{Path}' on {Server}:{Export} (v{Version}, rc={Rc}): {Error}", args: [nfsPath, _config.Server, _config.Export, _config.Version, rc, _libNfs.GetError(nfs: _nfs)]
                    );
                }
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
        string nfsPath = ToNfsPath(path: path);
        // Walk from root and create each segment that doesn't exist
        _lock.Wait();
        try
        {
            EnsureDirectoryRecursive(nfsPath: nfsPath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void DeleteFile(string path)
    {
        string nfsPath = ToNfsPath(path: path);
        _lock.Wait();
        try
        {
            int rc = UnlinkWithRetry(nfsPath: nfsPath);
            // Idempotent — ignore ENOENT (-2)
            if (
                rc != 0
                && rc != -2
                && !_libNfs.GetError(nfs: _nfs).Contains(value: "ENOENT", comparisonType: StringComparison.OrdinalIgnoreCase)
            )
                throw new IOException(message: $"NFS unlink failed for '{path}': {_libNfs.GetError(nfs: _nfs)}");
        }
        finally
        {
            _lock.Release();
        }
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        string nfsPath = ToNfsPath(path: path);
        _lock.Wait();
        try
        {
            if (recursive)
                DeleteDirectoryRecursive(nfsPath: nfsPath);
            else
            {
                int rc = RmDirWithRetry(nfsPath: nfsPath);
                if (
                    rc != 0
                    && rc != -2
                    && !_libNfs
                        .GetError(nfs: _nfs)
                        .Contains(value: "ENOENT", comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                    throw new IOException(
                        message: $"NFS rmdir failed for '{path}': {_libNfs.GetError(nfs: _nfs)}"
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
        string nfsPath = ToNfsPath(path: path);
        _lock.Wait();
        try
        {
            int rc = Stat64WithRetry(nfsPath: nfsPath, stat: out LibNfs.NfsStat64 stat);
            if (rc != 0)
                throw new FileNotFoundException(
                    message: $"NFS stat failed for '{path}': {_libNfs.GetError(nfs: _nfs)}"
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
        string nfsPath = ToNfsPath(path: path);
        _lock.Wait();
        try
        {
            int rc = Stat64WithRetry(nfsPath: nfsPath, stat: out LibNfs.NfsStat64 stat);
            if (rc != 0)
                throw new FileNotFoundException(
                    message: $"NFS stat failed for '{path}': {_libNfs.GetError(nfs: _nfs)}"
                );
            return DateTimeOffset
                .FromUnixTimeSeconds(seconds: (long)stat.MtimeSec)
                .AddTicks(ticks: (long)(stat.MtimeNsec / 100))
                .UtcDateTime;
        }
        finally
        {
            _lock.Release();
        }
    }

    public DateTime GetCreationTimeUtc(string path)
    {
        string nfsPath = ToNfsPath(path: path);
        _lock.Wait();
        try
        {
            int rc = Stat64WithRetry(nfsPath: nfsPath, stat: out LibNfs.NfsStat64 stat);
            if (rc != 0)
                throw new FileNotFoundException(
                    message: $"NFS stat failed for '{path}': {_libNfs.GetError(nfs: _nfs)}"
                );
            return DateTimeOffset
                .FromUnixTimeSeconds(seconds: (long)stat.CtimeSec)
                .AddTicks(ticks: (long)(stat.CtimeNsec / 100))
                .UtcDateTime;
        }
        finally
        {
            _lock.Release();
        }
    }

    public DateTime GetLastAccessTimeUtc(string path)
    {
        string nfsPath = ToNfsPath(path: path);
        _lock.Wait();
        try
        {
            int rc = Stat64WithRetry(nfsPath: nfsPath, stat: out LibNfs.NfsStat64 stat);
            if (rc != 0)
                throw new FileNotFoundException(
                    message: $"NFS stat failed for '{path}': {_libNfs.GetError(nfs: _nfs)}"
                );
            return DateTimeOffset
                .FromUnixTimeSeconds(seconds: (long)stat.AtimeSec)
                .AddTicks(ticks: (long)(stat.AtimeNsec / 100))
                .UtcDateTime;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Stream OpenRead(string path)
    {
        string nfsPath = ToNfsPath(path: path);
        _lock.Wait();
        try
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int rc = _libNfs.Stat64(nfs: _nfs, path: nfsPath, stat: out LibNfs.NfsStat64 stat);
                if (rc != 0)
                {
                    string err = _libNfs.GetError(nfs: _nfs);
                    if (attempt == 0 && IsExpiredStateError(rc: rc, err: err))
                    {
                        Remount();
                        continue;
                    }
                    throw new FileNotFoundException(message: $"NFS file not found: '{path}' ({err})");
                }

                int openRc = _libNfs.Open(nfs: _nfs, path: nfsPath, flags: LibNfs.O_RDONLY, fh: out IntPtr fh);
                if (openRc != 0)
                {
                    string err = _libNfs.GetError(nfs: _nfs);
                    if (attempt == 0 && IsExpiredStateError(rc: openRc, err: err))
                    {
                        Remount();
                        continue;
                    }
                    throw new IOException(message: $"NFS open (read) failed for '{path}': {err}");
                }

                try
                {
                    return new NfsReadStream(
                        nfs: _nfs,
                        fh: fh,
                        length: (long)stat.Size,
                        driverLock: _lock,
                        libNfs: _libNfs,
                        chunkSize: StreamChunkSize
                    );
                }
                catch
                {
                    // Stream constructor failure (OOM etc.) — close the libnfs
                    // file handle so we don't leak it inside the shared context.
                    _libNfs.Close(nfs: _nfs, fh: fh);
                    throw;
                }
            }

            throw new IOException(
                message: $"NFS open (read) failed for '{path}': retry after remount also failed"
            );
        }
        finally
        {
            _lock.Release();
        }
    }

    // Serialize libnfs context init/mount/open across all OpenReadIsolated
    // calls. Even with separate contexts, libnfs internal state (especially
    // NFSv4 session/clientid bookkeeping) does not survive concurrent
    // mount+open sequences from the same process — they trip BAD_SEQID(-22)
    // and EXPIRED(-11) at random under parallel scan workers.
    private static readonly SemaphoreSlim _isolatedOpenGate = new(initialCount: 1, maxCount: 1);

    /// <inheritdoc/>
    /// Opens a dedicated libnfs context for this call so concurrent
    /// AcquireLocalPath invocations cannot corrupt each other's NFSv4
    /// open-seqid sequence (NFS4ERR_BAD_SEQID).
    public Stream OpenReadIsolated(string path)
    {
        string nfsPath = ToNfsPath(path: path);

        _isolatedOpenGate.Wait();
        IntPtr ctx;
        IntPtr fh;
        long fileSize;
        try
        {
            ctx = _libNfs.InitContext();
            if (ctx == IntPtr.Zero)
                throw new InvalidOperationException(
                    message: "nfs_init_context returned null for isolated read context."
                );

            try
            {
                int versionRc = _libNfs.SetVersion(nfs: ctx, version: _config.Version);
                if (versionRc != 0)
                    throw new IOException(
                        message: $"Isolated NFS nfs_set_version({_config.Version}) failed — {_libNfs.GetError(nfs: ctx)}"
                    );

                if (_config.Uid.HasValue)
                    _libNfs.SetUid(nfs: ctx, uid: _config.Uid.Value);
                if (_config.Gid.HasValue)
                    _libNfs.SetGid(nfs: ctx, gid: _config.Gid.Value);

                // A FRESH unique client identity per isolated context — not the
                // driver's _clientId. Several isolated contexts are open at once
                // during a parallel scan; if they shared one clientid the server
                // would fold them into a single open-owner and their independent
                // local open-seqid counters would collide (NFS4ERR_BAD_SEQID).
                ApplyClientIdentity(ctx: ctx, clientId: $"nomercy-{Environment.ProcessId}-{Guid.NewGuid():N}");

                int mountRc = _libNfs.Mount(nfs: ctx, server: _config.Server, exportPath: _config.Export);
                if (mountRc != 0)
                    throw new IOException(
                        message: $"Isolated NFS mount failed for {_config.Server}:{_config.Export} — {_libNfs.GetError(nfs: ctx)}"
                    );

                _lock.Wait();
                try
                {
                    int statRc = Stat64WithRetry(nfsPath: nfsPath, stat: out LibNfs.NfsStat64 stat);
                    if (statRc != 0)
                        throw new FileNotFoundException(message: $"NFS file not found: '{path}'");
                    fileSize = (long)stat.Size;
                }
                finally
                {
                    _lock.Release();
                }

                int openRc = _libNfs.Open(nfs: ctx, path: nfsPath, flags: LibNfs.O_RDONLY, fh: out fh);
                if (openRc != 0)
                    throw new IOException(
                        message: $"NFS open (read) failed for '{path}': {_libNfs.GetError(nfs: ctx)}"
                    );
            }
            catch
            {
                _libNfs.Umount(nfs: ctx);
                _libNfs.DestroyContext(nfs: ctx);
                throw;
            }
        }
        finally
        {
            _isolatedOpenGate.Release();
        }

        try
        {
            return new IsolatedNfsReadStream(libNfs: _libNfs, ownedCtx: ctx, fh: fh, length: fileSize);
        }
        catch
        {
            _libNfs.Close(nfs: ctx, fh: fh);
            _libNfs.Umount(nfs: ctx);
            _libNfs.DestroyContext(nfs: ctx);
            throw;
        }
    }

    public Stream OpenWrite(string path, bool overwrite)
    {
        string nfsPath = ToNfsPath(path: path);
        _lock.Wait();
        try
        {
            if (!overwrite && FileExistsNoLock(nfsPath: nfsPath))
                throw new IOException(
                    message: $"Cannot write to '{path}': file already exists and overwrite is false."
                );

            // UNCHECKED (overwrite) or GUARDED (fail if exists) — creat truncates in both cases
            // since we already checked overwrite above; just use creat with GUARDED via O_EXCL
            int flags = overwrite
                ? LibNfs.O_WRONLY | LibNfs.O_CREAT | LibNfs.O_TRUNC
                : LibNfs.O_WRONLY | LibNfs.O_CREAT | LibNfs.O_EXCL;

            IntPtr fh = IntPtr.Zero;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int rc = _libNfs.Open(nfs: _nfs, path: nfsPath, flags: flags, fh: out fh);
                if (rc == 0)
                    break;

                string err = _libNfs.GetError(nfs: _nfs);
                if (attempt == 0 && IsExpiredStateError(rc: rc, err: err))
                {
                    Remount();
                    continue;
                }

                // Fall back to creat() which always truncates
                rc = _libNfs.Creat(nfs: _nfs, path: nfsPath, mode: LibNfs.DefaultFileMode, fh: out fh);
                if (rc == 0)
                    break;

                err = _libNfs.GetError(nfs: _nfs);
                if (attempt == 0 && IsExpiredStateError(rc: rc, err: err))
                {
                    Remount();
                    continue;
                }

                throw new IOException(message: $"NFS creat failed for '{path}': {err}");
            }

            try
            {
                return new NfsWriteStream(nfs: _nfs, fh: fh, driverLock: _lock, libNfs: _libNfs, chunkSize: StreamChunkSize);
            }
            catch
            {
                _libNfs.Close(nfs: _nfs, fh: fh);
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public void MoveFile(string source, string destination)
    {
        string srcPath = ToNfsPath(path: source);
        string dstPath = ToNfsPath(path: destination);
        _lock.Wait();
        try
        {
            int rc = RenameWithRetry(oldPath: srcPath, newPath: dstPath);
            if (rc != 0)
                throw new IOException(
                    message: $"NFS rename '{source}' -> '{destination}' failed: {_libNfs.GetError(nfs: _nfs)}"
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
        if (!overwrite && FileExists(path: destination))
            throw new IOException(
                message: $"Cannot copy to '{destination}': file already exists and overwrite is false."
            );

        using Stream src = OpenRead(path: source);
        using Stream dst = OpenWrite(path: destination, overwrite: true);
        src.CopyTo(destination: dst);
    }

    public IEnumerable<string> EnumerateFileSystemEntries(
        string directory,
        string searchPattern,
        SearchOption option
    )
    {
        string nfsPath = ToNfsPath(path: directory);
        List<string> results = [];
        _lock.Wait();
        try
        {
            CollectEntries(nfsDir: nfsPath, virtualDir: directory, searchPattern: searchPattern, option: option, results: results);
        }
        finally
        {
            _lock.Release();
        }
        return results;
    }

    public string GetFullPath(string path)
    {
        string normalized = path.Replace(oldChar: '\\', newChar: '/');
        if (!normalized.StartsWith(value: '/'))
            normalized = _config.Export.TrimEnd(trimChar: '/') + "/" + normalized.TrimStart(trimChar: '/');

        // Resolve ".." and "." segments
        string[] segments = normalized.Split(separator: '/', options: StringSplitOptions.RemoveEmptyEntries);
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
                stack.Push(item: segment);
            }
        }
        return "/" + string.Join(separator: "/", values: stack.Reverse());
    }

    public string? ResolveLinkTarget(string path)
    {
        string nfsPath = ToNfsPath(path: path);
        _lock.Wait();
        try
        {
            int rc = _libNfs.Lstat64(nfs: _nfs, path: nfsPath, stat: out LibNfs.NfsStat64 stat);
            if (rc != 0 || stat.FileType != LibNfs.S_IFLNK)
                return null;

            IntPtr buf = Marshal.AllocHGlobal(cb: 4096);
            try
            {
                int linkRc = _libNfs.Readlink(nfs: _nfs, path: nfsPath, buf: buf, bufSize: 4096);
                if (linkRc < 0)
                    return null;
                return Marshal.PtrToStringUTF8(ptr: buf);
            }
            finally
            {
                Marshal.FreeHGlobal(hglobal: buf);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public bool IsHidden(string path)
    {
        string name = Path.GetFileName(path: path.Replace(oldChar: '\\', newChar: '/'));
        return name.StartsWith(value: '.') && name.Length > 1;
    }

    public void MoveDirectory(string source, string destination)
    {
        // NFS RENAME works for directories too
        string srcPath = ToNfsPath(path: source);
        string dstPath = ToNfsPath(path: destination);
        _lock.Wait();
        try
        {
            int rc = RenameWithRetry(oldPath: srcPath, newPath: dstPath);
            if (rc != 0)
                throw new IOException(
                    message: $"NFS rename directory '{source}' -> '{destination}' failed: {_libNfs.GetError(nfs: _nfs)}"
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
        using CancellationTokenSource cts = new(millisecondsDelay: timeoutMs);

        try
        {
            return await Task.Run(function: () => GetExportsBlocking(server: server, log: log), cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            log.LogWarning(
                message: "NFS export discovery timed out after {Timeout}ms for {Server}", args: [timeoutMs, server]
            );
            return null;
        }
        catch (Exception ex)
        {
            // Best-effort discovery: any failure resolves to "unknown" (null),
            // never a thrown exception the callers aren't expecting.
            log.LogWarning(exception: ex, message: "NFS export discovery failed for {Server}", args: server);
            return null;
        }
    }

    private static List<string>? GetExportsBlocking(string server, ILogger log)
    {
        // Path 1 — NFSv3 mount protocol (port 111 portmap → mountd).
        // This is the "official" way but requires the server to register
        // mountd with rpcbind. TrueNAS / vanilla NFSv4-only servers often
        // skip this, returning an empty list.
        List<string>? v3 = TryV3MountGetExports(server: server, log: log);
        if (v3 is { Count: > 0 })
        {
            log.LogInformation(
                message: "NFS export discovery: v3 mount-protocol returned {Count} exports for {Server}", args: [v3.Count, server]
            );
            return v3;
        }
        log.LogInformation(
            message: "NFS export discovery: v3 mount-protocol returned no exports for {Server}, falling back to v4 root walk",
            args: server
        );

        // Path 2 — NFSv4 pseudo-fs walk. Mount the server's root as v4 and
        // list immediate sub-directories. On TrueNAS/Linux with NFSv4 only,
        // this surfaces real exports (e.g. /mnt/Vault/Media) via the
        // pseudo-filesystem the v4 server exposes from /.
        return TryV4RootListing(server: server, log: log);
    }

    private static List<string>? TryV3MountGetExports(string server, ILogger log)
    {
        IntPtr head;
        try
        {
            head = LibNfs.MountGetExports(server: server);
        }
        catch (Exception ex)
        {
            log.LogWarning(
                exception: ex,
                message: "NFS v3 mount-protocol export query threw for {Server} — falling back to v4 root walk",
                args: server
            );
            return null;
        }

        if (head == IntPtr.Zero)
            return null;

        List<string> exports = [];
        try
        {
            IntPtr current = head;
            while (current != IntPtr.Zero)
            {
                LibNfs.ExportEntry entry = Marshal.PtrToStructure<LibNfs.ExportEntry>(ptr: current);
                if (entry.ExDir != IntPtr.Zero)
                {
                    string? path = Marshal.PtrToStringUTF8(ptr: entry.ExDir);
                    if (!string.IsNullOrWhiteSpace(value: path))
                        exports.Add(item: path);
                }
                current = entry.ExNext;
            }
        }
        catch (Exception ex)
        {
            // A malformed or unexpected export list from the server must degrade
            // to the v4 root walk, never crash the whole discovery (or a
            // dashboard NFS browse that reaches this path).
            log.LogWarning(
                exception: ex,
                message: "NFS v3 export list parse failed for {Server} — falling back to v4 root walk",
                args: server
            );
            return null;
        }
        finally
        {
            LibNfs.MountFreeExportList(exports: head);
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
                message: "NFSv4 export discovery: nfs_init_context returned null for {Server}",
                args: server
            );
            return null;
        }

        try
        {
            if (LibNfs.SetVersion(nfs: ctx, version: 4) != 0)
            {
                log.LogWarning(
                    message: "NFSv4 export discovery: nfs_set_version(4) failed for {Server} — {Error}", args: [server, LibNfs.GetError(nfs: ctx)]
                );
                return null;
            }

            // Mount the v4 pseudo-root. libnfs accepts "/" as the export
            // path for v4 to land at the server's NFSv4 PUTROOTFH.
            if (LibNfs.Mount(nfs: ctx, server: server, exportPath: "/") != 0)
            {
                log.LogWarning(
                    message: "NFSv4 export discovery: nfs_mount({Server}, '/') failed — {Error}", args: [server, LibNfs.GetError(nfs: ctx)]
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
                CollectV4Children(ctx: ctx, path: probeRoot, maxDepth: 3, sink: roots);
                if (roots.Count > 0)
                {
                    log.LogInformation(
                        message: "NFSv4 export discovery: walked {Probe} on {Server}, found {Count} dirs", args: [probeRoot, server, roots.Count]
                    );
                    return roots;
                }
            }

            log.LogWarning(
                message: "NFSv4 export discovery: walked v4 root + {Count} fallback paths on {Server}, all empty — server may only expose explicit export paths (try entering manually)", args: [CommonV4Roots.Length - 1, server]
            );
            return null;
        }
        catch (Exception ex)
        {
            log.LogWarning(exception: ex, message: "NFSv4 export discovery threw for {Server}", args: server);
            return null;
        }
        finally
        {
            LibNfs.DestroyContext(nfs: ctx);
        }
    }

    private static void CollectV4Children(IntPtr ctx, string path, int maxDepth, List<string> sink)
    {
        if (maxDepth <= 0)
            return;
        if (LibNfs.OpenDir(nfs: ctx, path: path, dir: out IntPtr dir) != 0)
            return;

        try
        {
            while (true)
            {
                IntPtr entryPtr = LibNfs.ReadDir(nfs: ctx, dir: dir);
                if (entryPtr == IntPtr.Zero)
                    break;

                LibNfs.NfsDirent entry = Marshal.PtrToStructure<LibNfs.NfsDirent>(ptr: entryPtr);
                string? name = LibNfs.ReadDirentName(entry: entry);
                if (string.IsNullOrEmpty(value: name) || name == "." || name == "..")
                    continue;
                if (name.StartsWith(value: '.'))
                    continue;
                if (entry.Type != LibNfs.NF3DIR)
                    continue;

                string child = path == "/" ? "/" + name : path + "/" + name;
                sink.Add(item: child);

                // Descend one more level so /mnt/Vault/Media-style mount
                // points are captured even when only /mnt is listed at root.
                CollectV4Children(ctx: ctx, path: child, maxDepth: maxDepth - 1, sink: sink);
            }
        }
        finally
        {
            LibNfs.CloseDir(nfs: ctx, dir: dir);
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
        string nfsPath = string.IsNullOrWhiteSpace(value: relativePath) ? "/" : ToNfsPath(path: relativePath);

        List<(string, bool)> results = [];
        _lock.Wait();
        try
        {
            int openRc = OpenDirWithRetry(nfsPath: nfsPath, dir: out IntPtr dir);
            if (openRc != 0)
                return results;

            try
            {
                while (true)
                {
                    IntPtr entryPtr = _libNfs.ReadDir(nfs: _nfs, dir: dir);
                    if (entryPtr == IntPtr.Zero)
                        break;

                    LibNfs.NfsDirent entry = Marshal.PtrToStructure<LibNfs.NfsDirent>(ptr: entryPtr);
                    string? name = LibNfs.ReadDirentName(entry: entry);
                    if (
                        string.IsNullOrEmpty(value: name)
                        || name == "."
                        || name == ".."
                        || name.StartsWith(value: '.')
                    )
                        continue;

                    if (entry.Type == LibNfs.NF3DIR)
                        results.Add(item: (name, true));
                }
            }
            finally
            {
                _libNfs.CloseDir(nfs: _nfs, dir: dir);
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

        // Stop the keep-alive timer first so it can't fire mid-teardown and
        // call into a context we're about to free.
        _keepAlive?.Dispose();

        // Acquire the driver lock before tearing down the libnfs context so
        // any stream call already inside LibNfs.Read/Write finishes against
        // a valid context. Without this, Dispose can run while a stream's
        // native call is in progress, freeing the context out from under
        // it — same 0xC0000005 shape as the un-locked stream race.
        _lock.Wait();
        try
        {
            _disposed = true;
            _libNfs.Umount(nfs: _nfs);
            _libNfs.DestroyContext(nfs: _nfs);
        }
        finally
        {
            _lock.Release();
        }

        _lock.Dispose();
    }

    // -----------------------------------------------------------------------
    // Internals
    // -----------------------------------------------------------------------

    private string ToNfsPath(string path)
    {
        string normalized = path.Replace(oldChar: '\\', newChar: '/');

        // Path Contract Rule 2: collapse consecutive separators. Without
        // this, "foo//bar" reaches libnfs verbatim and fails to match the
        // canonical "foo/bar" entries in the directory listing.
        while (normalized.Contains(value: "//"))
            normalized = normalized.Replace(oldValue: "//", newValue: "/");

        if (!normalized.StartsWith(value: '/'))
            normalized = "/" + normalized;

        // libnfs paths are relative to the mounted Export. Strip a matching
        // Export prefix so callers can pass absolute server paths.
        string export = _config.Export.TrimEnd(trimChar: '/');
        if (export.Length > 0 && normalized.StartsWith(value: export, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[export.Length..];
            if (normalized.Length == 0)
                normalized = "/";
            else if (!normalized.StartsWith(value: '/'))
                normalized = "/" + normalized;
        }

        // Prepend the folder sub-path so per-request relative paths land
        // under the correct directory within the mounted export. The mount
        // is always at the export root; SubPath scopes this driver instance
        // to a specific subdirectory of that export.
        string subPath = _config.SubPath.Trim(trimChar: '/');
        if (!string.IsNullOrEmpty(value: subPath))
        {
            string subPathPrefix = "/" + subPath;
            if (
                !normalized.StartsWith(value: subPathPrefix + "/", comparisonType: StringComparison.OrdinalIgnoreCase)
                && !string.Equals(a: normalized, b: subPathPrefix, comparisonType: StringComparison.OrdinalIgnoreCase)
            )
            {
                normalized = subPathPrefix + normalized;
            }
        }

        // Collapse any double-slashes introduced by the prepend step.
        while (normalized.Contains(value: "//"))
            normalized = normalized.Replace(oldValue: "//", newValue: "/");

        return normalized;
    }

    private bool FileExistsNoLock(string nfsPath)
    {
        int rc = Stat64WithRetry(nfsPath: nfsPath, stat: out LibNfs.NfsStat64 stat);
        return rc == 0 && stat.FileType == LibNfs.S_IFREG;
    }

    private void EnsureDirectoryRecursive(string nfsPath)
    {
        if (nfsPath == "/" || nfsPath == string.Empty)
            return;

        int checkRc = Stat64WithRetry(nfsPath: nfsPath, stat: out LibNfs.NfsStat64 existing);
        if (checkRc == 0 && existing.FileType == LibNfs.S_IFDIR)
            return;

        string parent = Path.GetDirectoryName(path: nfsPath)?.Replace(oldChar: '\\', newChar: '/') ?? "/";
        if (parent != nfsPath)
            EnsureDirectoryRecursive(nfsPath: parent);

        int mkRc = MkDirWithRetry(nfsPath: nfsPath);
        if (mkRc != 0)
        {
            string mkErr = _libNfs.GetError(nfs: _nfs);
            // NFSv3 reports EEXIST; NFSv4 reports NFS4ERR_EXIST — treat both as success.
            bool alreadyExists =
                mkErr.Contains(value: "EEXIST", comparisonType: StringComparison.OrdinalIgnoreCase)
                || mkErr.Contains(value: "EXIST", comparisonType: StringComparison.OrdinalIgnoreCase);
            if (!alreadyExists)
                throw new IOException(message: $"NFS mkdir failed for '{nfsPath}': {mkErr}");
        }
    }

    private void DeleteDirectoryRecursive(string nfsPath)
    {
        int openRc = OpenDirWithRetry(nfsPath: nfsPath, dir: out IntPtr dir);
        if (openRc != 0)
        {
            if (_libNfs.GetError(nfs: _nfs).Contains(value: "ENOENT", comparisonType: StringComparison.OrdinalIgnoreCase))
                return;
            throw new IOException(message: $"NFS opendir failed for '{nfsPath}': {_libNfs.GetError(nfs: _nfs)}");
        }

        try
        {
            while (true)
            {
                IntPtr entryPtr = _libNfs.ReadDir(nfs: _nfs, dir: dir);
                if (entryPtr == IntPtr.Zero)
                    break;

                LibNfs.NfsDirent entry = Marshal.PtrToStructure<LibNfs.NfsDirent>(ptr: entryPtr);
                string? name = LibNfs.ReadDirentName(entry: entry);
                if (string.IsNullOrEmpty(value: name) || name == "." || name == "..")
                    continue;

                string childPath = nfsPath.TrimEnd(trimChar: '/') + "/" + name;

                if (entry.Type == LibNfs.NF3DIR)
                    DeleteDirectoryRecursive(nfsPath: childPath);
                else
                {
                    int unlinkRc = UnlinkWithRetry(nfsPath: childPath);
                    if (unlinkRc != 0)
                        throw new IOException(
                            message: $"NFS unlink failed for '{childPath}': {_libNfs.GetError(nfs: _nfs)}"
                        );
                }
            }
        }
        finally
        {
            _libNfs.CloseDir(nfs: _nfs, dir: dir);
        }

        int rmdirRc = RmDirWithRetry(nfsPath: nfsPath);
        if (
            rmdirRc != 0
            && !_libNfs.GetError(nfs: _nfs).Contains(value: "ENOENT", comparisonType: StringComparison.OrdinalIgnoreCase)
        )
            throw new IOException(message: $"NFS rmdir failed for '{nfsPath}': {_libNfs.GetError(nfs: _nfs)}");
    }

    private void CollectEntries(
        string nfsDir,
        string virtualDir,
        string searchPattern,
        SearchOption option,
        List<string> results
    )
    {
        int openRc = OpenDirWithRetry(nfsPath: nfsDir, dir: out IntPtr dir);
        if (openRc != 0)
        {
            // -20 (NFS4ERR_NOTDIR / ENOTDIR) just means the caller (or the
            // recursion below) probed a path that turned out to be a file —
            // not a real failure, and noisy at Warning level.
            if (openRc != -20)
                _log.LogWarning(
                    message: "NFS opendir failed for '{Path}' on {Server}:{Export} (v{Version}, rc={Rc}): {Error}", args: [nfsDir, _config.Server, _config.Export, _config.Version, openRc, _libNfs.GetError(nfs: _nfs)]
                );
            return;
        }

        try
        {
            while (true)
            {
                IntPtr entryPtr = _libNfs.ReadDir(nfs: _nfs, dir: dir);
                if (entryPtr == IntPtr.Zero)
                    break;

                LibNfs.NfsDirent entry = Marshal.PtrToStructure<LibNfs.NfsDirent>(ptr: entryPtr);
                string? name = LibNfs.ReadDirentName(entry: entry);
                if (string.IsNullOrEmpty(value: name) || name == "." || name == "..")
                    continue;

                // Contract: virtualPath is driver-relative (same shape FileExists/DirectoryExists accept).
                string virtualPath = virtualDir.TrimEnd(trimChar: '/') + "/" + name;
                string childNfsPath = nfsDir.TrimEnd(trimChar: '/') + "/" + name;

                if (StoragePatternMatcher.Matches(name: name, pattern: searchPattern))
                    results.Add(item: virtualPath);

                if (option == SearchOption.AllDirectories)
                {
                    // entry.Type from libnfs's nfsdirent has been unreliable
                    // for NFSv4 in practice — verify with Stat64 before
                    // recursing so we never opendir a regular file.
                    int statRc = Stat64WithRetry(nfsPath: childNfsPath, stat: out LibNfs.NfsStat64 childStat);
                    if (statRc == 0 && childStat.FileType == LibNfs.S_IFDIR)
                        CollectEntries(nfsDir: childNfsPath, virtualDir: virtualPath, searchPattern: searchPattern, option: option, results: results);
                }
            }
        }
        finally
        {
            _libNfs.CloseDir(nfs: _nfs, dir: dir);
        }
    }
}
