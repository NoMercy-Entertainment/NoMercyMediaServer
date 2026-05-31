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

    private readonly IntPtr _ownedCtx;
    private readonly IntPtr _fh;
    private readonly long _length;
    private long _position;
    private bool _disposed;

    internal IsolatedNfsReadStream(IntPtr ownedCtx, IntPtr fh, long length)
    {
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
                int n = LibNfs.Read(_ownedCtx, _fh, pinned, chunk);
                if (n < 0)
                    throw new IOException(
                        $"NFS isolated read failed: {LibNfs.GetError(_ownedCtx)}"
                    );
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

        long rc = LibNfs.Lseek(
            _ownedCtx,
            _fh,
            target,
            0 /* SEEK_SET */
            ,
            out _
        );
        if (rc < 0)
            throw new IOException($"NFS isolated lseek failed: {LibNfs.GetError(_ownedCtx)}");

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

        LibNfs.Close(_ownedCtx, _fh);
        LibNfs.Umount(_ownedCtx);
        LibNfs.DestroyContext(_ownedCtx);

        base.Dispose(disposing);
    }
}
