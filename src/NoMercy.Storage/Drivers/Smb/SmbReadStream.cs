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
/// Read stream for the SMB driver. Pulls bytes from the share with <c>ReadFile</c>
/// at an advancing offset over its own dedicated SMB connection and open handle,
/// so a multi-GB read never buffers the whole file in memory. Server chunks that
/// overflow the caller's buffer are held in a small carry-over until the next
/// read. The connection is held for the stream's lifetime and closed on dispose.
/// </summary>
internal sealed class SmbReadStream : Stream
{
    // Per-request read size; the server caps at the negotiated MaxReadSize, 1 MiB
    // is a safe, widely-supported request that keeps round-trips low.
    private const int ChunkSize = 1024 * 1024;

    private readonly SmbSession _session;
    private readonly object _handle;
    private readonly string _path;
    private long _serverOffset;
    private byte[] _carry = [];
    private int _carryPos;
    private bool _eof;
    private bool _disposed;

    internal SmbReadStream(SmbSession session, object handle, string path)
    {
        _session = session;
        _handle = handle;
        _path = path;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0)
            return 0;

        // Serve from carry-over first.
        if (_carryPos < _carry.Length)
            return DrainCarry(buffer, offset, count);

        if (_eof)
            return 0;

        NTStatus st = _session.Store.ReadFile(out byte[] chunk, _handle, _serverOffset, ChunkSize);
        if (st == NTStatus.STATUS_END_OF_FILE || chunk is null || chunk.Length == 0)
        {
            _eof = true;
            return 0;
        }
        SmbStatus.EnsureSuccess(st, $"read '{_path}'");
        _serverOffset += chunk.Length;
        _carry = chunk;
        _carryPos = 0;
        return DrainCarry(buffer, offset, count);
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        return await Task.FromResult(Read(buffer, offset, count));
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        byte[] tmp = new byte[buffer.Length];
        int read = Read(tmp, 0, tmp.Length);
        tmp.AsSpan(0, read).CopyTo(buffer.Span);
        return ValueTask.FromResult(read);
    }

    private int DrainCarry(byte[] buffer, int offset, int count)
    {
        int available = _carry.Length - _carryPos;
        int take = Math.Min(available, count);
        Array.Copy(_carry, _carryPos, buffer, offset, take);
        _carryPos += take;
        return take;
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
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
