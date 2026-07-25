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
/// LocalPathLease is the seam between IStorage drivers that have a
/// local file (LocalStorage — no-op dispose) and drivers that need to
/// stage a remote object into a temp file (SMB / NFS / S3 / R2 — clean
/// up on dispose). The contract: Path is the safe path to hand to a
/// child process; DisposeAsync runs the cleanup callback exactly once.
/// </summary>
public class LocalPathLeaseTests
{
    [Fact]
    public void Constructor_StoresPath()
    {
        LocalPathLease lease = new("/tmp/some-staged-file.mkv");
        lease.Path.Should().Be("/tmp/some-staged-file.mkv");
    }

    [Fact]
    public void Constructor_NullPath_Throws()
    {
        Action act = () => new LocalPathLease(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("path");
    }

    [Fact]
    public async Task DisposeAsync_CallsOnDisposeCallback()
    {
        // The cleanup callback is how remote drivers unlink their temp
        // stage. It MUST fire on first dispose.
        int callCount = 0;
        LocalPathLease lease = new(
            "/tmp/remote-staged.mkv",
            () =>
            {
                callCount++;
                return ValueTask.CompletedTask;
            }
        );

        await lease.DisposeAsync();

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        // A second DisposeAsync — e.g. an outer using block re-disposing
        // — must NOT re-run the cleanup. Remote drivers rely on this to
        // avoid double-unlinking the temp file.
        int callCount = 0;
        LocalPathLease lease = new(
            "/tmp/remote-staged.mkv",
            () =>
            {
                callCount++;
                return ValueTask.CompletedTask;
            }
        );

        await lease.DisposeAsync();
        await lease.DisposeAsync();
        await lease.DisposeAsync();

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_NullOnDispose_DoesNotThrow()
    {
        // LocalStorage hands out leases with null callbacks (no-op dispose).
        LocalPathLease lease = new("/tmp/local.mkv");

        Func<Task> act = async () => await lease.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_CallbackThrows_ExceptionPropagates()
    {
        // The lease doesn't swallow callback errors — a remote driver
        // failing to unlink should surface to the caller, not silently
        // leak stage files.
        LocalPathLease lease = new(
            "/tmp/remote.mkv",
            () => throw new InvalidOperationException("unlink failed")
        );

        Func<Task> act = async () => await lease.DisposeAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("unlink failed");
    }
}
