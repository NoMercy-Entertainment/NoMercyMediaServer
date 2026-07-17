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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Tests.Encoder.LiveTranscode;

public class LiveSessionTests
{
    private static LiveQuality MakeQuality() =>
        new(
            Id: "1080p",
            Label: "1080p",
            Width: 1920,
            Height: 1080,
            Codec: VideoCodecType.H264,
            BitrateKbps: 8000,
            Encoder: "h264_nvenc",
            IsHardwareAccelerated: true,
            ExpectedSpeed: 5.0,
            CanRealtime: true
        );

    // ──────────────────────────────────────────────────────────────────────────
    // State
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Session_StartsInStartingState()
    {
        LiveSession session = new("sess-001", MakeQuality());

        session.State.Should().Be(LiveSessionState.Starting);
    }

    [Fact]
    public void SetState_ChangesState()
    {
        LiveSession session = new("sess-001", MakeQuality());

        session.SetState(LiveSessionState.Transcoding);

        session.State.Should().Be(LiveSessionState.Transcoding);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PushSegment
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PushSegment_UpdatesTranscodedPosition()
    {
        LiveSession session = new("sess-001", MakeQuality());
        TimeSpan start = TimeSpan.FromSeconds(10);
        TimeSpan duration = TimeSpan.FromSeconds(6);
        Segment segment = new(0, start, duration, "/tmp/seg0.ts", 500_000);

        session.PushSegment(segment);

        session.TranscodedPosition.Should().Be(start + duration);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Suspend / Resume
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Suspend_WhenTranscoding_ChangesToBuffered()
    {
        LiveSession session = new("sess-001", MakeQuality());
        session.SetState(LiveSessionState.Transcoding);

        session.Suspend();

        session.State.Should().Be(LiveSessionState.Buffered);
    }

    [Fact]
    public void Suspend_WhenNotTranscoding_DoesNotChange()
    {
        LiveSession session = new("sess-001", MakeQuality());
        session.SetState(LiveSessionState.Starting);

        session.Suspend();

        session.State.Should().Be(LiveSessionState.Starting);
    }

    [Fact]
    public void Resume_WhenBuffered_ChangesToTranscoding()
    {
        LiveSession session = new("sess-001", MakeQuality());
        session.SetState(LiveSessionState.Buffered);

        session.Resume();

        session.State.Should().Be(LiveSessionState.Transcoding);
    }

    [Fact]
    public void Resume_WhenNotBuffered_DoesNotChange()
    {
        LiveSession session = new("sess-001", MakeQuality());
        session.SetState(LiveSessionState.Starting);

        session.Resume();

        session.State.Should().Be(LiveSessionState.Starting);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ReportPlaybackPosition / BufferAhead
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ReportPlaybackPosition_UpdatesBufferAhead()
    {
        LiveSession session = new("sess-001", MakeQuality());
        Segment segment = new(
            0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(30),
            "/tmp/seg0.ts",
            1_000_000
        );
        session.PushSegment(segment);

        session.ReportPlaybackPosition(TimeSpan.FromSeconds(10), authoritative: true);

        // TranscodedPosition = 30s, PlaybackPosition = 10s → BufferAhead = 20s
        session.BufferAhead.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void BufferAhead_BeforeAnyReport_IsZeroOrPositive()
    {
        LiveSession session = new("sess-001", MakeQuality());

        session.BufferAhead.TotalSeconds.Should().BeGreaterThanOrEqualTo(0);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SeekAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeekAsync_SetsSeekingState()
    {
        LiveSession session = new("sess-001", MakeQuality());

        await session.SeekAsync(TimeSpan.FromSeconds(60), CancellationToken.None);

        session.State.Should().Be(LiveSessionState.Seeking);
    }

    [Fact]
    public async Task SeekAsync_DoesNotInvokeBufferResetCallback()
    {
        // A seek changes only the playhead — same quality, same absolute segment
        // indices, deterministic content — so nothing is invalidated. Invoking the
        // callback here is what used to wipe the coverage-aware buffer/on-disk
        // state on every seek and made re-watching already-transcoded ground
        // re-encode. Only ChangeQualityAsync still resets.
        LiveSession session = new("sess-001", MakeQuality());
        bool resetCalled = false;
        session.AttachBufferResetCallback(() => resetCalled = true);

        await session.SeekAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        resetCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeQualityAsync_InvokesBufferResetCallback()
    {
        LiveSession session = new("sess-001", MakeQuality());
        bool resetCalled = false;
        session.AttachBufferResetCallback(() => resetCalled = true);

        LiveQuality newQuality = new(
            Id: "720p",
            Label: "720p",
            Width: 1280,
            Height: 720,
            Codec: VideoCodecType.H264,
            BitrateKbps: 4000,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 1.5,
            CanRealtime: true
        );

        await session.ChangeQualityAsync("720p", newQuality, CancellationToken.None);

        resetCalled.Should().BeTrue();
    }

    [Fact]
    public async Task SeekAsync_RuntimeBuffer_IsNotClearedOnSeek()
    {
        // Same quality + absolute segment indices means a seek invalidates
        // nothing — the buffer-reset callback (wired to LiveRuntimeSession.ResetBuffer
        // by LiveStreamingService.Register) fires on quality change only, so a
        // segment buffered before the seek must still be there after it.
        LiveSession session = new("sess-001", MakeQuality());
        LiveRuntimeSession runtime = new(session, TimeSpan.FromSeconds(6));
        session.AttachBufferResetCallback(() => runtime.ResetBuffer());

        Segment seg0 = new(0, TimeSpan.Zero, TimeSpan.FromSeconds(6), "/tmp/seg0.ts", 100);
        Segment seg1 = new(
            1,
            TimeSpan.FromSeconds(6),
            TimeSpan.FromSeconds(6),
            "/tmp/seg1.ts",
            100
        );
        runtime.BufferSegment(seg0);
        runtime.BufferSegment(seg1);
        runtime.HighestSegmentIndex.Should().Be(1);

        await session.SeekAsync(TimeSpan.FromSeconds(60), CancellationToken.None);

        runtime.SnapshotSegments().Should().HaveCount(2);
        runtime.HighestSegmentIndex.Should().Be(1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Dispose
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_SetsEndedState()
    {
        LiveSession session = new("sess-001", MakeQuality());

        await session.DisposeAsync();

        session.State.Should().Be(LiveSessionState.Ended);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Segment channel
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Segments_ReadsFromChannel_AfterPushAndComplete()
    {
        LiveSession session = new("sess-001", MakeQuality());
        Segment seg0 = new(0, TimeSpan.Zero, TimeSpan.FromSeconds(6), "/tmp/seg0.ts", 300_000);
        Segment seg1 = new(
            1,
            TimeSpan.FromSeconds(6),
            TimeSpan.FromSeconds(6),
            "/tmp/seg1.ts",
            300_000
        );

        session.PushSegment(seg0);
        session.PushSegment(seg1);
        session.Complete();

        List<Segment> received = [];
        await foreach (Segment segment in session.Segments)
        {
            received.Add(segment);
        }

        received.Should().HaveCount(2);
        received[0].Index.Should().Be(0);
        received[1].Index.Should().Be(1);
    }
}
