namespace NoMercy.Tests.Encoder.LiveTranscode.Protocol;

using Newtonsoft.Json;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Encoder.LiveTranscode.Protocol;

public class ProtocolMessageTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // LiveSessionState
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LiveSessionState.Starting)]
    [InlineData(LiveSessionState.Transcoding)]
    [InlineData(LiveSessionState.Buffering)]
    [InlineData(LiveSessionState.Buffered)]
    [InlineData(LiveSessionState.Seeking)]
    [InlineData(LiveSessionState.ChangingQuality)]
    [InlineData(LiveSessionState.Error)]
    [InlineData(LiveSessionState.Ended)]
    public void LiveSessionState_AllEightValues_Exist(LiveSessionState state)
    {
        state.Should().BeDefined();
    }

    [Fact]
    public void LiveSessionState_HasExactlyEightValues()
    {
        Enum.GetValues<LiveSessionState>().Length.Should().Be(8);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // LiveQuality
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LiveQuality_Constructs_AndJsonRoundTrips()
    {
        LiveQuality quality = new(
            Id: "1080p",
            Label: "1080p HD",
            Width: 1920,
            Height: 1080,
            Codec: VideoCodecType.H264,
            BitrateKbps: 4000,
            Encoder: "h264_nvenc",
            IsHardwareAccelerated: true,
            ExpectedSpeed: 1.8,
            CanRealtime: true
        );

        quality.Id.Should().Be("1080p");
        quality.Label.Should().Be("1080p HD");
        quality.Width.Should().Be(1920);
        quality.Height.Should().Be(1080);
        quality.Codec.Should().Be(VideoCodecType.H264);
        quality.BitrateKbps.Should().Be(4000);
        quality.Encoder.Should().Be("h264_nvenc");
        quality.IsHardwareAccelerated.Should().BeTrue();
        quality.ExpectedSpeed.Should().Be(1.8);
        quality.CanRealtime.Should().BeTrue();

        string json = JsonConvert.SerializeObject(quality);
        LiveQuality? deserialized = JsonConvert.DeserializeObject<LiveQuality>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(quality.Id);
        deserialized.Codec.Should().Be(quality.Codec);
        deserialized.IsHardwareAccelerated.Should().Be(quality.IsHardwareAccelerated);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SessionCreatedMessage
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SessionCreatedMessage_Constructs_AndJsonRoundTrips()
    {
        LiveQuality selected = new(
            Id: "720p",
            Label: "720p",
            Width: 1280,
            Height: 720,
            Codec: VideoCodecType.H264,
            BitrateKbps: 2500,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 2.5,
            CanRealtime: true
        );

        LiveQuality[] qualities = [selected];

        SessionCreatedMessage message = new(
            SessionId: "sess-abc123",
            DurationSeconds: 5400.0,
            AvailableQualities: qualities,
            SelectedQuality: selected,
            FirstSegmentUrl: "/segments/0.ts"
        );

        message.SessionId.Should().Be("sess-abc123");
        message.DurationSeconds.Should().Be(5400.0);
        message.AvailableQualities.Should().HaveCount(1);
        message.SelectedQuality.Id.Should().Be("720p");
        message.FirstSegmentUrl.Should().Be("/segments/0.ts");

        string json = JsonConvert.SerializeObject(message);
        SessionCreatedMessage? deserialized = JsonConvert.DeserializeObject<SessionCreatedMessage>(
            json
        );

        deserialized.Should().NotBeNull();
        deserialized!.SessionId.Should().Be("sess-abc123");
        deserialized.DurationSeconds.Should().Be(5400.0);
        deserialized.AvailableQualities.Should().HaveCount(1);
        deserialized.SelectedQuality.Id.Should().Be("720p");
        deserialized.FirstSegmentUrl.Should().Be("/segments/0.ts");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SegmentReadyMessage
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SegmentReadyMessage_Constructs_AndJsonRoundTrips()
    {
        SegmentReadyMessage message = new(
            Index: 3,
            StartTimeSeconds: 18.0,
            DurationSeconds: 6.0,
            RelativeUrl: "/segments/3.ts",
            SizeBytes: 204800L
        );

        message.Index.Should().Be(3);
        message.StartTimeSeconds.Should().Be(18.0);
        message.DurationSeconds.Should().Be(6.0);
        message.RelativeUrl.Should().Be("/segments/3.ts");
        message.SizeBytes.Should().Be(204800L);

        string json = JsonConvert.SerializeObject(message);
        SegmentReadyMessage? deserialized = JsonConvert.DeserializeObject<SegmentReadyMessage>(
            json
        );

        deserialized.Should().NotBeNull();
        deserialized!.Index.Should().Be(3);
        deserialized.StartTimeSeconds.Should().Be(18.0);
        deserialized.DurationSeconds.Should().Be(6.0);
        deserialized.RelativeUrl.Should().Be("/segments/3.ts");
        deserialized.SizeBytes.Should().Be(204800L);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SeekCompletedMessage
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SeekCompletedMessage_Constructs_AndJsonRoundTrips()
    {
        SeekCompletedMessage message = new(NewPositionSeconds: 120.5, FirstSegmentIndex: 20);

        message.NewPositionSeconds.Should().Be(120.5);
        message.FirstSegmentIndex.Should().Be(20);

        string json = JsonConvert.SerializeObject(message);
        SeekCompletedMessage? deserialized = JsonConvert.DeserializeObject<SeekCompletedMessage>(
            json
        );

        deserialized.Should().NotBeNull();
        deserialized!.NewPositionSeconds.Should().Be(120.5);
        deserialized.FirstSegmentIndex.Should().Be(20);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // QualityChangedMessage + QualityChangeReason
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(QualityChangeReason.UserRequested)]
    [InlineData(QualityChangeReason.AutoAdaptive)]
    [InlineData(QualityChangeReason.HardwareLimited)]
    [InlineData(QualityChangeReason.GpuFallbackToCpu)]
    public void QualityChangeReason_AllValues_Exist(QualityChangeReason reason)
    {
        reason.Should().BeDefined();
    }

    [Fact]
    public void QualityChangedMessage_Constructs_AndJsonRoundTrips()
    {
        LiveQuality newQuality = new(
            Id: "480p",
            Label: "480p",
            Width: 854,
            Height: 480,
            Codec: VideoCodecType.H265,
            BitrateKbps: 1200,
            Encoder: "libx265",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 3.1,
            CanRealtime: true
        );

        QualityChangedMessage message = new(
            NewQuality: newQuality,
            Reason: QualityChangeReason.AutoAdaptive
        );

        message.NewQuality.Id.Should().Be("480p");
        message.Reason.Should().Be(QualityChangeReason.AutoAdaptive);

        string json = JsonConvert.SerializeObject(message);
        QualityChangedMessage? deserialized = JsonConvert.DeserializeObject<QualityChangedMessage>(
            json
        );

        deserialized.Should().NotBeNull();
        deserialized!.NewQuality.Id.Should().Be("480p");
        deserialized.Reason.Should().Be(QualityChangeReason.AutoAdaptive);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TranscodeStateMessage
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TranscodeStateMessage_Constructs_AndJsonRoundTrips()
    {
        TranscodeStateMessage message = new(
            Speed: 1.4,
            BufferAheadSeconds: 30.0,
            State: LiveSessionState.Transcoding
        );

        message.Speed.Should().Be(1.4);
        message.BufferAheadSeconds.Should().Be(30.0);
        message.State.Should().Be(LiveSessionState.Transcoding);

        string json = JsonConvert.SerializeObject(message);
        TranscodeStateMessage? deserialized = JsonConvert.DeserializeObject<TranscodeStateMessage>(
            json
        );

        deserialized.Should().NotBeNull();
        deserialized!.Speed.Should().Be(1.4);
        deserialized.BufferAheadSeconds.Should().Be(30.0);
        deserialized.State.Should().Be(LiveSessionState.Transcoding);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TranscodeErrorMessage
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TranscodeErrorMessage_Constructs_AndJsonRoundTrips()
    {
        TranscodeErrorMessage message = new(
            Kind: EncodingErrorKind.HardwareFailure,
            Message: "NVENC session failed",
            Recoverable: true
        );

        message.Kind.Should().Be(EncodingErrorKind.HardwareFailure);
        message.Message.Should().Be("NVENC session failed");
        message.Recoverable.Should().BeTrue();

        string json = JsonConvert.SerializeObject(message);
        TranscodeErrorMessage? deserialized = JsonConvert.DeserializeObject<TranscodeErrorMessage>(
            json
        );

        deserialized.Should().NotBeNull();
        deserialized!.Kind.Should().Be(EncodingErrorKind.HardwareFailure);
        deserialized.Message.Should().Be("NVENC session failed");
        deserialized.Recoverable.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SessionEndedMessage + SessionEndReason
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SessionEndReason.ClientDisconnected)]
    [InlineData(SessionEndReason.Completed)]
    [InlineData(SessionEndReason.Error)]
    [InlineData(SessionEndReason.ServerShutdown)]
    public void SessionEndReason_AllValues_Exist(SessionEndReason reason)
    {
        reason.Should().BeDefined();
    }

    [Fact]
    public void SessionEndedMessage_Constructs_AndJsonRoundTrips()
    {
        SessionEndedMessage message = new(Reason: SessionEndReason.Completed);

        message.Reason.Should().Be(SessionEndReason.Completed);

        string json = JsonConvert.SerializeObject(message);
        SessionEndedMessage? deserialized = JsonConvert.DeserializeObject<SessionEndedMessage>(
            json
        );

        deserialized.Should().NotBeNull();
        deserialized!.Reason.Should().Be(SessionEndReason.Completed);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Client messages
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RequestSeekMessage_Constructs_AndJsonRoundTrips()
    {
        RequestSeekMessage message = new(PositionSeconds: 247.5);

        message.PositionSeconds.Should().Be(247.5);

        string json = JsonConvert.SerializeObject(message);
        RequestSeekMessage? deserialized = JsonConvert.DeserializeObject<RequestSeekMessage>(json);

        deserialized.Should().NotBeNull();
        deserialized!.PositionSeconds.Should().Be(247.5);
    }

    [Fact]
    public void RequestQualityMessage_WithId_JsonRoundTrips()
    {
        RequestQualityMessage message = new(QualityId: "1080p");

        message.QualityId.Should().Be("1080p");

        string json = JsonConvert.SerializeObject(message);
        RequestQualityMessage? deserialized = JsonConvert.DeserializeObject<RequestQualityMessage>(
            json
        );

        deserialized.Should().NotBeNull();
        deserialized!.QualityId.Should().Be("1080p");
    }

    [Fact]
    public void RequestQualityMessage_WithNull_JsonRoundTrips()
    {
        RequestQualityMessage message = new(QualityId: null);

        message.QualityId.Should().BeNull();

        string json = JsonConvert.SerializeObject(message);
        RequestQualityMessage? deserialized = JsonConvert.DeserializeObject<RequestQualityMessage>(
            json
        );

        deserialized.Should().NotBeNull();
        deserialized!.QualityId.Should().BeNull();
    }

    [Fact]
    public void ReportPositionMessage_Constructs_AndJsonRoundTrips()
    {
        ReportPositionMessage message = new(CurrentTimeSeconds: 83.25);

        message.CurrentTimeSeconds.Should().Be(83.25);

        string json = JsonConvert.SerializeObject(message);
        ReportPositionMessage? deserialized = JsonConvert.DeserializeObject<ReportPositionMessage>(
            json
        );

        deserialized.Should().NotBeNull();
        deserialized!.CurrentTimeSeconds.Should().Be(83.25);
    }
}
