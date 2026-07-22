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
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());

        session.State.Should().Be(expected: LiveSessionState.Starting);
    }

    [Fact]
    public void SetState_ChangesState()
    {
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());

        session.SetState(state: LiveSessionState.Transcoding);

        session.State.Should().Be(expected: LiveSessionState.Transcoding);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PushSegment
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PushSegment_UpdatesTranscodedPosition()
    {
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());
        TimeSpan start = TimeSpan.FromSeconds(seconds: 10);
        TimeSpan duration = TimeSpan.FromSeconds(seconds: 6);
        Segment segment = new(Index: 0, StartTime: start, Duration: duration, FilePath: "/tmp/seg0.ts", SizeBytes: 500_000);

        session.PushSegment(segment: segment);

        session.TranscodedPosition.Should().Be(expected: start + duration);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Suspend / Resume
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Suspend_WhenTranscoding_ChangesToBuffered()
    {
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());
        session.SetState(state: LiveSessionState.Transcoding);

        session.Suspend();

        session.State.Should().Be(expected: LiveSessionState.Buffered);
    }

    [Fact]
    public void Suspend_WhenNotTranscoding_DoesNotChange()
    {
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());
        session.SetState(state: LiveSessionState.Starting);

        session.Suspend();

        session.State.Should().Be(expected: LiveSessionState.Starting);
    }

    [Fact]
    public void Resume_WhenBuffered_ChangesToTranscoding()
    {
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());
        session.SetState(state: LiveSessionState.Buffered);

        session.Resume();

        session.State.Should().Be(expected: LiveSessionState.Transcoding);
    }

    [Fact]
    public void Resume_WhenNotBuffered_DoesNotChange()
    {
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());
        session.SetState(state: LiveSessionState.Starting);

        session.Resume();

        session.State.Should().Be(expected: LiveSessionState.Starting);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ReportPlaybackPosition / BufferAhead
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ReportPlaybackPosition_UpdatesBufferAhead()
    {
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());
        Segment segment = new(
            Index: 0,
            StartTime: TimeSpan.Zero,
            Duration: TimeSpan.FromSeconds(seconds: 30),
            FilePath: "/tmp/seg0.ts",
            SizeBytes: 1_000_000
        );
        session.PushSegment(segment: segment);

        session.ReportPlaybackPosition(position: TimeSpan.FromSeconds(seconds: 10), authoritative: true);

        // TranscodedPosition = 30s, PlaybackPosition = 10s → BufferAhead = 20s
        session.BufferAhead.Should().Be(expected: TimeSpan.FromSeconds(seconds: 20));
    }

    [Fact]
    public void BufferAhead_BeforeAnyReport_IsZeroOrPositive()
    {
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());

        session.BufferAhead.TotalSeconds.Should().BeGreaterThanOrEqualTo(expected: 0);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SeekAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeekAsync_SetsSeekingState()
    {
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 60), ct: CancellationToken.None);

        session.State.Should().Be(expected: LiveSessionState.Seeking);
    }

    [Fact]
    public async Task SeekAsync_DoesNotInvokeBufferResetCallback()
    {
        // A seek changes only the playhead — same quality, same absolute segment
        // indices, deterministic content — so nothing is invalidated. Invoking the
        // callback here is what used to wipe the coverage-aware buffer/on-disk
        // state on every seek and made re-watching already-transcoded ground
        // re-encode. Only ChangeQualityAsync still resets.
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());
        bool resetCalled = false;
        session.AttachBufferResetCallback(callback: () => resetCalled = true);

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 30), ct: CancellationToken.None);

        resetCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeQualityAsync_InvokesBufferResetCallback()
    {
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());
        bool resetCalled = false;
        session.AttachBufferResetCallback(callback: () => resetCalled = true);

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

        await session.ChangeQualityAsync(qualityId: "720p", newQuality: newQuality, ct: CancellationToken.None);

        resetCalled.Should().BeTrue();
    }

    [Fact]
    public async Task SeekAsync_RuntimeBuffer_IsNotClearedOnSeek()
    {
        // Same quality + absolute segment indices means a seek invalidates
        // nothing — the buffer-reset callback (wired to LiveRuntimeSession.ResetBuffer
        // by LiveStreamingService.Register) fires on quality change only, so a
        // segment buffered before the seek must still be there after it.
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());
        LiveRuntimeSession runtime = new(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));
        session.AttachBufferResetCallback(callback: () => runtime.ResetBuffer());

        Segment seg0 = new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 6), FilePath: "/tmp/seg0.ts", SizeBytes: 100);
        Segment seg1 = new(
            Index: 1,
            StartTime: TimeSpan.FromSeconds(seconds: 6),
            Duration: TimeSpan.FromSeconds(seconds: 6),
            FilePath: "/tmp/seg1.ts",
            SizeBytes: 100
        );
        runtime.BufferSegment(segment: seg0);
        runtime.BufferSegment(segment: seg1);
        runtime.HighestSegmentIndex.Should().Be(expected: 1);

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 60), ct: CancellationToken.None);

        runtime.SnapshotSegments().Should().HaveCount(expected: 2);
        runtime.HighestSegmentIndex.Should().Be(expected: 1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Dispose
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_SetsEndedState()
    {
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());

        await session.DisposeAsync();

        session.State.Should().Be(expected: LiveSessionState.Ended);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Segment channel
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Segments_ReadsFromChannel_AfterPushAndComplete()
    {
        LiveSession session = new(sessionId: "sess-001", quality: MakeQuality());
        Segment seg0 = new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 6), FilePath: "/tmp/seg0.ts", SizeBytes: 300_000);
        Segment seg1 = new(
            Index: 1,
            StartTime: TimeSpan.FromSeconds(seconds: 6),
            Duration: TimeSpan.FromSeconds(seconds: 6),
            FilePath: "/tmp/seg1.ts",
            SizeBytes: 300_000
        );

        session.PushSegment(segment: seg0);
        session.PushSegment(segment: seg1);
        session.Complete();

        List<Segment> received = [];
        await foreach (Segment segment in session.Segments)
        {
            received.Add(item: segment);
        }

        received.Should().HaveCount(expected: 2);
        received[index: 0].Index.Should().Be(expected: 0);
        received[index: 1].Index.Should().Be(expected: 1);
    }
}
