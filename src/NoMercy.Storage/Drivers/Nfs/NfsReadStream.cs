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
using NoMercy.Storage.Drivers.Nfs.Interop;

namespace NoMercy.Storage.Drivers.Nfs;

/// <summary>
/// Read-only <see cref="Stream"/> over a libnfs file handle.
/// Issues chunked <c>nfs_read</c> calls (1 MiB per chunk).
/// Every native call is gated on the driver's lock — the libnfs context is
/// shared across streams and is not re-entrant; concurrent reads without
/// the lock cause access violations inside libnfs.
/// </summary>
internal sealed class NfsReadStream : Stream
{
    // One request per chunk. libnfs clamps a larger request to the mount's
    // negotiated rsize internally and the loop below handles the short read,
    // so this only trades a smaller managed↔native round-trip (lock + heap
    // pin) count for a larger one — at 1 MiB a 5 GB file drops from ~163k
    // native read cycles to ~5k. The default is set from the throughput sweep;
    // the ctor accepts an override so the sweep can measure the curve.
    private const int DefaultChunkSize = 1024 * 1024;

    private readonly int _chunkSize;
    private readonly IntPtr _nfs;
    private readonly IntPtr _fh;
    private readonly SemaphoreSlim _lock;
    private readonly long _length;
    private readonly ILibNfs _libNfs;
    private long _position;
    private bool _disposed;

    internal NfsReadStream(IntPtr nfs, IntPtr fh, long length, SemaphoreSlim driverLock)
        : this(nfs: nfs, fh: fh, length: length, driverLock: driverLock, libNfs: LibNfsPInvoke.Instance) { }

    internal NfsReadStream(
        IntPtr nfs,
        IntPtr fh,
        long length,
        SemaphoreSlim driverLock,
        ILibNfs libNfs,
        int chunkSize = DefaultChunkSize
    )
    {
        _nfs = nfs;
        _fh = fh;
        _length = length;
        _lock = driverLock;
        _libNfs = libNfs;
        _chunkSize = chunkSize;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => Seek(offset: value, origin: SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(condition: _disposed, instance: this);
        if (count <= 0)
            return 0;

        int toRead = (int)Math.Min(val1: count, val2: _length - _position);
        if (toRead <= 0)
            return 0;

        int totalRead = 0;

        while (totalRead < toRead)
        {
            int chunk = Math.Min(val1: _chunkSize, val2: toRead - totalRead);
            IntPtr pinned = Marshal.AllocHGlobal(cb: chunk);
            try
            {
                int n;
                string? err = null;
                try
                {
                    _lock.Wait();
                    try
                    {
                        n = _libNfs.Read(nfs: _nfs, fh: _fh, buf: pinned, count: chunk);
                        if (n < 0)
                            err = _libNfs.GetError(nfs: _nfs);
                    }
                    finally
                    {
                        _lock.Release();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Driver was disposed while we were waiting on the lock.
                    // Surface as IOException so the HTTP pipeline can finish
                    // the response cleanly instead of crashing the host.
                    throw new IOException(message: "NFS driver disposed during read");
                }

                if (n < 0)
                    throw new IOException(message: $"NFS read failed: {err}");
                if (n == 0)
                    break;

                Marshal.Copy(source: pinned, destination: buffer, startIndex: offset + totalRead, length: n);
                totalRead += n;
                _position += n;
            }
            finally
            {
                Marshal.FreeHGlobal(hglobal: pinned);
            }
        }

        return totalRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(condition: _disposed, instance: this);
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(origin)),
        };

        long rc;
        string? err = null;
        try
        {
            _lock.Wait();
            try
            {
                rc = _libNfs.Lseek(
                    nfs: _nfs,
                    fh: _fh,
                    offset: target,
                    whence: 0 /* SEEK_SET */
                    ,
                    currentOffset: out _
                );
                if (rc < 0)
                    err = _libNfs.GetError(nfs: _nfs);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            throw new IOException(message: "NFS driver disposed during seek");
        }

        if (rc < 0)
            throw new IOException(message: $"NFS lseek failed: {err}");

        _position = target;
        return _position;
    }

    public override void Flush() { }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            _lock.Wait();
            try
            {
                _libNfs.Close(nfs: _nfs, fh: _fh);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            // Driver was disposed first — its own Dispose will tear down the
            // context, so the fh is already gone. Nothing to do.
        }

        base.Dispose(disposing: disposing);
    }
}
