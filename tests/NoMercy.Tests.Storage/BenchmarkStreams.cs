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

namespace NoMercy.Tests.Storage;

/// <summary>
/// Read-only source of <paramref name="length"/> deterministic bytes with no
/// backing buffer, so feeding a write stream from it never materializes the whole
/// payload in the test's memory — the benchmark measures the driver moving bytes,
/// not the test allocating them.
/// </summary>
internal sealed class GeneratedStream(int length) : Stream
{
    private long _position;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => length;
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int remaining = (int)Math.Min(val1: count, val2: length - _position);
        if (remaining <= 0)
            return 0;
        unchecked
        {
            uint state = (uint)_position * 2654435761u + 1u;
            for (int index = 0; index < remaining; index++)
            {
                state = state * 1664525u + 1013904223u;
                buffer[offset + index] = (byte)(state >> 24);
            }
        }
        _position += remaining;
        return remaining;
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}

/// <summary>Counts bytes and discards them — a zero-cost read sink.</summary>
internal sealed class CountingSink : Stream
{
    public long Total { get; private set; }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => Total;
    public override long Position
    {
        get => Total;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) => Total += count;

    public override void Write(ReadOnlySpan<byte> buffer) => Total += buffer.Length;

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
