using System.Runtime.InteropServices;
using NoMercy.Storage.Drivers.Nfs.Interop;

namespace NoMercy.Storage.Drivers.Nfs;

/// <summary>
/// Write-only <see cref="Stream"/> over a libnfs file handle.
/// Buffers in 32 KB chunks and flushes via <c>nfs_write</c>.
/// The handle is closed (implicitly synced) on <see cref="Dispose"/>.
/// Every native call is gated on the driver's lock — the libnfs context is
/// shared across streams and is not re-entrant.
/// </summary>
internal sealed class NfsWriteStream : Stream
{
    private const int ChunkSize = 32 * 1024;

    private readonly IntPtr _nfs;
    private readonly IntPtr _fh;
    private readonly SemaphoreSlim _lock;
    private bool _disposed;

    internal NfsWriteStream(IntPtr nfs, IntPtr fh, SemaphoreSlim driverLock)
    {
        _nfs = nfs;
        _fh = fh;
        _lock = driverLock;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int written = 0;
        while (written < count)
        {
            int chunk = Math.Min(ChunkSize, count - written);
            IntPtr pinned = Marshal.AllocHGlobal(chunk);
            try
            {
                Marshal.Copy(buffer, offset + written, pinned, chunk);

                int n;
                string? err = null;
                try
                {
                    _lock.Wait();
                    try
                    {
                        n = LibNfs.Write(_nfs, _fh, pinned, chunk);
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
                    throw new IOException("NFS driver disposed during write");
                }

                if (n < 0)
                    throw new IOException($"NFS write failed: {err}");
                written += n;
            }
            finally
            {
                Marshal.FreeHGlobal(pinned);
            }
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        byte[] tmp = buffer.ToArray();
        Write(tmp, 0, tmp.Length);
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

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
