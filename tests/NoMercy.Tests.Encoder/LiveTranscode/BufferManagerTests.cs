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

using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Tests.Encoder.LiveTranscode;

public class BufferManagerTests
{
    private readonly BufferManager _manager = new(new LiveSessionLimits());

    // ──────────────────────────────────────────────────────────────────────────
    // Suspend
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Buffer35s_NotSuspended_ReturnsSuspend()
    {
        BufferAction action = _manager.Evaluate(TimeSpan.FromSeconds(35), isSuspended: false);

        action.Should().Be(BufferAction.Suspend);
    }

    [Fact]
    public void Buffer35s_AlreadySuspended_ReturnsNone()
    {
        BufferAction action = _manager.Evaluate(TimeSpan.FromSeconds(35), isSuspended: true);

        action.Should().Be(BufferAction.None);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Resume
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Buffer10s_Suspended_ReturnsResume()
    {
        BufferAction action = _manager.Evaluate(TimeSpan.FromSeconds(10), isSuspended: true);

        action.Should().Be(BufferAction.Resume);
    }

    [Fact]
    public void Buffer10s_NotSuspended_ReturnsNone()
    {
        BufferAction action = _manager.Evaluate(TimeSpan.FromSeconds(10), isSuspended: false);

        action.Should().Be(BufferAction.None);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Quality drops
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Buffer4s_ReturnsDropQuality()
    {
        BufferAction action = _manager.Evaluate(TimeSpan.FromSeconds(4), isSuspended: false);

        action.Should().Be(BufferAction.DropQuality);
    }

    [Fact]
    public void Buffer2s_ReturnsEmergencyDropQuality()
    {
        BufferAction action = _manager.Evaluate(TimeSpan.FromSeconds(2), isSuspended: false);

        action.Should().Be(BufferAction.EmergencyDropQuality);
    }

    [Fact]
    public void Buffer5s_Boundary_ReturnsDropQuality()
    {
        // < 5 triggers DropQuality, exactly 5 should not
        BufferAction below = _manager.Evaluate(TimeSpan.FromSeconds(4.9), isSuspended: false);
        BufferAction boundary = _manager.Evaluate(TimeSpan.FromSeconds(5), isSuspended: false);

        below.Should().Be(BufferAction.DropQuality);
        boundary.Should().Be(BufferAction.None);
    }

    [Fact]
    public void Buffer3s_Boundary_ReturnsEmergencyDropQuality()
    {
        // < 3 triggers Emergency, exactly 3 should be DropQuality
        BufferAction below = _manager.Evaluate(TimeSpan.FromSeconds(2.9), isSuspended: false);
        BufferAction boundary = _manager.Evaluate(TimeSpan.FromSeconds(3), isSuspended: false);

        below.Should().Be(BufferAction.EmergencyDropQuality);
        boundary.Should().Be(BufferAction.DropQuality);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // None
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Buffer20s_NotSuspended_ReturnsNone()
    {
        BufferAction action = _manager.Evaluate(TimeSpan.FromSeconds(20), isSuspended: false);

        action.Should().Be(BufferAction.None);
    }

    [Fact]
    public void Buffer30s_Boundary_Suspend()
    {
        // > 30 triggers suspend, exactly 30 should not
        BufferAction above = _manager.Evaluate(TimeSpan.FromSeconds(30.1), isSuspended: false);
        BufferAction boundary = _manager.Evaluate(TimeSpan.FromSeconds(30), isSuspended: false);

        above.Should().Be(BufferAction.Suspend);
        boundary.Should().Be(BufferAction.None);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Configurable thresholds (LiveSessionLimits.BufferThresholds)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CustomSuspendThreshold_Respected()
    {
        // Operator wants a tighter suspend point — e.g. low-storage hosts.
        LiveSessionLimits limits = new() { Buffer = new() { SuspendAboveSeconds = 10 } };
        BufferManager manager = new(limits);

        manager
            .Evaluate(TimeSpan.FromSeconds(11), isSuspended: false)
            .Should()
            .Be(BufferAction.Suspend);
        manager
            .Evaluate(TimeSpan.FromSeconds(9), isSuspended: false)
            .Should()
            .Be(BufferAction.None);
    }

    [Fact]
    public void CustomDropThresholds_Respected()
    {
        // Operator wants more aggressive quality drops — e.g. spotty network.
        LiveSessionLimits limits = new()
        {
            Buffer = new() { DropQualityBelowSeconds = 10, EmergencyDropBelowSeconds = 6 },
        };
        BufferManager manager = new(limits);

        manager
            .Evaluate(TimeSpan.FromSeconds(9), isSuspended: false)
            .Should()
            .Be(BufferAction.DropQuality);
        manager
            .Evaluate(TimeSpan.FromSeconds(5), isSuspended: false)
            .Should()
            .Be(BufferAction.EmergencyDropQuality);
    }

    [Fact]
    public void CustomResumeThreshold_Respected()
    {
        // Operator wants a wider hysteresis between suspend / resume.
        LiveSessionLimits limits = new()
        {
            Buffer = new() { SuspendAboveSeconds = 30, ResumeBelowSeconds = 25 },
        };
        BufferManager manager = new(limits);

        manager
            .Evaluate(TimeSpan.FromSeconds(24), isSuspended: true)
            .Should()
            .Be(BufferAction.Resume);
        manager
            .Evaluate(TimeSpan.FromSeconds(26), isSuspended: true)
            .Should()
            .Be(BufferAction.None);
    }
}
