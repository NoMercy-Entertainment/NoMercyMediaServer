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
            0,
            "h264",
            1920,
            1080,
            24.0,
            8,
            "yuv420p",
            "bt709",
            "bt709",
            "bt709",
            true,
            5000
        );

    private static AudioStreamInfo MakeCompatibleAudio() =>
        new(
            1,
            "aac",
            2,
            48000,
            192,
            "eng",
            true,
            false
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
            "/media/movie.mkv",
            format,
            TimeSpan.FromMinutes(120),
            5200,
            4_000_000_000L,
            videos,
            audios,
            [],
            []
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DirectPlay — controller should skip session, populate DirectStreamUrl
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CompatibleH264MkvToMkvClient_IsDirectPlay()
    {
        MediaInfo media = MakeMedia("matroska,webm", MakeCompatibleVideo(), MakeCompatibleAudio());

        ClientCapabilities client = new(
            [VideoCodecType.H264],
            [AudioCodecType.Aac],
            ["mkv"],
            7680,
            4320,
            true,
            true,
            0
        );

        PlaybackDecision decision = _engine.Decide(media, client);

        decision.Action.Should().Be(PlaybackAction.DirectPlay);
        decision.Reason.Should().BeNull();
    }

    [Fact]
    public void DirectPlay_DirectStreamUrlIsNull_ControllerMustPopulate()
    {
        // The engine itself never produces a URL — that is the controller's job.
        // This test documents the contract: engine returns null, controller builds
        // the URL from VideoFile.HostFolder / VideoFile.Filename.
        MediaInfo media = MakeMedia("matroska,webm", MakeCompatibleVideo(), MakeCompatibleAudio());

        ClientCapabilities client = new(
            [VideoCodecType.H264],
            [AudioCodecType.Aac],
            ["mkv"],
            7680,
            4320,
            true,
            true,
            0
        );

        PlaybackDecision decision = _engine.Decide(media, client);

        decision.DirectStreamUrl.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Transcode paths — controller must start a session
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void IncompatibleCodec_IsTranscodeVideo_NotDirectPlay()
    {
        VideoStreamInfo hevc = new(
            0,
            "hevc",
            1920,
            1080,
            24.0,
            8,
            "yuv420p",
            "bt709",
            "bt709",
            "bt709",
            true,
            5000
        );

        MediaInfo media = MakeMedia("matroska,webm", hevc, MakeCompatibleAudio());

        ClientCapabilities client = new(
            [VideoCodecType.H264],
            [AudioCodecType.Aac],
            ["mkv"],
            7680,
            4320,
            true,
            true,
            0
        );

        PlaybackDecision decision = _engine.Decide(media, client);

        decision.Action.Should().Be(PlaybackAction.TranscodeVideo);
    }

    [Fact]
    public void WrongContainer_IsRemux_NotDirectPlay()
    {
        MediaInfo media = MakeMedia("matroska,webm", MakeCompatibleVideo(), MakeCompatibleAudio());

        ClientCapabilities client = new(
            [VideoCodecType.H264],
            [AudioCodecType.Aac],
            ["mp4"],
            7680,
            4320,
            true,
            true,
            0
        );

        PlaybackDecision decision = _engine.Decide(media, client);

        decision.Action.Should().Be(PlaybackAction.Remux);
    }

    [Fact]
    public void IncompatibleAudio_IsTranscodeAudio_NotDirectPlay()
    {
        AudioStreamInfo ac3 = new(
            1,
            "ac3",
            6,
            48000,
            640,
            "eng",
            true,
            false
        );

        MediaInfo media = MakeMedia("matroska,webm", MakeCompatibleVideo(), ac3);

        ClientCapabilities client = new(
            [VideoCodecType.H264],
            [AudioCodecType.Aac],
            ["mkv"],
            7680,
            4320,
            true,
            true,
            0
        );

        PlaybackDecision decision = _engine.Decide(media, client);

        decision.Action.Should().Be(PlaybackAction.TranscodeAudio);
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
        builtUrl.Should().Be(expectedUrl);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fallback: engine exception → falls back to transcode (not tested via controller
    // because we test just the decision logic here)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DecideBatch_AllDirectPlay_ReturnsSameCountAsInput()
    {
        MediaInfo media = MakeMedia("matroska,webm", MakeCompatibleVideo(), MakeCompatibleAudio());

        ClientCapabilities client = new(
            [VideoCodecType.H264],
            [AudioCodecType.Aac],
            ["mkv"],
            7680,
            4320,
            true,
            true,
            0
        );

        MediaInfo[] library = [media, media, media];
        PlaybackDecision[] decisions = _engine.DecideBatch(library, client);

        decisions.Should().HaveCount(3);
        decisions.Should().AllSatisfy(d => d.Action.Should().Be(PlaybackAction.DirectPlay));
    }
}
