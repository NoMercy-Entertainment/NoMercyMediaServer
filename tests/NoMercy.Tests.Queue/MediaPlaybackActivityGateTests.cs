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

using FluentAssertions;
using NoMercy.NmSystem.Monitoring;
using NoMercy.Queue.MediaServer;
using NoMercyQueue;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Pins <see cref="MediaPlaybackActivityGate"/>: only "library" and "file"
/// (the queues that walk the filesystem/NAS directly) defer while
/// <see cref="MediaActivityMonitor.IsActive"/> is true, and only up to a
/// capped trickle window — they can never be starved indefinitely. "import"
/// and "extras" (API/DB-only work), plus the encoder and music queues, must
/// never defer, active or not.
/// </summary>
public class MediaPlaybackActivityGateTests
{
    [Theory]
    [InlineData("library")]
    [InlineData("file")]
    public void ShouldDefer_NasHeavyQueue_WhenActive_ReturnsTrue(string queueName)
    {
        MediaActivityMonitor monitor = new();
        monitor.Touch();
        IWorkerActivityGate gate = new MediaPlaybackActivityGate(monitor);

        gate.ShouldDefer(queueName).Should().BeTrue();
    }

    [Theory]
    [InlineData("library")]
    [InlineData("file")]
    public void ShouldDefer_NasHeavyQueue_WhenInactive_ReturnsFalse(string queueName)
    {
        MediaActivityMonitor monitor = new();
        IWorkerActivityGate gate = new MediaPlaybackActivityGate(monitor);

        gate.ShouldDefer(queueName).Should().BeFalse();
    }

    [Theory]
    [InlineData("import")]
    [InlineData("extras")]
    [InlineData("encoder")]
    [InlineData("music")]
    public void ShouldDefer_ApiDbOnlyOrNonNasQueue_WhenActive_ReturnsFalse(string queueName)
    {
        MediaActivityMonitor monitor = new();
        monitor.Touch();
        IWorkerActivityGate gate = new MediaPlaybackActivityGate(monitor);

        gate.ShouldDefer(queueName).Should().BeFalse();
    }

    [Theory]
    [InlineData("import")]
    [InlineData("extras")]
    [InlineData("encoder")]
    [InlineData("music")]
    public void ShouldDefer_ApiDbOnlyOrNonNasQueue_WhenInactive_ReturnsFalse(string queueName)
    {
        MediaActivityMonitor monitor = new();
        IWorkerActivityGate gate = new MediaPlaybackActivityGate(monitor);

        gate.ShouldDefer(queueName).Should().BeFalse();
    }

    [Fact]
    public void DeferInterval_IsTwoSeconds()
    {
        MediaActivityMonitor monitor = new();
        IWorkerActivityGate gate = new MediaPlaybackActivityGate(monitor);

        gate.DeferInterval.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ShouldDefer_NasHeavyQueue_AllowsOneJobThroughPerTrickleWindow()
    {
        MediaActivityMonitor monitor = new();
        monitor.Touch();
        DateTime now = new(2026, 1, 1, 0, 0, 0);
        MediaPlaybackActivityGate gate = new(
            monitor,
            TimeSpan.FromSeconds(30),
            () => now
        );

        // Still within the trickle window — deferred.
        gate.ShouldDefer("library").Should().BeTrue();

        // Trickle window elapsed — exactly one poll is let through...
        now = now.AddSeconds(31);
        gate.ShouldDefer("library").Should().BeFalse();

        // ...and immediately re-armed, so the next poll defers again.
        gate.ShouldDefer("library").Should().BeTrue();
    }

    [Fact]
    public void ShouldDefer_NasHeavyQueue_TracksTrickleWindowPerQueueIndependently()
    {
        MediaActivityMonitor monitor = new();
        monitor.Touch();
        DateTime now = new(2026, 1, 1, 0, 0, 0);
        MediaPlaybackActivityGate gate = new(
            monitor,
            TimeSpan.FromSeconds(30),
            () => now
        );

        // Establish "library"'s baseline, then let its window elapse.
        gate.ShouldDefer("library").Should().BeTrue();
        now = now.AddSeconds(31);
        gate.ShouldDefer("library").Should().BeFalse();

        // "file" has never been checked before, so its own window hasn't
        // started yet — it defers independently of "library" having just
        // trickled through.
        gate.ShouldDefer("file").Should().BeTrue();
    }
}
