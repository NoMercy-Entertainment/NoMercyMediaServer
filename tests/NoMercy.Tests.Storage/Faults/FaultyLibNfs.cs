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
using System.Runtime.InteropServices;
using System.Text;
using NoMercy.Storage.Drivers.Nfs.Interop;

namespace NoMercy.Tests.Storage.Faults;

/// <summary>
/// In-memory <see cref="ILibNfs"/> for fault-injection tests. Backs a tiny
/// path → bytes map so OpenRead/OpenWrite/Stat behave like a real NFS export,
/// and exposes a per-method script ("call N returns rc=X with error=Y") so
/// tests can deterministically reproduce NFS4ERR_EXPIRED / BAD_SEQID etc.
/// at any specific call site.
///
/// Calls are tracked in <see cref="CallCounts"/> so tests can assert the
/// driver retried (or didn't) the expected number of times.
/// </summary>
internal sealed class FaultyLibNfs : ILibNfs
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();
    private readonly ConcurrentDictionary<string, bool> _dirs = new();
    private readonly ConcurrentDictionary<string, DateTime> _mtimes = new();
    private readonly ConcurrentDictionary<IntPtr, FileHandle> _openHandles = new();
    private readonly ConcurrentDictionary<IntPtr, DirIter> _openDirs = new();
    private long _nextHandle = 1;
    private long _nextContext = 1;

    private sealed class DirIter
    {
        public required List<string> Entries { get; init; }
        public required List<bool> EntryIsDir { get; init; }
        public int Cursor;
        public IntPtr ScratchEntry; // unmanaged NfsDirent struct
        public IntPtr ScratchName; // unmanaged UTF-8 bytes
    }

    public string CurrentError { get; private set; } = string.Empty;

    /// <summary>
    /// Per-method call counts — assert against these in tests
    /// (e.g. "Open was called twice" = original + post-remount retry).
    /// </summary>
    public Dictionary<string, int> CallCounts { get; } = new();

    /// <summary>
    /// When &gt; 0, every libnfs call sleeps this long inside the protected
    /// region. Forces concurrency races to surface in stress tests so the
    /// driver's lock guarantees are observable.
    /// </summary>
    public TimeSpan ArtificialLatency { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Largest number of libnfs calls observed running simultaneously — proves
    /// the driver serializes access to the shared context. The driver-side
    /// SemaphoreSlim should pin this to 1; anything higher is a contract bug.
    /// </summary>
    public int MaxConcurrentCalls => _maxConcurrent;

    private int _currentConcurrent;
    private int _maxConcurrent;

    /// <summary>
    /// Scripted responses keyed by "Method:callIndex" (0-based).
    /// E.g. <c>Faults["Open:0"] = (-11, "NFS4ERR_EXPIRED")</c> makes the FIRST
    /// Open call return -11 with that error. Other calls hit the real backend.
    /// </summary>
    public Dictionary<string, (int rc, string error)> Faults { get; } = new();

    public IReadOnlyDictionary<string, byte[]> Files => _files;

    public void Seed(string path, byte[] content)
    {
        string key = Normalise(path: path);
        _files[key: key] = content;
        _mtimes[key: key] = DateTime.UtcNow;
        EnsureParentDirs(path: path);
    }

    public void SeedDir(string path)
    {
        string key = Normalise(path: path);
        _dirs[key: key] = true;
        _mtimes[key: key] = DateTime.UtcNow;
    }

    private static string Normalise(string path)
    {
        string normalised = path.Replace(oldChar: '\\', newChar: '/');
        if (!normalised.StartsWith(value: '/'))
            normalised = "/" + normalised;
        return normalised.TrimEnd(trimChar: '/');
    }

    private void EnsureParentDirs(string path) => EnsureParentDirsKey(normalised: Normalise(path: path));

    private void EnsureParentDirsKey(string normalised)
    {
        int idx = normalised.LastIndexOf(value: '/');
        while (idx > 0)
        {
            _dirs[key: normalised[..idx]] = true;
            idx = normalised[..idx].LastIndexOf(value: '/');
        }
        _dirs[key: "/"] = true;
    }

    private bool TryFault(string method, out int rc, out string err)
    {
        // Track concurrent execution so stress tests can prove the driver's
        // lock holds the libnfs context single-threaded.
        int current = Interlocked.Increment(location: ref _currentConcurrent);
        int max = _maxConcurrent;
        while (current > max)
            max = Interlocked.CompareExchange(location1: ref _maxConcurrent, value: current, comparand: max);

        try
        {
            int index;
            lock (CallCounts)
            {
                index = CallCounts.GetValueOrDefault(key: method, defaultValue: 0);
                CallCounts[key: method] = index + 1;
            }

            if (ArtificialLatency > TimeSpan.Zero)
                Thread.Sleep(timeout: ArtificialLatency);

            (int rc, string error) fault;
            bool faultPresent;
            lock (Faults)
            {
                faultPresent = Faults.TryGetValue(key: $"{method}:{index}", value: out fault);
            }
            if (faultPresent)
            {
                rc = fault.rc;
                err = fault.error;
                CurrentError = err;
                return true;
            }
            rc = 0;
            err = string.Empty;
            return false;
        }
        finally
        {
            Interlocked.Decrement(location: ref _currentConcurrent);
        }
    }

    private sealed class FileHandle
    {
        public string Path { get; init; } = string.Empty;
        public int Mode { get; init; }
        public long Position { get; set; }
    }

    // -----------------------------------------------------------------------
    // ILibNfs
    // -----------------------------------------------------------------------

    public IntPtr InitContext()
    {
        if (TryFault(method: nameof(InitContext), rc: out _, err: out _))
            return IntPtr.Zero;
        return new(value: Interlocked.Increment(location: ref _nextContext));
    }

    public void DestroyContext(IntPtr nfs)
    {
        TryFault(method: nameof(DestroyContext), rc: out _, err: out _);
    }

    public int Mount(IntPtr nfs, string server, string exportPath)
    {
        if (TryFault(method: nameof(Mount), rc: out int rc, err: out _))
            return rc;
        return 0;
    }

    public int Umount(IntPtr nfs)
    {
        TryFault(method: nameof(Umount), rc: out _, err: out _);
        return 0;
    }

    public void SetUid(IntPtr nfs, int uid) => TryFault(method: nameof(SetUid), rc: out _, err: out _);

    public void SetGid(IntPtr nfs, int gid) => TryFault(method: nameof(SetGid), rc: out _, err: out _);

    public int SetVersion(IntPtr nfs, int version)
    {
        if (TryFault(method: nameof(SetVersion), rc: out int rc, err: out _))
            return rc;
        return 0;
    }

    /// <summary>
    /// NFSv4 client name applied to each context via <c>nfs_set_client_name</c>,
    /// keyed by context pointer. Isolated read contexts must each receive a
    /// UNIQUE name so their open-owner seqid sequences stay independent on the
    /// server (a shared name collides as NFS4ERR_BAD_SEQID under a parallel scan).
    /// </summary>
    public ConcurrentDictionary<IntPtr, string> ClientNames { get; } = new();

    public void SetClientName(IntPtr nfs, string id) => ClientNames[key: nfs] = id;

    public void SetVerifier(IntPtr nfs, string verifier) { }

    public string GetError(IntPtr nfs) => CurrentError;

    public int Stat64(IntPtr nfs, string path, out LibNfs.NfsStat64 stat)
    {
        if (TryFault(method: nameof(Stat64), rc: out int rc, err: out _))
        {
            stat = default;
            return rc;
        }

        string key = Normalise(path: path);
        stat = default;

        // Recorded write time so LastModifiedAsync returns a meaningful
        // recent timestamp instead of the Unix epoch.
        DateTime mtime = _mtimes.TryGetValue(key: key, value: out DateTime t) ? t : DateTime.UtcNow;
        ulong mtimeSec = (ulong)new DateTimeOffset(dateTime: mtime, offset: TimeSpan.Zero).ToUnixTimeSeconds();

        if (_files.TryGetValue(key: key, value: out byte[]? content))
        {
            stat = new()
            {
                Size = (ulong)content.Length,
                Mode = 0x8000, /* S_IFREG << 12 */
                MtimeSec = mtimeSec,
                CtimeSec = mtimeSec,
                AtimeSec = mtimeSec,
            };
            return 0;
        }
        if (_dirs.ContainsKey(key: key))
        {
            stat = new()
            {
                Mode = 0x4000, /* S_IFDIR << 12 */
                MtimeSec = mtimeSec,
                CtimeSec = mtimeSec,
                AtimeSec = mtimeSec,
            };
            return 0;
        }

        CurrentError = "NFS4ERR_NOENT";
        return -2;
    }

    public int Lstat64(IntPtr nfs, string path, out LibNfs.NfsStat64 stat) =>
        Stat64(nfs: nfs, path: path, stat: out stat);

    public int OpenDir(IntPtr nfs, string path, out IntPtr dir)
    {
        if (TryFault(method: nameof(OpenDir), rc: out int rc, err: out _))
        {
            dir = IntPtr.Zero;
            return rc;
        }

        string parent = Normalise(path: path);
        string parentPrefix = parent == "/" ? "/" : parent + "/";

        // Collect immediate children (not recursive).
        List<string> entries = [];
        List<bool> entryIsDir = [];

        foreach (string fileKey in _files.Keys)
        {
            if (!fileKey.StartsWith(value: parentPrefix, comparisonType: StringComparison.Ordinal))
                continue;
            string remainder = fileKey.Substring(startIndex: parentPrefix.Length);
            if (remainder.Length == 0 || remainder.Contains(value: '/'))
                continue;
            entries.Add(item: remainder);
            entryIsDir.Add(item: false);
        }

        foreach (string dirKey in _dirs.Keys)
        {
            if (dirKey == parent)
                continue;
            if (!dirKey.StartsWith(value: parentPrefix, comparisonType: StringComparison.Ordinal))
                continue;
            string remainder = dirKey.Substring(startIndex: parentPrefix.Length);
            if (remainder.Length == 0 || remainder.Contains(value: '/'))
                continue;
            entries.Add(item: remainder);
            entryIsDir.Add(item: true);
        }

        IntPtr handle = new(value: Interlocked.Increment(location: ref _nextHandle));
        _openDirs[key: handle] = new() { Entries = entries, EntryIsDir = entryIsDir };
        dir = handle;
        return 0;
    }

    public IntPtr ReadDir(IntPtr nfs, IntPtr dir)
    {
        TryFault(method: nameof(ReadDir), rc: out _, err: out _);

        if (!_openDirs.TryGetValue(key: dir, value: out DirIter? iter))
            return IntPtr.Zero;

        if (iter.Cursor >= iter.Entries.Count)
            return IntPtr.Zero;

        string name = iter.Entries[index: iter.Cursor];
        bool isDir = iter.EntryIsDir[index: iter.Cursor];
        iter.Cursor++;

        // Free any previous scratch allocations before allocating fresh ones
        // for this entry — the driver only reads the most recently returned
        // pointer before calling ReadDir again, so we can reuse the slot.
        FreeScratch(iter: iter);

        // Allocate UTF-8 name buffer in unmanaged memory.
        byte[] nameBytes = Encoding.UTF8.GetBytes(s: name + "\0");
        IntPtr namePtr = Marshal.AllocHGlobal(cb: nameBytes.Length);
        Marshal.Copy(source: nameBytes, startIndex: 0, destination: namePtr, length: nameBytes.Length);
        iter.ScratchName = namePtr;

        // Allocate the NfsDirent struct itself.
        LibNfs.NfsDirent entry = new()
        {
            Next = IntPtr.Zero,
            Name = namePtr,
            Inode = (ulong)iter.Cursor,
            // libnfs ftype3: NF3REG=1, NF3DIR=2.
            Type = isDir ? 2u : 1u,
        };
        IntPtr entryPtr = Marshal.AllocHGlobal(cb: Marshal.SizeOf<LibNfs.NfsDirent>());
        Marshal.StructureToPtr(structure: entry, ptr: entryPtr, fDeleteOld: false);
        iter.ScratchEntry = entryPtr;

        return entryPtr;
    }

    public void CloseDir(IntPtr nfs, IntPtr dir)
    {
        TryFault(method: nameof(CloseDir), rc: out _, err: out _);
        if (_openDirs.TryRemove(key: dir, value: out DirIter? iter))
            FreeScratch(iter: iter);
    }

    private static void FreeScratch(DirIter iter)
    {
        if (iter.ScratchEntry != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(hglobal: iter.ScratchEntry);
            iter.ScratchEntry = IntPtr.Zero;
        }
        if (iter.ScratchName != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(hglobal: iter.ScratchName);
            iter.ScratchName = IntPtr.Zero;
        }
    }

    public int MkDir(IntPtr nfs, string path)
    {
        if (TryFault(method: nameof(MkDir), rc: out int rc, err: out _))
            return rc;
        _dirs[key: Normalise(path: path)] = true;
        return 0;
    }

    public int RmDir(IntPtr nfs, string path)
    {
        if (TryFault(method: nameof(RmDir), rc: out int rc, err: out _))
            return rc;
        _dirs.TryRemove(key: Normalise(path: path), value: out _);
        return 0;
    }

    public int Open(IntPtr nfs, string path, int flags, out IntPtr fh)
    {
        if (TryFault(method: nameof(Open), rc: out int rc, err: out _))
        {
            fh = IntPtr.Zero;
            return rc;
        }

        string key = Normalise(path: path);
        bool isCreate =
            (
                flags & 0x40 /* O_CREAT */
            ) != 0;
        bool isTrunc =
            (
                flags & 0x200 /* O_TRUNC */
            ) != 0;
        bool isExcl =
            (
                flags & 0x80 /* O_EXCL */
            ) != 0;

        if (isCreate)
        {
            if (isExcl && _files.ContainsKey(key: key))
            {
                fh = IntPtr.Zero;
                CurrentError = "EEXIST";
                return -17;
            }
            if (isTrunc || !_files.ContainsKey(key: key))
            {
                _files[key: key] = [];
                _mtimes[key: key] = DateTime.UtcNow;
                EnsureParentDirsKey(normalised: key);
            }
        }
        else if (!_files.ContainsKey(key: key))
        {
            fh = IntPtr.Zero;
            CurrentError = "NFS4ERR_NOENT";
            return -2;
        }

        FileHandle handle = new() { Path = key, Mode = flags };
        IntPtr ptr = new(value: Interlocked.Increment(location: ref _nextHandle));
        _openHandles[key: ptr] = handle;
        fh = ptr;
        return 0;
    }

    public int Creat(IntPtr nfs, string path, int mode, out IntPtr fh)
    {
        if (TryFault(method: nameof(Creat), rc: out int rc, err: out _))
        {
            fh = IntPtr.Zero;
            return rc;
        }
        string key = Normalise(path: path);
        _files[key: key] = [];
        _mtimes[key: key] = DateTime.UtcNow;
        EnsureParentDirsKey(normalised: key);
        FileHandle handle = new() { Path = key };
        IntPtr ptr = new(value: Interlocked.Increment(location: ref _nextHandle));
        _openHandles[key: ptr] = handle;
        fh = ptr;
        return 0;
    }

    public int Close(IntPtr nfs, IntPtr fh)
    {
        TryFault(method: nameof(Close), rc: out _, err: out _);
        _openHandles.TryRemove(key: fh, value: out _);
        return 0;
    }

    public int Read(IntPtr nfs, IntPtr fh, IntPtr buf, int count)
    {
        if (TryFault(method: nameof(Read), rc: out int rc, err: out _))
            return rc;

        if (!_openHandles.TryGetValue(key: fh, value: out FileHandle? handle))
            return -9;
        if (!_files.TryGetValue(key: handle.Path, value: out byte[]? content))
            return -2;

        long remaining = content.Length - handle.Position;
        int toRead = (int)Math.Min(val1: count, val2: remaining);
        if (toRead <= 0)
            return 0;
        Marshal.Copy(source: content, startIndex: (int)handle.Position, destination: buf, length: toRead);
        handle.Position += toRead;
        return toRead;
    }

    public int Write(IntPtr nfs, IntPtr fh, IntPtr buf, int count)
    {
        if (TryFault(method: nameof(Write), rc: out int rc, err: out _))
            return rc;

        if (!_openHandles.TryGetValue(key: fh, value: out FileHandle? handle))
            return -9;
        if (!_files.TryGetValue(key: handle.Path, value: out byte[]? content))
            return -2;

        byte[] writeBuf = new byte[count];
        Marshal.Copy(source: buf, destination: writeBuf, startIndex: 0, length: count);

        long newSize = handle.Position + count;
        if (newSize > content.Length)
            Array.Resize(array: ref content, newSize: (int)newSize);
        Array.Copy(sourceArray: writeBuf, sourceIndex: 0, destinationArray: content, destinationIndex: handle.Position, length: count);
        _files[key: handle.Path] = content;
        _mtimes[key: handle.Path] = DateTime.UtcNow;
        handle.Position += count;
        return count;
    }

    public long Lseek(IntPtr nfs, IntPtr fh, long offset, int whence, out ulong currentOffset)
    {
        currentOffset = 0;
        if (TryFault(method: nameof(Lseek), rc: out int rc, err: out _))
            return rc;
        if (!_openHandles.TryGetValue(key: fh, value: out FileHandle? handle))
            return -9;
        handle.Position = offset;
        currentOffset = (ulong)offset;
        return offset;
    }

    public int Unlink(IntPtr nfs, string path)
    {
        if (TryFault(method: nameof(Unlink), rc: out int rc, err: out _))
            return rc;
        _files.TryRemove(key: Normalise(path: path), value: out _);
        return 0;
    }

    public int Rename(IntPtr nfs, string oldPath, string newPath)
    {
        if (TryFault(method: nameof(Rename), rc: out int rc, err: out _))
            return rc;
        string from = Normalise(path: oldPath);
        string to = Normalise(path: newPath);
        if (_files.TryRemove(key: from, value: out byte[]? content))
        {
            _files[key: to] = content;
            return 0;
        }
        if (_dirs.TryRemove(key: from, value: out _))
        {
            _dirs[key: to] = true;
            return 0;
        }
        CurrentError = "NFS4ERR_NOENT";
        return -2;
    }

    public int Readlink(IntPtr nfs, string path, IntPtr buf, int bufSize)
    {
        if (TryFault(method: nameof(Readlink), rc: out int rc, err: out _))
            return rc;
        return -1;
    }

    public IntPtr MountGetExports(string server) => IntPtr.Zero;

    public void MountFreeExportList(IntPtr exports) { }
}
