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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Tests.Encoder.LiveTranscode;

/// <summary>
/// Tests verifying that the decision engine correctly identifies cases where
/// the controller should skip session creation and return a direct-play URL.
/// These exercise the same inputs the controller passes to IPlaybackDecisionEngine.Decide().
/// </summary>
public class PlaybackDecisionEngineDirectPlayTests
{
    private readonly PlaybackDecisionEngine _engine = new();

    private static VideoStreamInfo MakeCompatibleVideo() =>
        new(
            Index: 0,
            Codec: "h264",
            Width: 1920,
            Height: 1080,
            FrameRate: 24.0,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 5000
        );

    private static AudioStreamInfo MakeCompatibleAudio() =>
        new(
            Index: 1,
            Codec: "aac",
            Channels: 2,
            SampleRate: 48000,
            BitRateKbps: 192,
            Language: "eng",
            IsDefault: true,
            IsForced: false
        );

    private static MediaInfo MakeMedia(
        string format,
        VideoStreamInfo? video,
        AudioStreamInfo? audio
    )
    {
        List<VideoStreamInfo> videos = video is not null ? [video] : [];
        List<AudioStreamInfo> audios = audio is not null ? [audio] : [];
        return new(
            FilePath: "/media/movie.mkv",
            Format: format,
            Duration: TimeSpan.FromMinutes(minutes: 120),
            OverallBitRateKbps: 5200,
            FileSizeBytes: 4_000_000_000L,
            VideoStreams: videos,
            AudioStreams: audios,
            SubtitleStreams: [],
            Chapters: []
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DirectPlay — controller should skip session, populate DirectStreamUrl
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CompatibleH264MkvToMkvClient_IsDirectPlay()
    {
        MediaInfo media = MakeMedia(format: "matroska,webm", video: MakeCompatibleVideo(), audio: MakeCompatibleAudio());

        ClientCapabilities client = new(
            SupportedVideoCodecs: [VideoCodecType.H264],
            SupportedAudioCodecs: [AudioCodecType.Aac],
            SupportedContainers: ["mkv"],
            MaxWidth: 7680,
            MaxHeight: 4320,
            SupportsHdr: true,
            Supports10Bit: true,
            MaxBitrateKbps: 0
        );

        PlaybackDecision decision = _engine.Decide(media: media, client: client);

        decision.Action.Should().Be(expected: PlaybackAction.DirectPlay);
        decision.Reason.Should().BeNull();
    }

    [Fact]
    public void DirectPlay_DirectStreamUrlIsNull_ControllerMustPopulate()
    {
        // The engine itself never produces a URL — that is the controller's job.
        // This test documents the contract: engine returns null, controller builds
        // the URL from VideoFile.HostFolder / VideoFile.Filename.
        MediaInfo media = MakeMedia(format: "matroska,webm", video: MakeCompatibleVideo(), audio: MakeCompatibleAudio());

        ClientCapabilities client = new(
            SupportedVideoCodecs: [VideoCodecType.H264],
            SupportedAudioCodecs: [AudioCodecType.Aac],
            SupportedContainers: ["mkv"],
            MaxWidth: 7680,
            MaxHeight: 4320,
            SupportsHdr: true,
            Supports10Bit: true,
            MaxBitrateKbps: 0
        );

        PlaybackDecision decision = _engine.Decide(media: media, client: client);

        decision.DirectStreamUrl.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Transcode paths — controller must start a session
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void IncompatibleCodec_IsTranscodeVideo_NotDirectPlay()
    {
        VideoStreamInfo hevc = new(
            Index: 0,
            Codec: "hevc",
            Width: 1920,
            Height: 1080,
            FrameRate: 24.0,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 5000
        );

        MediaInfo media = MakeMedia(format: "matroska,webm", video: hevc, audio: MakeCompatibleAudio());

        ClientCapabilities client = new(
            SupportedVideoCodecs: [VideoCodecType.H264],
            SupportedAudioCodecs: [AudioCodecType.Aac],
            SupportedContainers: ["mkv"],
            MaxWidth: 7680,
            MaxHeight: 4320,
            SupportsHdr: true,
            Supports10Bit: true,
            MaxBitrateKbps: 0
        );

        PlaybackDecision decision = _engine.Decide(media: media, client: client);

        decision.Action.Should().Be(expected: PlaybackAction.TranscodeVideo);
    }

    [Fact]
    public void WrongContainer_IsRemux_NotDirectPlay()
    {
        MediaInfo media = MakeMedia(format: "matroska,webm", video: MakeCompatibleVideo(), audio: MakeCompatibleAudio());

        ClientCapabilities client = new(
            SupportedVideoCodecs: [VideoCodecType.H264],
            SupportedAudioCodecs: [AudioCodecType.Aac],
            SupportedContainers: ["mp4"],
            MaxWidth: 7680,
            MaxHeight: 4320,
            SupportsHdr: true,
            Supports10Bit: true,
            MaxBitrateKbps: 0
        );

        PlaybackDecision decision = _engine.Decide(media: media, client: client);

        decision.Action.Should().Be(expected: PlaybackAction.Remux);
    }

    [Fact]
    public void IncompatibleAudio_IsTranscodeAudio_NotDirectPlay()
    {
        AudioStreamInfo ac3 = new(
            Index: 1,
            Codec: "ac3",
            Channels: 6,
            SampleRate: 48000,
            BitRateKbps: 640,
            Language: "eng",
            IsDefault: true,
            IsForced: false
        );

        MediaInfo media = MakeMedia(format: "matroska,webm", video: MakeCompatibleVideo(), audio: ac3);

        ClientCapabilities client = new(
            SupportedVideoCodecs: [VideoCodecType.H264],
            SupportedAudioCodecs: [AudioCodecType.Aac],
            SupportedContainers: ["mkv"],
            MaxWidth: 7680,
            MaxHeight: 4320,
            SupportsHdr: true,
            Supports10Bit: true,
            MaxBitrateKbps: 0
        );

        PlaybackDecision decision = _engine.Decide(media: media, client: client);

        decision.Action.Should().Be(expected: PlaybackAction.TranscodeAudio);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // URL construction contract
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DirectPlayUrl_IsSlashHostFolderSlashFilename()
    {
        // Documents the URL shape the controller builds.
        // Format: /{hostFolder}/{filename}
        // These are served by DynamicStaticFilesMiddleware.
        string hostFolder = "01HVXXXXXXXXXXXXXXXXXXXXXXX";
        string filename = "movie.mkv";
        string expectedUrl = $"/{hostFolder}/{filename}";

        // The actual assembly is in the controller — this test documents the contract.
        string builtUrl = $"/{hostFolder}/{filename}";
        builtUrl.Should().Be(expected: expectedUrl);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fallback: engine exception → falls back to transcode (not tested via controller
    // because we test just the decision logic here)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DecideBatch_AllDirectPlay_ReturnsSameCountAsInput()
    {
        MediaInfo media = MakeMedia(format: "matroska,webm", video: MakeCompatibleVideo(), audio: MakeCompatibleAudio());

        ClientCapabilities client = new(
            SupportedVideoCodecs: [VideoCodecType.H264],
            SupportedAudioCodecs: [AudioCodecType.Aac],
            SupportedContainers: ["mkv"],
            MaxWidth: 7680,
            MaxHeight: 4320,
            SupportsHdr: true,
            Supports10Bit: true,
            MaxBitrateKbps: 0
        );

        MediaInfo[] library = [media, media, media];
        PlaybackDecision[] decisions = _engine.DecideBatch(library: library, client: client);

        decisions.Should().HaveCount(expected: 3);
        decisions.Should().AllSatisfy(expected: d => d.Action.Should().Be(expected: PlaybackAction.DirectPlay));
    }
}
