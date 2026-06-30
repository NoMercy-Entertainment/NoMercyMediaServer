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

namespace NoMercy.Storage.Drivers.Smb;

/// <summary>
/// Write stream for the SMB driver. Buffers to memory and flushes the whole
/// payload to the share on dispose via <see cref="SmbStorageDriver.WriteAllBytes"/>,
/// keeping the (non-re-entrant) SMB session single-threaded for one create+write.
/// </summary>
internal sealed class SmbUploadStream : Stream
{
    private readonly SmbStorageDriver _driver;
    private readonly string _path;
    private readonly MemoryStream _buffer = new();
    private bool _committed;

    internal SmbUploadStream(SmbStorageDriver driver, string path)
    {
        _driver = driver;
        _path = path;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _buffer.Length;
    public override long Position
    {
        get => _buffer.Position;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        _buffer.Write(buffer, offset, count);

    public override void Write(ReadOnlySpan<byte> buffer) => _buffer.Write(buffer);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        _buffer.WriteAsync(buffer, offset, count, ct);

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken ct = default
    ) => _buffer.WriteAsync(buffer, ct);

    public override void Flush() { }

    private void Commit()
    {
        if (_committed)
            return;
        _committed = true;
        _driver.WriteAllBytes(_path, _buffer.ToArray());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                Commit();
            }
            finally
            {
                _buffer.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
