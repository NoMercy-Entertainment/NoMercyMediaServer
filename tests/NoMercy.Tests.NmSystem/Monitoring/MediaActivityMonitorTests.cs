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

using System.Diagnostics;
using NoMercy.NmSystem.Monitoring;

namespace NoMercy.Tests.NmSystem.Monitoring;

/// <summary>
/// Pins <see cref="MediaActivityMonitor"/>: the idle-by-default state, that
/// <see cref="MediaActivityMonitor.Touch"/> flips <see cref="MediaActivityMonitor.IsActive"/>
/// on, and that <see cref="MediaActivityMonitor.WaitForIdleAsync"/> returns immediately
/// when nothing has ever touched it.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class MediaActivityMonitorTests
{
    [Fact]
    public void IsActive_InitialState_IsFalse()
    {
        MediaActivityMonitor monitor = new();

        monitor.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_RightAfterTouch_IsTrue()
    {
        MediaActivityMonitor monitor = new();

        monitor.Touch();

        monitor.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task WaitForIdleAsync_NeverTouched_ReturnsQuickly()
    {
        MediaActivityMonitor monitor = new();

        Stopwatch stopwatch = Stopwatch.StartNew();
        await monitor.WaitForIdleAsync(maxWait: TimeSpan.FromMinutes(minutes: 5), ct: CancellationToken.None);
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(expected: TimeSpan.FromSeconds(seconds: 1));
    }

    [Fact]
    public async Task WaitForIdleAsync_RespectsCancellation()
    {
        MediaActivityMonitor monitor = new();
        monitor.Touch();

        using CancellationTokenSource cts = new(delay: TimeSpan.FromMilliseconds(milliseconds: 100));

        Func<Task> act = () => monitor.WaitForIdleAsync(maxWait: TimeSpan.FromMinutes(minutes: 5), ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
