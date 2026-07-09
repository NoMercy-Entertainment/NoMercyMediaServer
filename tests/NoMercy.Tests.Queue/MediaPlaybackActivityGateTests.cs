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
/// Pins <see cref="MediaPlaybackActivityGate"/>: only the NAS-read-heavy
/// queues (library/file/import/extras) defer, and only while
/// <see cref="MediaActivityMonitor.IsActive"/> is true. The encoder queue and
/// network-only queues (e.g. music) must never defer, active or not.
/// </summary>
public class MediaPlaybackActivityGateTests
{
    [Theory]
    [InlineData("library")]
    [InlineData("file")]
    [InlineData("import")]
    [InlineData("extras")]
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
    [InlineData("import")]
    [InlineData("extras")]
    public void ShouldDefer_NasHeavyQueue_WhenInactive_ReturnsFalse(string queueName)
    {
        MediaActivityMonitor monitor = new();
        IWorkerActivityGate gate = new MediaPlaybackActivityGate(monitor);

        gate.ShouldDefer(queueName).Should().BeFalse();
    }

    [Theory]
    [InlineData("encoder")]
    [InlineData("music")]
    public void ShouldDefer_EncoderOrMusicQueue_WhenActive_ReturnsFalse(string queueName)
    {
        MediaActivityMonitor monitor = new();
        monitor.Touch();
        IWorkerActivityGate gate = new MediaPlaybackActivityGate(monitor);

        gate.ShouldDefer(queueName).Should().BeFalse();
    }

    [Theory]
    [InlineData("encoder")]
    [InlineData("music")]
    public void ShouldDefer_EncoderOrMusicQueue_WhenInactive_ReturnsFalse(string queueName)
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
}
