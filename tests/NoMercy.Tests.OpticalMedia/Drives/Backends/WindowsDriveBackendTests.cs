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

using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Drives.Backends;

namespace NoMercy.Tests.OpticalMedia.Drives.Backends;

/// <summary>
/// REQUIREMENT: <see cref="WindowsDriveBackend"/> wires a real WMI
/// <c>__InstanceModificationEvent</c> watcher against <c>Win32_LogicalDisk</c>
/// (DriveType=5, CD-ROM) at construction, and <see cref="GetDrives"/> /
/// <see cref="ListenAsync"/> / <see cref="DisposeAsync"/> must behave
/// correctly around that real subscription without a physical drive present.
///
/// hardware-validate: this entire type only compiles/runs on Windows
/// ([SupportedOSPlatform("windows")]) and depends on the real WMI service —
/// no DI seam exists over <c>ManagementEventWatcher</c> or
/// <c>Win32_LogicalDisk</c>. <see cref="OnDriveChanged"/> (private,
/// WMI-event-driven) cannot be invoked without a real
/// <c>__InstanceModificationEvent</c>; itemized rather than faked with a
/// hand-built <c>EventArrivedEventArgs</c>, since that type's only public
/// constructor requires internal WMI plumbing this test has no seam for.
/// Each test runtime-skips on non-Windows hosts rather than relying on a
/// separate test package.
/// </summary>
[SupportedOSPlatform(platformName: "windows")]
[Trait(name: "Category", value: "Unit")]
public class WindowsDriveBackendTests
{
    [Fact]
    public async Task Constructor_SubscribesToWmiWatcher_WithoutThrowing()
    {
        if (!OperatingSystem.IsWindows())
            return;

        WindowsDriveBackend backend = new(logger: NullLogger<WindowsDriveBackend>.Instance);
        backend.Should().NotBeNull();
        await backend.DisposeAsync();
    }

    [Fact]
    public async Task GetDrives_RealOsCall_DoesNotThrowAndReturnsAList()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await using WindowsDriveBackend backend = new(logger: NullLogger<WindowsDriveBackend>.Instance);

        IReadOnlyList<DiscDrive> drives = backend.GetDrives();

        drives.Should().NotBeNull();
    }

    [Fact]
    public async Task ListenAsync_AlreadyCancelledToken_CompletesWithNoItems()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await using WindowsDriveBackend backend = new(logger: NullLogger<WindowsDriveBackend>.Instance);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        List<DriveEvent> observed = [];
        await foreach (DriveEvent ev in backend.ListenAsync(ct: cts.Token))
            observed.Add(item: ev);

        observed.Should().BeEmpty();
    }

    [Fact]
    public async Task ListenAsync_CancelledShortlyAfterStart_StopsCleanlyWithoutObservingAnEvent()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await using WindowsDriveBackend backend = new(logger: NullLogger<WindowsDriveBackend>.Instance);
        using CancellationTokenSource cts = new(delay: TimeSpan.FromMilliseconds(milliseconds: 100));

        List<DriveEvent> observed = [];
        Func<Task> act = async () =>
        {
            await foreach (DriveEvent ev in backend.ListenAsync(ct: cts.Token))
                observed.Add(item: ev);
        };

        await act.Should().NotThrowAsync();
        observed.Should().BeEmpty(because: "no physical drive is attached to raise a real WMI event");
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_IsIdempotent()
    {
        if (!OperatingSystem.IsWindows())
            return;

        WindowsDriveBackend backend = new(logger: NullLogger<WindowsDriveBackend>.Instance);

        Func<Task> act = async () =>
        {
            await backend.DisposeAsync();
            await backend.DisposeAsync();
        };

        await act.Should().NotThrowAsync();
    }
}
