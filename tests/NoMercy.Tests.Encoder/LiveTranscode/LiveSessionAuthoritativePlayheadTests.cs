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

using System.Reflection;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Tests.Encoder.LiveTranscode;

/// <summary>
/// Tests for LiveSession.ReportPlaybackPosition's authoritative/non-authoritative
/// split: a client-reported true playhead (authoritative) must win over the
/// segment-request-derived prefetch frontier (non-authoritative) for as long as
/// the authoritative report is fresh, so a player that prefetches far ahead of
/// where the user is actually watching cannot make BufferAhead read as
/// permanently low.
/// </summary>
public class LiveSessionAuthoritativePlayheadTests
{
    private static LiveQuality MakeQuality() =>
        new(
            Id: "1080p",
            Label: "1080p",
            Width: 1920,
            Height: 1080,
            Codec: VideoCodecType.H264,
            BitrateKbps: 8000,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 2.0,
            CanRealtime: true
        );

    // Directly backdates the private authority stamp instead of sleeping past
    // the real 15s window, mirroring the reflection technique already used in
    // LiveSessionSuspendResumeTests.GetRunnerCts for private-field assertions.
    private static void BackdateAuthoritativeStamp(LiveSession session, TimeSpan age)
    {
        FieldInfo field = typeof(LiveSession).GetField(
            "_lastAuthoritativePlayheadUtcTicks",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;
        field.SetValue(session, (DateTime.UtcNow - age).Ticks);
    }

    [Fact]
    public void AuthoritativeReport_SetsThePlayhead()
    {
        LiveSession session = new("sess-001", MakeQuality());
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(30), "/tmp/seg0.ts", 1));

        session.ReportPlaybackPosition(TimeSpan.FromSeconds(10), authoritative: true);

        session.BufferAhead.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void NonAuthoritativeReport_WithinAuthorityWindow_IsIgnored()
    {
        LiveSession session = new("sess-001", MakeQuality());
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(30), "/tmp/seg0.ts", 1));

        session.ReportPlaybackPosition(TimeSpan.FromSeconds(10), authoritative: true);

        // The prefetch frontier tries to drag the playhead forward to 25s; it
        // must be ignored while the authoritative report is still fresh.
        session.ReportPlaybackPosition(TimeSpan.FromSeconds(25), authoritative: false);

        session.BufferAhead.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void NonAuthoritativeReport_AfterAuthorityWindowExpires_Applies()
    {
        LiveSession session = new("sess-001", MakeQuality());
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(30), "/tmp/seg0.ts", 1));

        session.ReportPlaybackPosition(TimeSpan.FromSeconds(10), authoritative: true);
        BackdateAuthoritativeStamp(session, TimeSpan.FromSeconds(16));

        // No fresh heartbeat has landed for 16s (window is 15s) — the
        // segment-request-derived estimate is now allowed to move the playhead.
        session.ReportPlaybackPosition(TimeSpan.FromSeconds(25), authoritative: false);

        session.BufferAhead.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void NonAuthoritativeReport_BeforeAnyAuthoritativeReport_AppliesImmediately()
    {
        // A session that never received an authoritative report (an old client
        // that has not adopted ReportPlayhead) must keep the pre-fix behavior:
        // the segment-request-derived estimate drives the playhead outright.
        LiveSession session = new("sess-001", MakeQuality());
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(30), "/tmp/seg0.ts", 1));

        session.ReportPlaybackPosition(TimeSpan.FromSeconds(12), authoritative: false);

        session.BufferAhead.Should().Be(TimeSpan.FromSeconds(18));
    }

    [Fact]
    public async Task SeekAsync_RefreshesAuthority_SoAStalePrefetchReportCannotClobberIt()
    {
        LiveSession session = new("sess-001", MakeQuality());
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(60), "/tmp/seg0.ts", 1));
        session.ReportPlaybackPosition(TimeSpan.FromSeconds(10), authoritative: true);

        await session.SeekAsync(TimeSpan.FromSeconds(40), CancellationToken.None);

        // A prefetch report for the pre-seek position, landing just after the
        // seek completes, must not override the fresh seek target.
        session.ReportPlaybackPosition(TimeSpan.FromSeconds(5), authoritative: false);

        // SeekAsync resets TranscodedPosition to the seek target too, so a
        // clobbered playhead would show up as a non-zero BufferAhead here.
        session.BufferAhead.Should().Be(TimeSpan.Zero);
    }
}
