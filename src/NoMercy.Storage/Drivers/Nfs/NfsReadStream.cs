using System.Runtime.InteropServices;
using NoMercy.Storage.Drivers.Nfs.Interop;

namespace NoMercy.Storage.Drivers.Nfs;

/// <summary>
/// Read-only <see cref="Stream"/> over a libnfs file handle.
/// Issues chunked <c>nfs_read</c> calls (32 KB per chunk).
/// Every native call is gated on the driver's lock — the libnfs context is
/// shared across streams and is not re-entrant; concurrent reads without
/// the lock cause access violations inside libnfs.
/// </summary>
internal sealed class NfsReadStream : Stream
{
    private const int ChunkSize = 32 * 1024;

    private readonly IntPtr _nfs;
    private readonly IntPtr _fh;
    private readonly SemaphoreSlim _lock;
    private readonly long _length;
    private long _position;
    private bool _disposed;

    internal NfsReadStream(IntPtr nfs, IntPtr fh, long length, SemaphoreSlim driverLock)
    {
        _nfs = nfs;
        _fh = fh;
        _length = length;
        _lock = driverLock;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (count <= 0)
            return 0;

        int toRead = (int)Math.Min(count, _length - _position);
        if (toRead <= 0)
            return 0;

        int totalRead = 0;

        while (totalRead < toRead)
        {
            int chunk = Math.Min(ChunkSize, toRead - totalRead);
            IntPtr pinned = Marshal.AllocHGlobal(chunk);
            try
            {
                int n;
                string? err = null;
                try
                {
                    _lock.Wait();
                    try
                    {
                        n = LibNfs.Read(_nfs, _fh, pinned, chunk);
                        if (n < 0)
                            err = LibNfs.GetError(_nfs);
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
                    throw new IOException("NFS driver disposed during read");
                }

                if (n < 0)
                    throw new IOException($"NFS read failed: {err}");
                if (n == 0)
                    break;

                Marshal.Copy(pinned, buffer, offset + totalRead, n);
                totalRead += n;
                _position += n;
            }
            finally
            {
                Marshal.FreeHGlobal(pinned);
            }
        }

        return totalRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        long rc;
        string? err = null;
        try
        {
            _lock.Wait();
            try
            {
                rc = LibNfs.Lseek(
                    _nfs,
                    _fh,
                    target,
                    0 /* SEEK_SET */
                    ,
                    out _
                );
                if (rc < 0)
                    err = LibNfs.GetError(_nfs);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            throw new IOException("NFS driver disposed during seek");
        }

        if (rc < 0)
            throw new IOException($"NFS lseek failed: {err}");

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
                LibNfs.Close(_nfs, _fh);
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

        base.Dispose(disposing);
    }
}
