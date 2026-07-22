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
/// Read-only stream backed by a dedicated libnfs context that is owned
/// exclusively by this stream. On Dispose the file handle is closed,
/// the context unmounted, and the context destroyed — no shared state
/// is touched, so concurrent AcquireLocalPath calls cannot corrupt each
/// other's NFSv4 open-seqid sequence.
/// </summary>
internal sealed class IsolatedNfsReadStream : Stream
{
    private const int ChunkSize = 32 * 1024;

    private readonly ILibNfs _libNfs;
    private readonly IntPtr _ownedCtx;
    private readonly IntPtr _fh;
    private readonly long _length;
    private long _position;
    private bool _disposed;

    internal IsolatedNfsReadStream(ILibNfs libNfs, IntPtr ownedCtx, IntPtr fh, long length)
    {
        _libNfs = libNfs;
        _ownedCtx = ownedCtx;
        _fh = fh;
        _length = length;
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
            int chunk = Math.Min(val1: ChunkSize, val2: toRead - totalRead);
            IntPtr pinned = Marshal.AllocHGlobal(cb: chunk);
            try
            {
                int n = _libNfs.Read(nfs: _ownedCtx, fh: _fh, buf: pinned, count: chunk);
                if (n < 0)
                    throw new IOException(
                        message: $"NFS isolated read failed: {_libNfs.GetError(nfs: _ownedCtx)}"
                    );
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

        long rc = _libNfs.Lseek(
            nfs: _ownedCtx,
            fh: _fh,
            offset: target,
            whence: 0 /* SEEK_SET */
            ,
            currentOffset: out _
        );
        if (rc < 0)
            throw new IOException(message: $"NFS isolated lseek failed: {_libNfs.GetError(nfs: _ownedCtx)}");

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

        _libNfs.Close(nfs: _ownedCtx, fh: _fh);
        _libNfs.Umount(nfs: _ownedCtx);
        _libNfs.DestroyContext(nfs: _ownedCtx);

        base.Dispose(disposing: disposing);
    }
}
