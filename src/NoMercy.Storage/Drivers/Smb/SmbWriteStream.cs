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

using SMBLibrary;

namespace NoMercy.Storage.Drivers.Smb;

/// <summary>
/// Write stream for the SMB driver. Streams incoming bytes straight to the share
/// with <c>WriteFile</c> at an advancing offset over its own dedicated SMB
/// connection and open handle, so a multi-GB write never buffers in memory. The
/// connection is held for the stream's lifetime and closed on dispose.
/// </summary>
internal sealed class SmbWriteStream : Stream
{
    // SMB2 write payloads are capped by the negotiated MaxWriteSize; a larger
    // chunk is clamped there, so this trades managed round-trips for larger ones
    // up to that ceiling. Default set from the throughput sweep; the ctor accepts
    // an override so the sweep can measure the curve.
    private const int DefaultChunkSize = 1024 * 1024;

    private readonly int _chunkSize;
    private readonly SmbSession _session;
    private readonly object _handle;
    private readonly string _path;
    private long _position;
    private bool _disposed;

    internal SmbWriteStream(
        SmbSession session,
        object handle,
        string path,
        int chunkSize = DefaultChunkSize
    )
    {
        _session = session;
        _handle = handle;
        _path = path;
        _chunkSize = chunkSize;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _position;
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        int sent = 0;
        while (sent < count)
        {
            int len = Math.Min(_chunkSize, count - sent);
            byte[] chunk = new byte[len];
            Array.Copy(buffer, offset + sent, chunk, 0, len);
            NTStatus st = _session.Store.WriteFile(out int written, _handle, _position, chunk);
            SmbStatus.EnsureSuccess(st, $"write '{_path}'");
            _position += written;
            sent += written;
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        byte[] tmp = buffer.ToArray();
        Write(tmp, 0, tmp.Length);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken ct = default
    )
    {
        ct.ThrowIfCancellationRequested();
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        _disposed = true;
        if (disposing)
        {
            try
            {
                _session.Store.CloseFile(_handle);
            }
            finally
            {
                _session.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
