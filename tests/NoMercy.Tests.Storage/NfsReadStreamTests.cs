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
/// <see cref="NfsReadStream"/> reads over a libnfs handle SHARED with the
/// owning driver, gated on the driver's <see cref="SemaphoreSlim"/> because
/// the libnfs context is not re-entrant. These tests demand: chunked reads
/// reassemble correctly, native errors surface as <see cref="IOException"/>,
/// and — the behavior that's easy to regress — a driver disposed while a
/// read/seek is waiting on the shared lock must surface as
/// <see cref="IOException"/>, not crash with a raw
/// <see cref="ObjectDisposedException"/> from the semaphore.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NfsReadStreamTests
{
    private static (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) OpenSeeded(byte[] content)
    {
        FaultyLibNfs fake = new();
        fake.Seed("/file.bin", content);
        IntPtr ctx = fake.InitContext();
        fake.Open(ctx, "/file.bin", LibNfs.O_RDONLY, out IntPtr fh);
        return (fake, ctx, fh);
    }

    [Fact]
    public void Delegating_constructor_wires_the_production_LibNfsPInvoke_instance()
    {
        // The 4-arg constructor is the one production code actually calls
        // (NfsStorageDriver.OpenRead). It must not touch any native P/Invoke
        // at construction time — only Read/Seek/Dispose do — so this is safe
        // to run without a loaded libnfs binary. Exercising it directly
        // proves the delegation wires LibNfsPInvoke.Instance rather than
        // silently requiring callers to supply one.
        using SemaphoreSlim lockObj = new(1, 1);
        NfsReadStream stream = new(IntPtr.Zero, IntPtr.Zero, 5, lockObj);

        stream.CanRead.Should().BeTrue();
        stream.CanSeek.Should().BeTrue();
        stream.CanWrite.Should().BeFalse();
        stream.Length.Should().Be(5);
    }

    [Fact]
    public void Read_returns_full_content_in_one_call()
    {
        byte[] content = "hello shared nfs stream"u8.ToArray();
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content);
        using SemaphoreSlim lockObj = new(1, 1);
        using NfsReadStream stream = new(ctx, fh, content.Length, lockObj, fake);

        byte[] buffer = new byte[content.Length];
        int read = stream.Read(buffer, 0, buffer.Length);

        read.Should().Be(content.Length);
        buffer.Should().Equal(content);
    }

    [Fact]
    public void Read_with_small_chunk_size_reassembles_across_many_native_calls()
    {
        byte[] content = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content);
        using SemaphoreSlim lockObj = new(1, 1);
        // chunkSize=3 forces 4 native Read calls (3+3+3+1) for a single stream.Read.
        using NfsReadStream stream = new(ctx, fh, content.Length, lockObj, fake, 3);

        byte[] buffer = new byte[content.Length];
        int read = stream.Read(buffer, 0, buffer.Length);

        read.Should().Be(content.Length);
        buffer.Should().Equal(content);
        fake.CallCounts[nameof(FaultyLibNfs.Read)].Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Read_with_zero_count_returns_zero_without_touching_libnfs()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3]);
        using SemaphoreSlim lockObj = new(1, 1);
        using NfsReadStream stream = new(ctx, fh, 3, lockObj, fake);

        stream.Read([], 0, 0).Should().Be(0);
        fake.CallCounts.GetValueOrDefault(nameof(FaultyLibNfs.Read)).Should().Be(0);
    }

    [Fact]
    public void Read_past_end_of_file_returns_zero()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3]);
        using SemaphoreSlim lockObj = new(1, 1);
        using NfsReadStream stream = new(ctx, fh, 3, lockObj, fake);
        stream.Seek(3, SeekOrigin.Begin);

        stream.Read(new byte[4], 0, 4).Should().Be(0);
    }

    [Fact]
    public void Read_propagates_libnfs_error_as_IOException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3, 4]);
        fake.Faults["Read:0"] = (-5, "EIO");
        using SemaphoreSlim lockObj = new(1, 1);
        using NfsReadStream stream = new(ctx, fh, 4, lockObj, fake);

        Action act = () => _ = stream.Read(new byte[4], 0, 4);

        act.Should().Throw<IOException>().WithMessage("*EIO*");
    }

    [Fact]
    public void Read_while_driver_lock_is_disposed_surfaces_as_IOException()
    {
        // Simulates the driver being disposed while this stream's read call
        // is waiting on the SHARED lock — must not let a raw
        // ObjectDisposedException from the semaphore escape and crash the
        // HTTP pipeline serving the range request.
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3]);
        SemaphoreSlim lockObj = new(1, 1);
        using NfsReadStream stream = new(ctx, fh, 3, lockObj, fake);
        lockObj.Dispose();

        Action act = () => _ = stream.Read(new byte[3], 0, 3);

        act.Should()
            .Throw<IOException>()
            .WithMessage(
                "*disposed*",
                "the disposed-lock race must surface as a clean IOException, not a raw ObjectDisposedException"
            );
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
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3, 4]);
        using SemaphoreSlim lockObj = new(1, 1);
        using NfsReadStream stream = new(ctx, fh, 4, lockObj, fake);

        long result = stream.Seek(offset, origin);

        result.Should().Be(expectedPosition);
        stream.Position.Should().Be(expectedPosition);
    }

    [Fact]
    public void Seek_with_invalid_origin_throws()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3]);
        using SemaphoreSlim lockObj = new(1, 1);
        using NfsReadStream stream = new(ctx, fh, 3, lockObj, fake);

        Action act = () => stream.Seek(0, (SeekOrigin)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Seek_propagates_libnfs_error_as_IOException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3]);
        fake.Faults["Lseek:0"] = (-5, "ESPIPE");
        using SemaphoreSlim lockObj = new(1, 1);
        using NfsReadStream stream = new(ctx, fh, 3, lockObj, fake);

        Action act = () => stream.Seek(1, SeekOrigin.Begin);

        act.Should().Throw<IOException>().WithMessage("*ESPIPE*");
    }

    [Fact]
    public void Seek_while_driver_lock_is_disposed_surfaces_as_IOException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3]);
        SemaphoreSlim lockObj = new(1, 1);
        using NfsReadStream stream = new(ctx, fh, 3, lockObj, fake);
        lockObj.Dispose();

        Action act = () => stream.Seek(0, SeekOrigin.Begin);

        act.Should().Throw<IOException>().WithMessage("*disposed*");
    }

    [Fact]
    public void Position_setter_delegates_to_seek_from_begin()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3, 4, 5]);
        using SemaphoreSlim lockObj = new(1, 1);
        using NfsReadStream stream = new(ctx, fh, 5, lockObj, fake);

        stream.Position = 4;

        stream.Position.Should().Be(4);
    }

    [Fact]
    public void Dispose_closes_the_handle_under_the_shared_lock()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3]);
        using SemaphoreSlim lockObj = new(1, 1);
        NfsReadStream stream = new(ctx, fh, 3, lockObj, fake);

        stream.Dispose();

        fake.CallCounts.GetValueOrDefault(nameof(FaultyLibNfs.Close)).Should().Be(1);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3]);
        using SemaphoreSlim lockObj = new(1, 1);
        NfsReadStream stream = new(ctx, fh, 3, lockObj, fake);

        stream.Dispose();
        stream.Dispose();

        fake.CallCounts.GetValueOrDefault(nameof(FaultyLibNfs.Close))
            .Should()
            .Be(1, "a second Dispose must not re-close the handle");
    }

    [Fact]
    public void Dispose_when_driver_lock_already_disposed_does_not_throw()
    {
        // The DRIVER'S OWN Dispose tears down the context and its lock first;
        // this stream's Dispose must tolerate the lock already being gone
        // instead of throwing during cleanup.
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3]);
        SemaphoreSlim lockObj = new(1, 1);
        NfsReadStream stream = new(ctx, fh, 3, lockObj, fake);
        lockObj.Dispose();

        Action act = () => stream.Dispose();

        act.Should()
            .NotThrow("the fh is already gone once the driver disposed; nothing left to close");
    }

    [Fact]
    public void Read_after_dispose_throws_ObjectDisposedException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3]);
        using SemaphoreSlim lockObj = new(1, 1);
        NfsReadStream stream = new(ctx, fh, 3, lockObj, fake);
        stream.Dispose();

        Action act = () => _ = stream.Read(new byte[1], 0, 1);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Seek_after_dispose_throws_ObjectDisposedException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3]);
        using SemaphoreSlim lockObj = new(1, 1);
        NfsReadStream stream = new(ctx, fh, 3, lockObj, fake);
        stream.Dispose();

        Action act = () => stream.Seek(0, SeekOrigin.Begin);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void SetLength_and_Write_throw_NotSupportedException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3]);
        using SemaphoreSlim lockObj = new(1, 1);
        using NfsReadStream stream = new(ctx, fh, 3, lockObj, fake);

        Action setLength = () => stream.SetLength(10);
        Action write = () => stream.Write([1], 0, 1);

        setLength.Should().Throw<NotSupportedException>();
        write.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Flush_is_a_noop()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded([1, 2, 3]);
        using SemaphoreSlim lockObj = new(1, 1);
        using NfsReadStream stream = new(ctx, fh, 3, lockObj, fake);

        Action act = () => stream.Flush();

        act.Should().NotThrow();
    }
}
