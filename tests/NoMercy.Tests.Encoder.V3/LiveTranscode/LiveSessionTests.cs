namespace NoMercy.Tests.Encoder.V3.LiveTranscode;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.LiveTranscode;

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

        session.ReportPlaybackPosition(TimeSpan.FromSeconds(10));

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
