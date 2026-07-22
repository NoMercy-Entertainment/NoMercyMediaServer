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
[Trait(name: "Category", value: "Unit")]
public sealed class NfsReadStreamTests
{
    private static (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) OpenSeeded(byte[] content)
    {
        FaultyLibNfs fake = new();
        fake.Seed(path: "/file.bin", content: content);
        IntPtr ctx = fake.InitContext();
        fake.Open(nfs: ctx, path: "/file.bin", flags: LibNfs.O_RDONLY, fh: out IntPtr fh);
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
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        NfsReadStream stream = new(nfs: IntPtr.Zero, fh: IntPtr.Zero, length: 5, driverLock: lockObj);

        stream.CanRead.Should().BeTrue();
        stream.CanSeek.Should().BeTrue();
        stream.CanWrite.Should().BeFalse();
        stream.Length.Should().Be(expected: 5);
    }

    [Fact]
    public void Read_returns_full_content_in_one_call()
    {
        byte[] content = "hello shared nfs stream"u8.ToArray();
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: content);
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        using NfsReadStream stream = new(nfs: ctx, fh: fh, length: content.Length, driverLock: lockObj, libNfs: fake);

        byte[] buffer = new byte[content.Length];
        int read = stream.Read(buffer: buffer, offset: 0, count: buffer.Length);

        read.Should().Be(expected: content.Length);
        buffer.Should().Equal(elements: content);
    }

    [Fact]
    public void Read_with_small_chunk_size_reassembles_across_many_native_calls()
    {
        byte[] content = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: content);
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        // chunkSize=3 forces 4 native Read calls (3+3+3+1) for a single stream.Read.
        using NfsReadStream stream = new(nfs: ctx, fh: fh, length: content.Length, driverLock: lockObj, libNfs: fake, chunkSize: 3);

        byte[] buffer = new byte[content.Length];
        int read = stream.Read(buffer: buffer, offset: 0, count: buffer.Length);

        read.Should().Be(expected: content.Length);
        buffer.Should().Equal(elements: content);
        fake.CallCounts[key: nameof(FaultyLibNfs.Read)].Should().BeGreaterThanOrEqualTo(expected: 4);
    }

    [Fact]
    public void Read_with_zero_count_returns_zero_without_touching_libnfs()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3]);
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        using NfsReadStream stream = new(nfs: ctx, fh: fh, length: 3, driverLock: lockObj, libNfs: fake);

        stream.Read(buffer: [], offset: 0, count: 0).Should().Be(expected: 0);
        fake.CallCounts.GetValueOrDefault(key: nameof(FaultyLibNfs.Read)).Should().Be(expected: 0);
    }

    [Fact]
    public void Read_past_end_of_file_returns_zero()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3]);
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        using NfsReadStream stream = new(nfs: ctx, fh: fh, length: 3, driverLock: lockObj, libNfs: fake);
        stream.Seek(offset: 3, origin: SeekOrigin.Begin);

        stream.Read(buffer: new byte[4], offset: 0, count: 4).Should().Be(expected: 0);
    }

    [Fact]
    public void Read_propagates_libnfs_error_as_IOException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3, 4]);
        fake.Faults[key: "Read:0"] = (-5, "EIO");
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        using NfsReadStream stream = new(nfs: ctx, fh: fh, length: 4, driverLock: lockObj, libNfs: fake);

        Action act = () => _ = stream.Read(buffer: new byte[4], offset: 0, count: 4);

        act.Should().Throw<IOException>().WithMessage(expectedWildcardPattern: "*EIO*");
    }

    [Fact]
    public void Read_while_driver_lock_is_disposed_surfaces_as_IOException()
    {
        // Simulates the driver being disposed while this stream's read call
        // is waiting on the SHARED lock — must not let a raw
        // ObjectDisposedException from the semaphore escape and crash the
        // HTTP pipeline serving the range request.
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3]);
        SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        using NfsReadStream stream = new(nfs: ctx, fh: fh, length: 3, driverLock: lockObj, libNfs: fake);
        lockObj.Dispose();

        Action act = () => _ = stream.Read(buffer: new byte[3], offset: 0, count: 3);

        act.Should()
            .Throw<IOException>()
            .WithMessage(
                expectedWildcardPattern: "*disposed*",
                because: "the disposed-lock race must surface as a clean IOException, not a raw ObjectDisposedException"
            );
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
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3, 4]);
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        using NfsReadStream stream = new(nfs: ctx, fh: fh, length: 4, driverLock: lockObj, libNfs: fake);

        long result = stream.Seek(offset: offset, origin: origin);

        result.Should().Be(expected: expectedPosition);
        stream.Position.Should().Be(expected: expectedPosition);
    }

    [Fact]
    public void Seek_with_invalid_origin_throws()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3]);
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        using NfsReadStream stream = new(nfs: ctx, fh: fh, length: 3, driverLock: lockObj, libNfs: fake);

        Action act = () => stream.Seek(offset: 0, origin: (SeekOrigin)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Seek_propagates_libnfs_error_as_IOException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3]);
        fake.Faults[key: "Lseek:0"] = (-5, "ESPIPE");
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        using NfsReadStream stream = new(nfs: ctx, fh: fh, length: 3, driverLock: lockObj, libNfs: fake);

        Action act = () => stream.Seek(offset: 1, origin: SeekOrigin.Begin);

        act.Should().Throw<IOException>().WithMessage(expectedWildcardPattern: "*ESPIPE*");
    }

    [Fact]
    public void Seek_while_driver_lock_is_disposed_surfaces_as_IOException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3]);
        SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        using NfsReadStream stream = new(nfs: ctx, fh: fh, length: 3, driverLock: lockObj, libNfs: fake);
        lockObj.Dispose();

        Action act = () => stream.Seek(offset: 0, origin: SeekOrigin.Begin);

        act.Should().Throw<IOException>().WithMessage(expectedWildcardPattern: "*disposed*");
    }

    [Fact]
    public void Position_setter_delegates_to_seek_from_begin()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3, 4, 5]);
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        using NfsReadStream stream = new(nfs: ctx, fh: fh, length: 5, driverLock: lockObj, libNfs: fake);

        stream.Position = 4;

        stream.Position.Should().Be(expected: 4);
    }

    [Fact]
    public void Dispose_closes_the_handle_under_the_shared_lock()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3]);
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        NfsReadStream stream = new(nfs: ctx, fh: fh, length: 3, driverLock: lockObj, libNfs: fake);

        stream.Dispose();

        fake.CallCounts.GetValueOrDefault(key: nameof(FaultyLibNfs.Close)).Should().Be(expected: 1);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3]);
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        NfsReadStream stream = new(nfs: ctx, fh: fh, length: 3, driverLock: lockObj, libNfs: fake);

        stream.Dispose();
        stream.Dispose();

        fake.CallCounts.GetValueOrDefault(key: nameof(FaultyLibNfs.Close))
            .Should()
            .Be(expected: 1, because: "a second Dispose must not re-close the handle");
    }

    [Fact]
    public void Dispose_when_driver_lock_already_disposed_does_not_throw()
    {
        // The DRIVER'S OWN Dispose tears down the context and its lock first;
        // this stream's Dispose must tolerate the lock already being gone
        // instead of throwing during cleanup.
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3]);
        SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        NfsReadStream stream = new(nfs: ctx, fh: fh, length: 3, driverLock: lockObj, libNfs: fake);
        lockObj.Dispose();

        Action act = () => stream.Dispose();

        act.Should()
            .NotThrow(because: "the fh is already gone once the driver disposed; nothing left to close");
    }

    [Fact]
    public void Read_after_dispose_throws_ObjectDisposedException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3]);
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        NfsReadStream stream = new(nfs: ctx, fh: fh, length: 3, driverLock: lockObj, libNfs: fake);
        stream.Dispose();

        Action act = () => _ = stream.Read(buffer: new byte[1], offset: 0, count: 1);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Seek_after_dispose_throws_ObjectDisposedException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3]);
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        NfsReadStream stream = new(nfs: ctx, fh: fh, length: 3, driverLock: lockObj, libNfs: fake);
        stream.Dispose();

        Action act = () => stream.Seek(offset: 0, origin: SeekOrigin.Begin);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void SetLength_and_Write_throw_NotSupportedException()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3]);
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        using NfsReadStream stream = new(nfs: ctx, fh: fh, length: 3, driverLock: lockObj, libNfs: fake);

        Action setLength = () => stream.SetLength(value: 10);
        Action write = () => stream.Write(buffer: [1], offset: 0, count: 1);

        setLength.Should().Throw<NotSupportedException>();
        write.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Flush_is_a_noop()
    {
        (FaultyLibNfs fake, IntPtr ctx, IntPtr fh) = OpenSeeded(content: [1, 2, 3]);
        using SemaphoreSlim lockObj = new(initialCount: 1, maxCount: 1);
        using NfsReadStream stream = new(nfs: ctx, fh: fh, length: 3, driverLock: lockObj, libNfs: fake);

        Action act = () => stream.Flush();

        act.Should().NotThrow();
    }
}
