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

namespace NoMercy.Tests.OpticalMedia.Drives.Backends;

/// <summary>
/// REQUIREMENT: <see cref="PollingDriveBackend"/> is the cross-platform
/// fallback drive-state source. <see cref="PollingDriveBackend.GetDrives"/>
/// must enumerate real OS optical-drive state without throwing;
/// <see cref="PollingDriveBackend.ListenAsync"/> must observe cancellation
/// cleanly (both while idle and while the background poll loop is between
/// iterations), diff real drive state into insert events on its first poll,
/// and <see cref="PollingDriveBackend.DisposeAsync"/> must tear the loop
/// down without leaking.
///
/// hardware-validate: <c>Optical.GetOpticalDrives()</c> (NoMercy.NmSystem) is
/// a real OS call (DriveInfo enumeration on Windows, lsblk/diskutil shell-outs
/// elsewhere) with no DI seam — every assertion below reads whatever the host
/// machine's real optical drives currently report rather than asserting a
/// fixed drive letter or disc label, so the suite passes identically whether
/// or not a physical disc happens to be inserted at run time. When a disc IS
/// present (as it was verified to be for this run) the insert-event/label/
/// disc-type projection lines run for real; when none is present those lines
/// are still itemized residue (no disc to project), never faked.
/// </summary>
[Trait("Category", "Unit")]
public class PollingDriveBackendTests
{
    [Fact]
    public void GetDrives_RealOsCall_ReturnsSelfConsistentDriveEntries()
    {
        PollingDriveBackend backend = new(NullLogger<PollingDriveBackend>.Instance);

        IReadOnlyList<DiscDrive> drives = backend.GetDrives();

        drives.Should().NotBeNull();
        foreach (DiscDrive drive in drives)
        {
            drive.Path.Should().NotBeNullOrEmpty();
            if (drive.HasDisc)
            {
                drive
                    .DiscType.Should()
                    .NotBe(
                        OpticalDiscType.None,
                        "a drive reporting a disc must resolve a concrete disc type"
                    );
            }
            else
            {
                drive.Label.Should().BeNull("an empty drive has no volume label");
            }
        }
    }

    [Fact]
    public async Task ListenAsync_AlreadyCancelledToken_CompletesWithNoItems()
    {
        PollingDriveBackend backend = new(NullLogger<PollingDriveBackend>.Instance);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        List<DriveEvent> observed = [];
        await foreach (DriveEvent ev in backend.ListenAsync(cts.Token))
            observed.Add(ev);

        observed.Should().BeEmpty();

        await backend.DisposeAsync();
    }

    [Fact]
    public async Task ListenAsync_FirstPoll_ReportsCurrentlyPresentDiscsAsInsertedEvents()
    {
        // A fresh backend starts with an empty _knownDrives set, so its very
        // first poll tick (which fires almost immediately — see
        // PollLoopAsync) reports every currently-inserted disc as
        // DiscInserted. Real hardware state, read dynamically rather than
        // hardcoded, so this passes whether or not a disc is present.
        PollingDriveBackend probe = new(NullLogger<PollingDriveBackend>.Instance);
        IReadOnlyList<DiscDrive> currentState = probe.GetDrives();
        await probe.DisposeAsync();

        PollingDriveBackend backend = new(NullLogger<PollingDriveBackend>.Instance);
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));

        List<DriveEvent> observed = [];
        try
        {
            await foreach (DriveEvent ev in backend.ListenAsync(cts.Token))
                observed.Add(ev);
        }
        catch (OperationCanceledException)
        {
            // Expected once the 2s window closes.
        }

        int expectedInsertedCount = currentState.Count(d => d.HasDisc);
        observed
            .Count(e => e.Type == DriveEventType.DiscInserted)
            .Should()
            .Be(
                expectedInsertedCount,
                "every currently-present disc must surface exactly one insert event on the first poll"
            );

        foreach (DriveEvent ev in observed.Where(e => e.Type == DriveEventType.DiscInserted))
        {
            ev.Drive.HasDisc.Should().BeTrue();
            ev.Drive.DiscType.Should().NotBe(OpticalDiscType.None);
        }

        await backend.DisposeAsync();
    }

    [Fact]
    public async Task ListenAsync_CancelledShortlyAfterStart_StopsCleanly()
    {
        // Cancels well within the 5-second poll interval so the background
        // loop's Task.Delay observes cancellation immediately rather than
        // this test waiting out a real poll tick.
        PollingDriveBackend backend = new(NullLogger<PollingDriveBackend>.Instance);
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(100));

        Func<Task> act = async () =>
        {
            await foreach (DriveEvent _ in backend.ListenAsync(cts.Token))
            {
                // drain — assertion is only that cancellation is observed cleanly
            }
        };

        await act.Should()
            .NotThrowAsync(
                "cancellation must be observed cleanly, not thrown out of the enumerator"
            );

        await backend.DisposeAsync();
    }

    [Fact]
    public async Task ListenAsync_SurvivesAFullPollIntervalWithoutCancellation_LoopsAgain()
    {
        // The only way to reach PollLoopAsync's "try succeeded, fall through
        // to the next while iteration" path is to let one real 5-second
        // Task.Delay complete without cancelling — every other test in this
        // file cancels within the interval specifically to test the
        // cancellation path instead. This is the one test in the suite that
        // pays the real 5s cost for that fall-through branch.
        PollingDriveBackend backend = new(NullLogger<PollingDriveBackend>.Instance);
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5.5));

        Func<Task> act = async () =>
        {
            await foreach (DriveEvent _ in backend.ListenAsync(cts.Token))
            {
                // drain
            }
        };

        await act.Should().NotThrowAsync();

        await backend.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CalledWithoutEverListening_DoesNotThrow()
    {
        PollingDriveBackend backend = new(NullLogger<PollingDriveBackend>.Instance);

        Func<Task> act = async () => await backend.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_IsIdempotent()
    {
        PollingDriveBackend backend = new(NullLogger<PollingDriveBackend>.Instance);
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(50));

        await foreach (DriveEvent _ in backend.ListenAsync(cts.Token))
        {
            // drain
        }

        await backend.DisposeAsync();
        Func<Task> secondDispose = async () => await backend.DisposeAsync();

        await secondDispose.Should().NotThrowAsync();
    }
}
