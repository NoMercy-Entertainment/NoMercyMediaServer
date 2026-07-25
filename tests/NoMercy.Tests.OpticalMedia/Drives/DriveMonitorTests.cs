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

using Moq;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;

namespace NoMercy.Tests.OpticalMedia.Drives;

/// <summary>
/// REQUIREMENT: <see cref="DriveMonitor"/> is a thin pass-through singleton —
/// <see cref="DriveMonitor.GetDrives"/> and <see cref="DriveMonitor.MonitorAsync"/>
/// must delegate verbatim to the injected <see cref="IDriveBackend"/> without
/// altering the drive list or the event stream.
/// </summary>
[Trait("Category", "Unit")]
public class DriveMonitorTests
{
    [Fact]
    public void GetDrives_DelegatesToBackend()
    {
        DiscDrive[] drives = [new("D:\\", "MOVIE", true, OpticalDiscType.Dvd)];
        Mock<IDriveBackend> backendMock = new();
        backendMock.Setup(b => b.GetDrives()).Returns(drives);

        DriveMonitor monitor = new(backendMock.Object);
        IReadOnlyList<DiscDrive> result = monitor.GetDrives();

        result.Should().BeSameAs(drives);
        backendMock.Verify(b => b.GetDrives(), Times.Once);
    }

    [Fact]
    public async Task MonitorAsync_DelegatesToBackendListenAsync()
    {
        DriveEvent[] events =
        [
            new(DriveEventType.DiscInserted, new("D:\\", "MOVIE", true, OpticalDiscType.Dvd)),
        ];
        Mock<IDriveBackend> backendMock = new();
        backendMock
            .Setup(b => b.ListenAsync(It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(events));

        DriveMonitor monitor = new(backendMock.Object);

        List<DriveEvent> observed = [];
        await foreach (DriveEvent ev in monitor.MonitorAsync(CancellationToken.None))
            observed.Add(ev);

        observed.Should().BeEquivalentTo(events);
    }

    private static async IAsyncEnumerable<DriveEvent> ToAsyncEnumerable(DriveEvent[] events)
    {
        foreach (DriveEvent ev in events)
        {
            await Task.Yield();
            yield return ev;
        }
    }
}
