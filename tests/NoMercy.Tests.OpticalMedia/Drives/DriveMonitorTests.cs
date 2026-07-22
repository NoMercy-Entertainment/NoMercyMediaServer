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
[Trait(name: "Category", value: "Unit")]
public class DriveMonitorTests
{
    [Fact]
    public void GetDrives_DelegatesToBackend()
    {
        DiscDrive[] drives = [new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.Dvd)];
        Mock<IDriveBackend> backendMock = new();
        backendMock.Setup(expression: b => b.GetDrives()).Returns(value: drives);

        DriveMonitor monitor = new(backend: backendMock.Object);
        IReadOnlyList<DiscDrive> result = monitor.GetDrives();

        result.Should().BeSameAs(expected: drives);
        backendMock.Verify(expression: b => b.GetDrives(), times: Times.Once);
    }

    [Fact]
    public async Task MonitorAsync_DelegatesToBackendListenAsync()
    {
        DriveEvent[] events =
        [
            new(Type: DriveEventType.DiscInserted, Drive: new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.Dvd)),
        ];
        Mock<IDriveBackend> backendMock = new();
        backendMock
            .Setup(expression: b => b.ListenAsync(It.IsAny<CancellationToken>()))
            .Returns(value: ToAsyncEnumerable(events: events));

        DriveMonitor monitor = new(backend: backendMock.Object);

        List<DriveEvent> observed = [];
        await foreach (DriveEvent ev in monitor.MonitorAsync(ct: CancellationToken.None))
            observed.Add(item: ev);

        observed.Should().BeEquivalentTo(expectation: events);
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
