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

using Newtonsoft.Json;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Encoder.LiveTranscode.Protocol;

namespace NoMercy.Tests.Encoder.LiveTranscode.Protocol;

public class ProtocolMessageTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // LiveSessionState
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: LiveSessionState.Starting)]
    [InlineData(data: LiveSessionState.Transcoding)]
    [InlineData(data: LiveSessionState.Buffering)]
    [InlineData(data: LiveSessionState.Buffered)]
    [InlineData(data: LiveSessionState.Seeking)]
    [InlineData(data: LiveSessionState.ChangingQuality)]
    [InlineData(data: LiveSessionState.Error)]
    [InlineData(data: LiveSessionState.Ended)]
    public void LiveSessionState_AllEightValues_Exist(LiveSessionState state)
    {
        state.Should().BeDefined();
    }

    [Fact]
    public void LiveSessionState_HasExactlyEightValues()
    {
        Enum.GetValues<LiveSessionState>().Length.Should().Be(expected: 8);
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

        quality.Id.Should().Be(expected: "1080p");
        quality.Label.Should().Be(expected: "1080p HD");
        quality.Width.Should().Be(expected: 1920);
        quality.Height.Should().Be(expected: 1080);
        quality.Codec.Should().Be(expected: VideoCodecType.H264);
        quality.BitrateKbps.Should().Be(expected: 4000);
        quality.Encoder.Should().Be(expected: "h264_nvenc");
        quality.IsHardwareAccelerated.Should().BeTrue();
        quality.ExpectedSpeed.Should().Be(expected: 1.8);
        quality.CanRealtime.Should().BeTrue();

        string json = JsonConvert.SerializeObject(value: quality);
        LiveQuality? deserialized = JsonConvert.DeserializeObject<LiveQuality>(value: json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(expected: quality.Id);
        deserialized.Codec.Should().Be(expected: quality.Codec);
        deserialized.IsHardwareAccelerated.Should().Be(expected: quality.IsHardwareAccelerated);
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

        message.SessionId.Should().Be(expected: "sess-abc123");
        message.DurationSeconds.Should().Be(expected: 5400.0);
        message.AvailableQualities.Should().HaveCount(expected: 1);
        message.SelectedQuality.Id.Should().Be(expected: "720p");
        message.FirstSegmentUrl.Should().Be(expected: "/segments/0.ts");

        string json = JsonConvert.SerializeObject(value: message);
        SessionCreatedMessage? deserialized = JsonConvert.DeserializeObject<SessionCreatedMessage>(
            value: json
        );

        deserialized.Should().NotBeNull();
        deserialized!.SessionId.Should().Be(expected: "sess-abc123");
        deserialized.DurationSeconds.Should().Be(expected: 5400.0);
        deserialized.AvailableQualities.Should().HaveCount(expected: 1);
        deserialized.SelectedQuality.Id.Should().Be(expected: "720p");
        deserialized.FirstSegmentUrl.Should().Be(expected: "/segments/0.ts");
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

        message.Index.Should().Be(expected: 3);
        message.StartTimeSeconds.Should().Be(expected: 18.0);
        message.DurationSeconds.Should().Be(expected: 6.0);
        message.RelativeUrl.Should().Be(expected: "/segments/3.ts");
        message.SizeBytes.Should().Be(expected: 204800L);

        string json = JsonConvert.SerializeObject(value: message);
        SegmentReadyMessage? deserialized = JsonConvert.DeserializeObject<SegmentReadyMessage>(
            value: json
        );

        deserialized.Should().NotBeNull();
        deserialized!.Index.Should().Be(expected: 3);
        deserialized.StartTimeSeconds.Should().Be(expected: 18.0);
        deserialized.DurationSeconds.Should().Be(expected: 6.0);
        deserialized.RelativeUrl.Should().Be(expected: "/segments/3.ts");
        deserialized.SizeBytes.Should().Be(expected: 204800L);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SeekCompletedMessage
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SeekCompletedMessage_Constructs_AndJsonRoundTrips()
    {
        SeekCompletedMessage message = new(NewPositionSeconds: 120.5, FirstSegmentIndex: 20);

        message.NewPositionSeconds.Should().Be(expected: 120.5);
        message.FirstSegmentIndex.Should().Be(expected: 20);

        string json = JsonConvert.SerializeObject(value: message);
        SeekCompletedMessage? deserialized = JsonConvert.DeserializeObject<SeekCompletedMessage>(
            value: json
        );

        deserialized.Should().NotBeNull();
        deserialized!.NewPositionSeconds.Should().Be(expected: 120.5);
        deserialized.FirstSegmentIndex.Should().Be(expected: 20);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // QualityChangedMessage + QualityChangeReason
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: QualityChangeReason.UserRequested)]
    [InlineData(data: QualityChangeReason.AutoAdaptive)]
    [InlineData(data: QualityChangeReason.HardwareLimited)]
    [InlineData(data: QualityChangeReason.GpuFallbackToCpu)]
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

        message.NewQuality.Id.Should().Be(expected: "480p");
        message.Reason.Should().Be(expected: QualityChangeReason.AutoAdaptive);

        string json = JsonConvert.SerializeObject(value: message);
        QualityChangedMessage? deserialized = JsonConvert.DeserializeObject<QualityChangedMessage>(
            value: json
        );

        deserialized.Should().NotBeNull();
        deserialized!.NewQuality.Id.Should().Be(expected: "480p");
        deserialized.Reason.Should().Be(expected: QualityChangeReason.AutoAdaptive);
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

        message.Speed.Should().Be(expected: 1.4);
        message.BufferAheadSeconds.Should().Be(expected: 30.0);
        message.State.Should().Be(expected: LiveSessionState.Transcoding);

        string json = JsonConvert.SerializeObject(value: message);
        TranscodeStateMessage? deserialized = JsonConvert.DeserializeObject<TranscodeStateMessage>(
            value: json
        );

        deserialized.Should().NotBeNull();
        deserialized!.Speed.Should().Be(expected: 1.4);
        deserialized.BufferAheadSeconds.Should().Be(expected: 30.0);
        deserialized.State.Should().Be(expected: LiveSessionState.Transcoding);
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

        message.Kind.Should().Be(expected: EncodingErrorKind.HardwareFailure);
        message.Message.Should().Be(expected: "NVENC session failed");
        message.Recoverable.Should().BeTrue();

        string json = JsonConvert.SerializeObject(value: message);
        TranscodeErrorMessage? deserialized = JsonConvert.DeserializeObject<TranscodeErrorMessage>(
            value: json
        );

        deserialized.Should().NotBeNull();
        deserialized!.Kind.Should().Be(expected: EncodingErrorKind.HardwareFailure);
        deserialized.Message.Should().Be(expected: "NVENC session failed");
        deserialized.Recoverable.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SessionEndedMessage + SessionEndReason
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: SessionEndReason.ClientDisconnected)]
    [InlineData(data: SessionEndReason.Completed)]
    [InlineData(data: SessionEndReason.Error)]
    [InlineData(data: SessionEndReason.ServerShutdown)]
    public void SessionEndReason_AllValues_Exist(SessionEndReason reason)
    {
        reason.Should().BeDefined();
    }

    [Fact]
    public void SessionEndedMessage_Constructs_AndJsonRoundTrips()
    {
        SessionEndedMessage message = new(Reason: SessionEndReason.Completed);

        message.Reason.Should().Be(expected: SessionEndReason.Completed);

        string json = JsonConvert.SerializeObject(value: message);
        SessionEndedMessage? deserialized = JsonConvert.DeserializeObject<SessionEndedMessage>(
            value: json
        );

        deserialized.Should().NotBeNull();
        deserialized!.Reason.Should().Be(expected: SessionEndReason.Completed);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Client messages
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RequestSeekMessage_Constructs_AndJsonRoundTrips()
    {
        RequestSeekMessage message = new(PositionSeconds: 247.5);

        message.PositionSeconds.Should().Be(expected: 247.5);

        string json = JsonConvert.SerializeObject(value: message);
        RequestSeekMessage? deserialized = JsonConvert.DeserializeObject<RequestSeekMessage>(value: json);

        deserialized.Should().NotBeNull();
        deserialized!.PositionSeconds.Should().Be(expected: 247.5);
    }

    [Fact]
    public void RequestQualityMessage_WithId_JsonRoundTrips()
    {
        RequestQualityMessage message = new(QualityId: "1080p");

        message.QualityId.Should().Be(expected: "1080p");

        string json = JsonConvert.SerializeObject(value: message);
        RequestQualityMessage? deserialized = JsonConvert.DeserializeObject<RequestQualityMessage>(
            value: json
        );

        deserialized.Should().NotBeNull();
        deserialized!.QualityId.Should().Be(expected: "1080p");
    }

    [Fact]
    public void RequestQualityMessage_WithNull_JsonRoundTrips()
    {
        RequestQualityMessage message = new(QualityId: null);

        message.QualityId.Should().BeNull();

        string json = JsonConvert.SerializeObject(value: message);
        RequestQualityMessage? deserialized = JsonConvert.DeserializeObject<RequestQualityMessage>(
            value: json
        );

        deserialized.Should().NotBeNull();
        deserialized!.QualityId.Should().BeNull();
    }

    [Fact]
    public void ReportPositionMessage_Constructs_AndJsonRoundTrips()
    {
        ReportPositionMessage message = new(CurrentTimeSeconds: 83.25);

        message.CurrentTimeSeconds.Should().Be(expected: 83.25);

        string json = JsonConvert.SerializeObject(value: message);
        ReportPositionMessage? deserialized = JsonConvert.DeserializeObject<ReportPositionMessage>(
            value: json
        );

        deserialized.Should().NotBeNull();
        deserialized!.CurrentTimeSeconds.Should().Be(expected: 83.25);
    }
}
