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
    private readonly BufferManager _manager = new(limits: new());

    // ──────────────────────────────────────────────────────────────────────────
    // Suspend
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Buffer35s_NotSuspended_ReturnsSuspend()
    {
        BufferAction action = _manager.Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 35), isSuspended: false);

        action.Should().Be(expected: BufferAction.Suspend);
    }

    [Fact]
    public void Buffer35s_AlreadySuspended_ReturnsNone()
    {
        BufferAction action = _manager.Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 35), isSuspended: true);

        action.Should().Be(expected: BufferAction.None);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Resume
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Buffer10s_Suspended_ReturnsResume()
    {
        BufferAction action = _manager.Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 10), isSuspended: true);

        action.Should().Be(expected: BufferAction.Resume);
    }

    [Fact]
    public void Buffer10s_NotSuspended_ReturnsNone()
    {
        BufferAction action = _manager.Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 10), isSuspended: false);

        action.Should().Be(expected: BufferAction.None);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Quality drops
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Buffer4s_ReturnsDropQuality()
    {
        BufferAction action = _manager.Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 4), isSuspended: false);

        action.Should().Be(expected: BufferAction.DropQuality);
    }

    [Fact]
    public void Buffer2s_ReturnsEmergencyDropQuality()
    {
        BufferAction action = _manager.Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 2), isSuspended: false);

        action.Should().Be(expected: BufferAction.EmergencyDropQuality);
    }

    [Fact]
    public void Buffer5s_Boundary_ReturnsDropQuality()
    {
        // < 5 triggers DropQuality, exactly 5 should not
        BufferAction below = _manager.Evaluate(bufferAhead: TimeSpan.FromSeconds(value: 4.9), isSuspended: false);
        BufferAction boundary = _manager.Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 5), isSuspended: false);

        below.Should().Be(expected: BufferAction.DropQuality);
        boundary.Should().Be(expected: BufferAction.None);
    }

    [Fact]
    public void Buffer3s_Boundary_ReturnsEmergencyDropQuality()
    {
        // < 3 triggers Emergency, exactly 3 should be DropQuality
        BufferAction below = _manager.Evaluate(bufferAhead: TimeSpan.FromSeconds(value: 2.9), isSuspended: false);
        BufferAction boundary = _manager.Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 3), isSuspended: false);

        below.Should().Be(expected: BufferAction.EmergencyDropQuality);
        boundary.Should().Be(expected: BufferAction.DropQuality);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // None
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Buffer20s_NotSuspended_ReturnsNone()
    {
        BufferAction action = _manager.Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 20), isSuspended: false);

        action.Should().Be(expected: BufferAction.None);
    }

    [Fact]
    public void Buffer30s_Boundary_Suspend()
    {
        // > 30 triggers suspend, exactly 30 should not
        BufferAction above = _manager.Evaluate(bufferAhead: TimeSpan.FromSeconds(value: 30.1), isSuspended: false);
        BufferAction boundary = _manager.Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 30), isSuspended: false);

        above.Should().Be(expected: BufferAction.Suspend);
        boundary.Should().Be(expected: BufferAction.None);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Configurable thresholds (LiveSessionLimits.BufferThresholds)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CustomSuspendThreshold_Respected()
    {
        // Operator wants a tighter suspend point — e.g. low-storage hosts.
        LiveSessionLimits limits = new() { Buffer = new() { SuspendAboveSeconds = 10 } };
        BufferManager manager = new(limits: limits);

        manager
            .Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 11), isSuspended: false)
            .Should()
            .Be(expected: BufferAction.Suspend);
        manager
            .Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 9), isSuspended: false)
            .Should()
            .Be(expected: BufferAction.None);
    }

    [Fact]
    public void CustomDropThresholds_Respected()
    {
        // Operator wants more aggressive quality drops — e.g. spotty network.
        LiveSessionLimits limits = new()
        {
            Buffer = new() { DropQualityBelowSeconds = 10, EmergencyDropBelowSeconds = 6 },
        };
        BufferManager manager = new(limits: limits);

        manager
            .Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 9), isSuspended: false)
            .Should()
            .Be(expected: BufferAction.DropQuality);
        manager
            .Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 5), isSuspended: false)
            .Should()
            .Be(expected: BufferAction.EmergencyDropQuality);
    }

    [Fact]
    public void CustomResumeThreshold_Respected()
    {
        // Operator wants a wider hysteresis between suspend / resume.
        LiveSessionLimits limits = new()
        {
            Buffer = new() { SuspendAboveSeconds = 30, ResumeBelowSeconds = 25 },
        };
        BufferManager manager = new(limits: limits);

        manager
            .Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 24), isSuspended: true)
            .Should()
            .Be(expected: BufferAction.Resume);
        manager
            .Evaluate(bufferAhead: TimeSpan.FromSeconds(seconds: 26), isSuspended: true)
            .Should()
            .Be(expected: BufferAction.None);
    }
}
