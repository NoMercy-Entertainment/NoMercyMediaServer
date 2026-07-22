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

using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Tests.Encoder.Output;

public class PlaylistGeneratorTests
{
    private const string MediaTitle = "Movie.Name.NoMercy";

    private string Generate(OutputPlan plan)
    {
        // GenerateMasterPlaylist now skips variants whose measured bandwidth
        // is zero (the analyzer's signal for "playlist / segments missing on
        // disk") to keep dead variants out of the published master. Tests
        // that build a synthetic OutputPlan need to seed non-zero metrics so
        // the variant rows still render — otherwise the master is empty and
        // every codec-tag / structure assertion fails.
        // Metrics are keyed by each variant's resolved playlist path (NOT
        // MapLabel — every rung re-plans as "[v0]", so MapLabel keys collide and
        // collapse the ladder onto one shared BANDWIDTH). Mirror the production
        // keying so the lookup in GenerateMasterPlaylist resolves.
        Dictionary<string, VariantMetrics> videoMetrics = plan.VideoOutputs.ToDictionary(
            keySelector: VideoVariantKey,
            elementSelector: _ => new VariantMetrics(PeakBandwidth: 5_000_000, AverageBandwidth: 4_500_000)
        );

        Dictionary<string, VariantMetrics> audioMetrics = plan.AudioOutputs.ToDictionary(
            keySelector: AudioVariantKey,
            elementSelector: _ => new VariantMetrics(PeakBandwidth: 192_000, AverageBandwidth: 180_000)
        );

        PlaylistGenerator generator = new();
        return generator.GenerateMasterPlaylist(plan: plan, mediaTitle: MediaTitle, videoMetrics: videoMetrics, audioMetrics: audioMetrics);
    }

    // The metrics-dict key a variant is stored/looked-up under: its resolved
    // playlist path, unique per resolution/HDR. Mirrors HlsOutputStrategy +
    // PlaylistGenerator exactly.
    private static string VideoVariantKey(VideoOutputPlan video) =>
        TemplateResolver.Resolve(
            template: video.PlaylistNameTemplate,
            values: TemplateResolver.VideoTokens(width: video.Width, height: video.Height, isHdrOutput: video.IsHdrOutput)
        );

    private static string AudioVariantKey(AudioOutputPlan audio) =>
        TemplateResolver.Resolve(
            template: audio.PlaylistNameTemplate,
            values: TemplateResolver.AudioTokens(language: audio.Language ?? "und", codecName: audio.CodecToken, channels: audio.Channels)
        );

    [Fact]
    public void MasterPlaylist_ContainsExtm3u()
    {
        string playlist = Generate(plan: CreatePlan());

        playlist.Should().StartWith(expected: "#EXTM3U");
        // Version is computed from active features. Basic mpegts with no subtitles
        // group, no fMP4, and no chapter date-ranges requires version 3.
        playlist.Should().Contain(expected: "#EXT-X-VERSION:3");
    }

    [Fact]
    public void MasterPlaylist_ContainsIndependentSegments()
    {
        string playlist = Generate(plan: CreatePlan());

        playlist.Should().Contain(expected: "#EXT-X-INDEPENDENT-SEGMENTS");
    }

    [Fact]
    public void MasterPlaylist_ContainsVideoVariants()
    {
        string playlist = Generate(plan: CreateMultiResPlan());

        playlist.Should().Contain(expected: "RESOLUTION=1920x1080");
        playlist.Should().Contain(expected: "RESOLUTION=1280x720");
        playlist.Should().Contain(expected: "video_1920x1080_SDR/video_1920x1080_SDR.m3u8");
        playlist.Should().Contain(expected: "video_1280x720_SDR/video_1280x720_SDR.m3u8");
    }

    [Fact]
    public void MasterPlaylist_H264_CorrectCodecTag()
    {
        string playlist = Generate(plan: CreatePlan());

        playlist.Should().Contain(expected: "avc1.640028");
    }

    [Fact]
    public void MasterPlaylist_Hevc_CorrectCodecTag()
    {
        string playlist = Generate(plan: CreatePlan(encoderName: "hevc_nvenc"));

        playlist.Should().Contain(expected: "hvc1.");
    }

    [Fact]
    public void MasterPlaylist_Av1_10bit_CorrectCodecTag()
    {
        // Plan fixture declares Level="4.0" (Av1 spec table A.1 → index 8)
        // and tenBit=true → expect av01.0.08M.10. Phase 4.17 introduced the
        // spec-accurate HlsCodecsStringBuilder which derives the level index
        // from the plan instead of hard-coding 5.3 (index 15) like the legacy
        // generator did.
        string playlist = Generate(plan: CreatePlan(encoderName: "libsvtav1", tenBit: true));

        playlist.Should().Contain(expected: "av01.0.08M.10");
    }

    [Fact]
    public void MasterPlaylist_CopyVideo_OmitsCodecsInsteadOfLying()
    {
        // "copy" is a passthrough — the real codec could be anything. The
        // master must never advertise avc1 (the old H.264-fallback bug) for
        // a copy-mode variant; CODECS should list only the known audio codec.
        string playlist = Generate(plan: CreatePlan(encoderName: "copy"));

        playlist.Should().NotContain(unexpected: "avc1.");
        playlist.Should().Contain(expected: "CODECS=\"mp4a.40.2\"");
    }

    [Fact]
    public void MasterPlaylist_AudioGroup_Present()
    {
        string playlist = Generate(plan: CreatePlan());

        playlist.Should().Contain(expected: "#EXT-X-MEDIA:TYPE=AUDIO");
        playlist.Should().Contain(expected: "GROUP-ID=\"audio_aac\"");
        playlist.Should().Contain(expected: "LANGUAGE=\"eng\"");
    }

    [Fact]
    public void MasterPlaylist_AacAudio_Mp4aCodecTag()
    {
        string playlist = Generate(plan: CreatePlan());

        playlist.Should().Contain(expected: "mp4a.40.2");
    }

    // ── ComputeMasterVersion (internal helper) ─────────────────────────────

    [Theory]
    [InlineData(data: [false, false, false, 3])] // mpegts baseline → v3
    [InlineData(data: [true, false, false, 6])] // subs → v6
    [InlineData(data: [false, true, false, 7])] // fmp4 → v7
    [InlineData(data: [true, true, false, 7])] // subs + fmp4 → v7
    [InlineData(data: [false, false, true, 8])] // chapter date-ranges → v8
    [InlineData(data: [true, true, true, 8])] // everything → v8
    public void ComputeMasterVersion_ReturnsCorrectMinVersion(
        bool hasSubsGroup,
        bool hasFmp4,
        bool hasChapterDateRanges,
        int expectedVersion
    )
    {
        // Pin the EXT-X-VERSION ladder. Wrong version makes players fall
        // back to a more conservative interpretation or refuse to parse.
        // Reflection because the method is internal — keep the contract
        // testable without exposing it publicly to the rest of the encoder.
        int actual = (int)
            typeof(PlaylistGenerator)
                .GetMethod(
                    name: "ComputeMasterVersion",
                    bindingAttr: System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
                )!
                .Invoke(obj: null, parameters: [hasSubsGroup, hasFmp4, hasChapterDateRanges])!;

        actual.Should().Be(expected: expectedVersion);
    }

    [Fact]
    public void MasterPlaylist_Fmp4SegmentType_BumpsVersionTo7()
    {
        // fMP4 requires v7 minimum per HLS spec.
        OutputPlan plan = CreatePlan() with
        {
            HlsOptions = new() { SegmentType = "fmp4" },
        };

        string playlist = Generate(plan: plan);

        playlist.Should().Contain(expected: "#EXT-X-VERSION:7");
        // fMP4 forces INDEPENDENT-SEGMENTS regardless of option.
        playlist.Should().Contain(expected: "#EXT-X-INDEPENDENT-SEGMENTS");
    }

    [Fact]
    public void MasterPlaylist_AudioCopyAction_Included()
    {
        // Copy is a valid audio action that still produces a sidecar HLS
        // group — the master MUST list it the same as a transcoded variant.
        OutputPlan plan = CreatePlan() with
        {
            AudioOutputs = [new(EncoderName: "aac", BitrateKbps: 0, Channels: 2, SampleRate: 48000, Action: StreamAction.Copy, Language: "eng", MapLabel: "0:a:0")],
        };

        string playlist = Generate(plan: plan);

        playlist.Should().Contain(expected: "#EXT-X-MEDIA:TYPE=AUDIO");
        playlist.Should().Contain(expected: "LANGUAGE=\"eng\"");
    }

    [Fact]
    public void MasterPlaylist_AudioWithZeroBandwidth_IsSkipped()
    {
        // The analyzer reports 0 bandwidth when segments are missing on
        // disk. Listing those variants in the master makes hls.js / VLC
        // bail on the first fetch.
        PlaylistGenerator generator = new();
        OutputPlan plan = CreatePlan();
        Dictionary<string, VariantMetrics> vidMetrics = new()
        {
            [key: VideoVariantKey(video: plan.VideoOutputs[0])] = new(PeakBandwidth: 5_000_000, AverageBandwidth: 3_500_000),
        };
        Dictionary<string, VariantMetrics> audMetrics = new()
        {
            [key: AudioVariantKey(audio: plan.AudioOutputs[0])] = new(PeakBandwidth: 0, AverageBandwidth: 0),
        };

        string playlist = generator.GenerateMasterPlaylist(
            plan: plan,
            mediaTitle: MediaTitle,
            videoMetrics: vidMetrics,
            audioMetrics: audMetrics
        );

        playlist.Should().NotContain(unexpected: "#EXT-X-MEDIA:TYPE=AUDIO");
    }

    [Fact]
    public void MasterPlaylist_VideoWithZeroBandwidth_IsSkipped()
    {
        // Same reason for video — a missing variant playlist must never
        // ship in the master.
        PlaylistGenerator generator = new();
        OutputPlan plan = CreatePlan();
        Dictionary<string, VariantMetrics> vidMetrics = new()
        {
            [key: VideoVariantKey(video: plan.VideoOutputs[0])] = new(PeakBandwidth: 0, AverageBandwidth: 0),
        };
        Dictionary<string, VariantMetrics> audMetrics = new()
        {
            [key: AudioVariantKey(audio: plan.AudioOutputs[0])] = new(PeakBandwidth: 192_000, AverageBandwidth: 180_000),
        };

        string playlist = generator.GenerateMasterPlaylist(
            plan: plan,
            mediaTitle: MediaTitle,
            videoMetrics: vidMetrics,
            audioMetrics: audMetrics
        );

        playlist.Should().NotContain(unexpected: "RESOLUTION=");
    }

    [Fact]
    public void MasterPlaylist_OpusAudio_CodecTag()
    {
        OutputPlan plan = CreatePlan() with
        {
            AudioOutputs = [new(EncoderName: "libopus", BitrateKbps: 128, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0")],
        };

        string playlist = Generate(plan: plan);

        playlist.Should().Contain(expected: "opus");
    }

    [Fact]
    public void MasterPlaylist_Eac3Audio_CodecTag()
    {
        OutputPlan plan = CreatePlan() with
        {
            AudioOutputs = [new(EncoderName: "eac3", BitrateKbps: 384, Channels: 6, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0")],
        };

        string playlist = Generate(plan: plan);

        playlist.Should().Contain(expected: "ec-3");
    }

    [Fact]
    public void MasterPlaylist_MeasuredBandwidth_UsedWhenProvided()
    {
        PlaylistGenerator generator = new();
        OutputPlan plan = CreatePlan();
        Dictionary<string, VariantMetrics> vidMetrics = new()
        {
            [key: VideoVariantKey(video: plan.VideoOutputs[0])] = new(PeakBandwidth: 5_000_000, AverageBandwidth: 3_500_000),
        };
        Dictionary<string, VariantMetrics> audMetrics = new()
        {
            [key: AudioVariantKey(audio: plan.AudioOutputs[0])] = new(PeakBandwidth: 256_000, AverageBandwidth: 192_000),
        };

        string playlist = generator.GenerateMasterPlaylist(
            plan: plan,
            mediaTitle: MediaTitle,
            videoMetrics: vidMetrics,
            audioMetrics: audMetrics
        );

        playlist.Should().Contain(expected: "BANDWIDTH=5256000");
        playlist.Should().Contain(expected: "AVERAGE-BANDWIDTH=3692000");
    }

    [Fact]
    public void MasterPlaylist_VariantsSharingMapLabel_EachGetTheirOwnBandwidth()
    {
        // Regression for the identical-BANDWIDTH bug: every rung re-plans in its
        // own bundle as MapLabel "[v0]", so two variants can carry the SAME
        // MapLabel at different resolutions. Metrics are keyed by the resolved
        // playlist PATH, so each variant must still advertise its OWN measured
        // bandwidth — not one value shared across the whole ladder (which is what
        // MapLabel keying produced).
        OutputPlan plan = CreateMultiResPlan() with
        { };
        plan = plan with
        {
            VideoOutputs =
            [
                plan.VideoOutputs[0] with
                {
                    MapLabel = "[v0]",
                }, // 1080p
                plan.VideoOutputs[1] with
                {
                    MapLabel = "[v0]",
                }, // 720p, SAME label
            ],
        };

        Dictionary<string, VariantMetrics> vidMetrics = new()
        {
            [key: VideoVariantKey(video: plan.VideoOutputs[0])] = new(PeakBandwidth: 8_000_000, AverageBandwidth: 6_000_000),
            [key: VideoVariantKey(video: plan.VideoOutputs[1])] = new(PeakBandwidth: 3_000_000, AverageBandwidth: 2_400_000),
        };
        Dictionary<string, VariantMetrics> audMetrics = new()
        {
            [key: AudioVariantKey(audio: plan.AudioOutputs[0])] = new(PeakBandwidth: 192_000, AverageBandwidth: 180_000),
        };

        string playlist = new PlaylistGenerator().GenerateMasterPlaylist(
            plan: plan,
            mediaTitle: MediaTitle,
            videoMetrics: vidMetrics,
            audioMetrics: audMetrics
        );

        // Each variant advertises its own bandwidth (video + audio), proving the
        // shared-MapLabel collision no longer collapses the ladder.
        playlist.Should().Contain(expected: "BANDWIDTH=8192000");
        playlist.Should().Contain(expected: "BANDWIDTH=3192000");
    }

    private static OutputPlan CreatePlan(string encoderName = "libx264", bool tenBit = false)
    {
        return new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: encoderName,
                    Crf: 23,
                    BitrateKbps: 8000,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
                    TenBit: tenBit,
                    PixelFormat: tenBit ? "yuv420p10le" : "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs: [new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null
        );
    }

    private static OutputPlan CreateMultiResPlan()
    {
        return new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 8000,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new()
                ),
                new(
                    Width: 1280,
                    Height: 720,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 4000,
                    Preset: "medium",
                    Profile: "high",
                    Level: "3.1",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v1]",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs: [new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null
        );
    }
}
