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

using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Drivers.Nfs.Interop;
using NoMercy.Tests.Storage.Faults;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="IsolatedNfsReadStream"/> owns a dedicated libnfs context so
/// concurrent <c>AcquireLocalPath</c> calls (ffprobe/fpcalc/whisper staging)
/// cannot corrupt each other's NFSv4 open-seqid sequence. These tests demand
/// the read/seek/dispose contract directly against <see cref="FaultyLibNfs"/>
/// (the same fault-injection fake the driver's own recovery tests use) rather
/// than a mock of the stream itself.
/// </summary>
[Trait("Category", "Unit")]
public sealed class IsolatedNfsReadStreamTests
{
    private static (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) OpenSeeded(
        byte[] content
    )
    {
        FaultyLibNfs fake = new();
        fake.Seed("/file.bin", content);
        IntPtr ctx = fake.InitContext();
        fake.Open(ctx, "/file.bin", LibNfs.O_RDONLY, out IntPtr fh);
        return (fake, ctx, fh, content.Length);
    }

    [Fact]
    public void Read_returns_full_content_in_one_call()
    {
        byte[] content = "hello isolated nfs"u8.ToArray();
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content);
        using IsolatedNfsReadStream stream = new(fake, ctx, fh, length);

        byte[] buffer = new byte[content.Length];
        int read = stream.Read(buffer, 0, buffer.Length);

        read.Should().Be(content.Length);
        buffer.Should().Equal(content);
    }

    [Fact]
    public void Read_across_multiple_chunks_reassembles_content()
    {
        // ChunkSize is 32 KiB internally; force at least 3 chunk iterations.
        byte[] content = new byte[32 * 1024 * 3 + 17];
        new Random(42).NextBytes(content);
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content);
        using IsolatedNfsReadStream stream = new(fake, ctx, fh, length);

        byte[] buffer = new byte[content.Length];
        int totalRead = 0;
        int n;
        while (
            totalRead < buffer.Length
            && (n = stream.Read(buffer, totalRead, buffer.Length - totalRead)) > 0
        )
            totalRead += n;

        totalRead.Should().Be(content.Length);
        buffer
            .Should()
            .Equal(content, "chunked reads must reassemble into exactly the original bytes");
    }

    [Fact]
    public void Read_with_zero_or_negative_count_returns_zero_without_touching_libnfs()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3]);
        using IsolatedNfsReadStream stream = new(fake, ctx, fh, length);

        stream.Read([], 0, 0).Should().Be(0);
        fake.CallCounts.GetValueOrDefault(nameof(FaultyLibNfs.Read)).Should().Be(0);
    }

    [Fact]
    public void Read_past_end_of_file_returns_zero()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3]);
        using IsolatedNfsReadStream stream = new(fake, ctx, fh, length);
        stream.Seek(3, SeekOrigin.Begin);

        int read = stream.Read(new byte[10], 0, 10);

        read.Should().Be(0);
    }

    [Fact]
    public void Read_propagates_libnfs_error_as_IOException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3, 4]);
        fake.Faults["Read:0"] = (-5, "EIO");
        using IsolatedNfsReadStream stream = new(fake, ctx, fh, length);

        Action act = () => _ = stream.Read(new byte[4], 0, 4);

        act.Should().Throw<IOException>().WithMessage("*EIO*");
    }

    [Theory]
    [InlineData([SeekOrigin.Begin, 2L, 2L])]
    [InlineData([SeekOrigin.Current, 2L, 2L])]
    [InlineData([SeekOrigin.End, -1L, 3L])]
    public void Seek_computes_target_from_origin(
        SeekOrigin origin,
        long offset,
        long expectedPosition
    )
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3, 4]);
        using IsolatedNfsReadStream stream = new(fake, ctx, fh, length);

        long result = stream.Seek(offset, origin);

        result.Should().Be(expectedPosition);
        stream.Position.Should().Be(expectedPosition);
    }

    [Fact]
    public void Seek_with_invalid_origin_throws()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3]);
        using IsolatedNfsReadStream stream = new(fake, ctx, fh, length);

        Action act = () => stream.Seek(0, (SeekOrigin)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Seek_propagates_libnfs_error_as_IOException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3]);
        fake.Faults["Lseek:0"] = (-5, "ESPIPE");
        using IsolatedNfsReadStream stream = new(fake, ctx, fh, length);

        Action act = () => stream.Seek(1, SeekOrigin.Begin);

        act.Should().Throw<IOException>().WithMessage("*ESPIPE*");
    }

    [Fact]
    public void Position_setter_delegates_to_seek_from_begin()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3, 4, 5]);
        using IsolatedNfsReadStream stream = new(fake, ctx, fh, length);

        stream.Position = 3;

        stream.Position.Should().Be(3);
    }

    [Fact]
    public void Dispose_closes_unmounts_and_destroys_the_owned_context()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3]);
        IsolatedNfsReadStream stream = new(fake, ctx, fh, length);

        stream.Dispose();

        fake.CallCounts.GetValueOrDefault(nameof(FaultyLibNfs.Close)).Should().Be(1);
        fake.CallCounts.GetValueOrDefault(nameof(FaultyLibNfs.Umount)).Should().Be(1);
        fake.CallCounts.GetValueOrDefault(nameof(FaultyLibNfs.DestroyContext)).Should().Be(1);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3]);
        IsolatedNfsReadStream stream = new(fake, ctx, fh, length);

        stream.Dispose();
        stream.Dispose();

        fake.CallCounts.GetValueOrDefault(nameof(FaultyLibNfs.DestroyContext))
            .Should()
            .Be(1, "a second Dispose must not tear down the context again");
    }

    [Fact]
    public void Read_after_dispose_throws_ObjectDisposedException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3]);
        IsolatedNfsReadStream stream = new(fake, ctx, fh, length);
        stream.Dispose();

        Action act = () => _ = stream.Read(new byte[1], 0, 1);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Seek_after_dispose_throws_ObjectDisposedException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3]);
        IsolatedNfsReadStream stream = new(fake, ctx, fh, length);
        stream.Dispose();

        Action act = () => stream.Seek(0, SeekOrigin.Begin);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Capability_flags_reflect_read_only_seekable_stream()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3]);
        using IsolatedNfsReadStream stream = new(fake, ctx, fh, length);

        stream.CanRead.Should().BeTrue();
        stream.CanSeek.Should().BeTrue();
        stream.CanWrite.Should().BeFalse();
        stream.Length.Should().Be(3);
    }

    [Fact]
    public void Flush_is_a_noop()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3]);
        using IsolatedNfsReadStream stream = new(fake, ctx, fh, length);

        Action act = () => stream.Flush();

        act.Should().NotThrow();
    }

    [Fact]
    public void SetLength_and_Write_throw_NotSupportedException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded([1, 2, 3]);
        using IsolatedNfsReadStream stream = new(fake, ctx, fh, length);

        Action setLength = () => stream.SetLength(10);
        Action write = () => stream.Write([1], 0, 1);

        setLength.Should().Throw<NotSupportedException>();
        write.Should().Throw<NotSupportedException>();
    }
}
