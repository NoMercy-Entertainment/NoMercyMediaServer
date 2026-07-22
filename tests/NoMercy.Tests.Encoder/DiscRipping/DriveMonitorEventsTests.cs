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
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Drives.Backends;

namespace NoMercy.Tests.Encoder.DiscRipping;

/// <summary>
/// Verifies the event payload shape produced by <see cref="DriveMonitor"/> and the
/// <see cref="DriveEvent"/> / <see cref="DriveEventType"/> records used to carry
/// drive state to the SignalR hub.
/// </summary>
public class DriveMonitorEventsTests
{
    // ── DriveEvent record shape ──────────────────────────────────────────────

    [Fact]
    public void DriveEvent_DiscInserted_HasExpectedShape()
    {
        DiscDrive drive = new(Path: "D:\\", Label: "MY_DISC", HasDisc: true, DiscType: OpticalDiscType.BluRay);
        DriveEvent evt = new(Type: DriveEventType.DiscInserted, Drive: drive);

        evt.Type.Should().Be(expected: DriveEventType.DiscInserted);
        evt.Drive.Path.Should().Be(expected: "D:\\");
        evt.Drive.Label.Should().Be(expected: "MY_DISC");
        evt.Drive.HasDisc.Should().BeTrue();
        evt.Drive.DiscType.Should().Be(expected: OpticalDiscType.BluRay);
    }

    [Fact]
    public void DriveEvent_DiscEjected_HasExpectedShape()
    {
        DiscDrive drive = new(Path: "E:\\", Label: string.Empty, HasDisc: false, DiscType: OpticalDiscType.None);
        DriveEvent evt = new(Type: DriveEventType.DiscEjected, Drive: drive);

        evt.Type.Should().Be(expected: DriveEventType.DiscEjected);
        evt.Drive.HasDisc.Should().BeFalse();
        evt.Drive.Label.Should().BeEmpty();
    }

    [Fact]
    public void DriveEvent_DriveAdded_HasExpectedShape()
    {
        DiscDrive drive = new(Path: "F:\\", Label: string.Empty, HasDisc: false, DiscType: OpticalDiscType.None);
        DriveEvent evt = new(Type: DriveEventType.DriveAdded, Drive: drive);

        evt.Type.Should().Be(expected: DriveEventType.DriveAdded);
        evt.Drive.Path.Should().Be(expected: "F:\\");
    }

    [Fact]
    public void DriveEvent_DriveRemoved_HasExpectedShape()
    {
        DiscDrive drive = new(Path: "G:\\", Label: string.Empty, HasDisc: false, DiscType: OpticalDiscType.None);
        DriveEvent evt = new(Type: DriveEventType.DriveRemoved, Drive: drive);

        evt.Type.Should().Be(expected: DriveEventType.DriveRemoved);
    }

    // ── DriveEventType covers all four cases ────────────────────────────────

    [Theory]
    [InlineData(data: DriveEventType.DriveAdded)]
    [InlineData(data: DriveEventType.DriveRemoved)]
    [InlineData(data: DriveEventType.DiscInserted)]
    [InlineData(data: DriveEventType.DiscEjected)]
    public void DriveEventType_AllVariantsRoundTripThroughRecord(DriveEventType kind)
    {
        DiscDrive drive = new(Path: "H:\\", Label: string.Empty, HasDisc: false, DiscType: OpticalDiscType.None);
        DriveEvent evt = new(Type: kind, Drive: drive);

        evt.Type.Should().Be(expected: kind);
    }

    // ── DiscDrive record equality (value semantics) ──────────────────────────

    [Fact]
    public void DiscDrive_SameValues_AreEqual()
    {
        DiscDrive a = new(Path: "D:\\", Label: "LABEL", HasDisc: true, DiscType: OpticalDiscType.Dvd);
        DiscDrive b = new(Path: "D:\\", Label: "LABEL", HasDisc: true, DiscType: OpticalDiscType.Dvd);

        a.Should().Be(expected: b);
    }

    [Fact]
    public void DiscDrive_DifferentPath_AreNotEqual()
    {
        DiscDrive a = new(Path: "D:\\", Label: "LABEL", HasDisc: true, DiscType: OpticalDiscType.Dvd);
        DiscDrive b = new(Path: "E:\\", Label: "LABEL", HasDisc: true, DiscType: OpticalDiscType.Dvd);

        a.Should().NotBe(unexpected: b);
    }

    // ── GetDrives returns consistent shape ───────────────────────────────────

    [Fact]
    public void GetDrives_AllReturnedDrivesHaveNonEmptyPath()
    {
        DriveMonitor monitor = new(
            backend: new PollingDriveBackend(logger: NullLogger<PollingDriveBackend>.Instance)
        );

        IReadOnlyList<DiscDrive> drives = monitor.GetDrives();

        foreach (DiscDrive drive in drives)
        {
            drive.Path.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void GetDrives_DiscTypeIsNoneWhenNoDisc()
    {
        DriveMonitor monitor = new(
            backend: new PollingDriveBackend(logger: NullLogger<PollingDriveBackend>.Instance)
        );

        IReadOnlyList<DiscDrive> drives = monitor.GetDrives();

        foreach (DiscDrive drive in drives.Where(predicate: d => !d.HasDisc))
        {
            drive.DiscType.Should().Be(expected: OpticalDiscType.None);
        }
    }
}
