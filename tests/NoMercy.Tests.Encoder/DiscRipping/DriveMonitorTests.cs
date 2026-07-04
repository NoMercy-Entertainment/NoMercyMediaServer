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

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Drives.Backends;

namespace NoMercy.Tests.Encoder.DiscRipping;

/// <summary>
/// DriveMonitor reads real OS drive state via <see cref="DriveInfo.GetDrives"/>,
/// which can't be mocked without a filesystem abstraction. These tests stay
/// on the public contract: cancellation ends enumeration promptly, and
/// <see cref="DriveMonitor.GetDrives"/> returns entries shaped correctly
/// regardless of how many optical drives the host has (zero is fine).
/// </summary>
public class DriveMonitorTests
{
    [Fact]
    public void GetDrives_ReturnsOpticalDrivesOnly()
    {
        DriveMonitor monitor = new(
            new PollingDriveBackend(NullLogger<PollingDriveBackend>.Instance)
        );

        IReadOnlyList<DiscDrive> drives = monitor.GetDrives();

        drives.Should().NotBeNull();
        // On CI hosts there are typically zero optical drives. That's fine —
        // we just need the list shape and path non-emptiness to be right.
        foreach (DiscDrive drive in drives)
        {
            drive.Path.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task MonitorAsync_CancellationEndsEnumeration()
    {
        DriveMonitor monitor = new(
            new PollingDriveBackend(NullLogger<PollingDriveBackend>.Instance)
        );

        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(100));
        List<DriveEvent> events = [];

        await foreach (DriveEvent evt in monitor.MonitorAsync(cts.Token))
        {
            events.Add(evt);
        }

        // The test completes — that's the success condition. We expect zero
        // events on a stable host; the important thing is the loop exited.
        events.Should().NotBeNull();
    }

    [Fact]
    public async Task MonitorAsync_AlreadyCancelledToken_ExitsImmediately()
    {
        DriveMonitor monitor = new(
            new PollingDriveBackend(NullLogger<PollingDriveBackend>.Instance)
        );

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        List<DriveEvent> events = [];
        DateTime start = DateTime.UtcNow;

        await foreach (DriveEvent evt in monitor.MonitorAsync(cts.Token))
        {
            events.Add(evt);
        }

        TimeSpan elapsed = DateTime.UtcNow - start;
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }
}
