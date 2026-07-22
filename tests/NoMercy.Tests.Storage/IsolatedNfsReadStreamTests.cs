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
[Trait(name: "Category", value: "Unit")]
public sealed class IsolatedNfsReadStreamTests
{
    private static (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) OpenSeeded(
        byte[] content
    )
    {
        FaultyLibNfs fake = new();
        fake.Seed(path: "/file.bin", content: content);
        IntPtr ctx = fake.InitContext();
        fake.Open(nfs: ctx, path: "/file.bin", flags: LibNfs.O_RDONLY, fh: out IntPtr fh);
        return (fake, ctx, fh, content.Length);
    }

    [Fact]
    public void Read_returns_full_content_in_one_call()
    {
        byte[] content = "hello isolated nfs"u8.ToArray();
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: content);
        using IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);

        byte[] buffer = new byte[content.Length];
        int read = stream.Read(buffer: buffer, offset: 0, count: buffer.Length);

        read.Should().Be(expected: content.Length);
        buffer.Should().Equal(elements: content);
    }

    [Fact]
    public void Read_across_multiple_chunks_reassembles_content()
    {
        // ChunkSize is 32 KiB internally; force at least 3 chunk iterations.
        byte[] content = new byte[32 * 1024 * 3 + 17];
        new Random(Seed: 42).NextBytes(buffer: content);
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: content);
        using IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);

        byte[] buffer = new byte[content.Length];
        int totalRead = 0;
        int n;
        while (
            totalRead < buffer.Length
            && (n = stream.Read(buffer: buffer, offset: totalRead, count: buffer.Length - totalRead)) > 0
        )
            totalRead += n;

        totalRead.Should().Be(expected: content.Length);
        buffer
            .Should()
            .Equal(expected: content, because: "chunked reads must reassemble into exactly the original bytes");
    }

    [Fact]
    public void Read_with_zero_or_negative_count_returns_zero_without_touching_libnfs()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3]);
        using IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);

        stream.Read(buffer: [], offset: 0, count: 0).Should().Be(expected: 0);
        fake.CallCounts.GetValueOrDefault(key: nameof(FaultyLibNfs.Read)).Should().Be(expected: 0);
    }

    [Fact]
    public void Read_past_end_of_file_returns_zero()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3]);
        using IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);
        stream.Seek(offset: 3, origin: SeekOrigin.Begin);

        int read = stream.Read(buffer: new byte[10], offset: 0, count: 10);

        read.Should().Be(expected: 0);
    }

    [Fact]
    public void Read_propagates_libnfs_error_as_IOException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3, 4]);
        fake.Faults[key: "Read:0"] = (-5, "EIO");
        using IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);

        Action act = () => _ = stream.Read(buffer: new byte[4], offset: 0, count: 4);

        act.Should().Throw<IOException>().WithMessage(expectedWildcardPattern: "*EIO*");
    }

    [Theory]
    [InlineData(data: [SeekOrigin.Begin, 2L, 2L])]
    [InlineData(data: [SeekOrigin.Current, 2L, 2L])]
    [InlineData(data: [SeekOrigin.End, -1L, 3L])]
    public void Seek_computes_target_from_origin(
        SeekOrigin origin,
        long offset,
        long expectedPosition
    )
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3, 4]);
        using IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);

        long result = stream.Seek(offset: offset, origin: origin);

        result.Should().Be(expected: expectedPosition);
        stream.Position.Should().Be(expected: expectedPosition);
    }

    [Fact]
    public void Seek_with_invalid_origin_throws()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3]);
        using IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);

        Action act = () => stream.Seek(offset: 0, origin: (SeekOrigin)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Seek_propagates_libnfs_error_as_IOException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3]);
        fake.Faults[key: "Lseek:0"] = (-5, "ESPIPE");
        using IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);

        Action act = () => stream.Seek(offset: 1, origin: SeekOrigin.Begin);

        act.Should().Throw<IOException>().WithMessage(expectedWildcardPattern: "*ESPIPE*");
    }

    [Fact]
    public void Position_setter_delegates_to_seek_from_begin()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3, 4, 5]);
        using IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);

        stream.Position = 3;

        stream.Position.Should().Be(expected: 3);
    }

    [Fact]
    public void Dispose_closes_unmounts_and_destroys_the_owned_context()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3]);
        IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);

        stream.Dispose();

        fake.CallCounts.GetValueOrDefault(key: nameof(FaultyLibNfs.Close)).Should().Be(expected: 1);
        fake.CallCounts.GetValueOrDefault(key: nameof(FaultyLibNfs.Umount)).Should().Be(expected: 1);
        fake.CallCounts.GetValueOrDefault(key: nameof(FaultyLibNfs.DestroyContext)).Should().Be(expected: 1);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3]);
        IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);

        stream.Dispose();
        stream.Dispose();

        fake.CallCounts.GetValueOrDefault(key: nameof(FaultyLibNfs.DestroyContext))
            .Should()
            .Be(expected: 1, because: "a second Dispose must not tear down the context again");
    }

    [Fact]
    public void Read_after_dispose_throws_ObjectDisposedException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3]);
        IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);
        stream.Dispose();

        Action act = () => _ = stream.Read(buffer: new byte[1], offset: 0, count: 1);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Seek_after_dispose_throws_ObjectDisposedException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3]);
        IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);
        stream.Dispose();

        Action act = () => stream.Seek(offset: 0, origin: SeekOrigin.Begin);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Capability_flags_reflect_read_only_seekable_stream()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3]);
        using IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);

        stream.CanRead.Should().BeTrue();
        stream.CanSeek.Should().BeTrue();
        stream.CanWrite.Should().BeFalse();
        stream.Length.Should().Be(expected: 3);
    }

    [Fact]
    public void Flush_is_a_noop()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3]);
        using IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);

        Action act = () => stream.Flush();

        act.Should().NotThrow();
    }

    [Fact]
    public void SetLength_and_Write_throw_NotSupportedException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh, long length) = OpenSeeded(content: [1, 2, 3]);
        using IsolatedNfsReadStream stream = new(libNfs: fake, ownedCtx: ctx, fh: fh, length: length);

        Action setLength = () => stream.SetLength(value: 10);
        Action write = () => stream.Write(buffer: [1], offset: 0, count: 1);

        setLength.Should().Throw<NotSupportedException>();
        write.Should().Throw<NotSupportedException>();
    }
}
