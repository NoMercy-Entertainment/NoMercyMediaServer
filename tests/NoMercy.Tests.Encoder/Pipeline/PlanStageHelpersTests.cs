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
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline;

/// <summary>
/// PlanStageHelpers expands a profile's stream outputs — manual-ladder
/// rungs into per-rung VideoOutput entries, container → output-format
/// mapping. The expansion contract is what PlanStage relies on to emit a
/// stable rung-by-rung plan.
/// </summary>
public class PlanStageHelpersTests
{
    private static VideoOutput RefVideo() =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            RateControl: RateControlMode.Vbr,
            Crf: 0,
            BitrateKbps: 4000,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: "medium",
            CodecProfile: CodecProfile.Main,
            Level: "4.0",
            Tune: null,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            KeyframeIntervalSeconds: 2,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: "video/{w}x{h}",
            PlaylistNameTemplate: "video/{w}x{h}/playlist"
        );

    private static EncodingProfile Profile(VideoOutput? video, LadderConfig? ladder) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "test",
            Container: Container.HlsFmp4,
            Video: video,
            Audio: [],
            Subtitles: [],
            Ladder: ladder
        );

    // ── EnumerateVideo ──────────────────────────────────────────────────────

    [Fact]
    public void EnumerateVideo_NoVideoNoLadder_ReturnsEmpty()
    {
        EncodingProfile profile = Profile(video: null, ladder: null);
        PlanStageHelpers.EnumerateVideo(profile: profile).Should().BeEmpty();
    }

    [Fact]
    public void EnumerateVideo_VideoNoLadder_ReturnsSingleEntry()
    {
        EncodingProfile profile = Profile(video: RefVideo(), ladder: null);
        VideoOutput[] result = PlanStageHelpers.EnumerateVideo(profile: profile);

        result.Should().ContainSingle();
        result[0].Width.Should().Be(expected: 1920);
    }

    [Fact]
    public void EnumerateVideo_ManualLadder_MaterialisesEachRung()
    {
        EncodingProfile profile = Profile(
            video: RefVideo(),
            ladder: new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new(Width: 854, Height: 480, Codec: VideoCodecType.H264, BitrateKbps: 800, MaxBitrateKbps: 1000, BufferSizeKbps: 2000, Framerate: 24),
                    new(Width: 1280, Height: 720, Codec: VideoCodecType.H264, BitrateKbps: 2000, MaxBitrateKbps: 2400, BufferSizeKbps: 4000, Framerate: 24),
                    new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 4000, MaxBitrateKbps: 4800, BufferSizeKbps: 8000, Framerate: 24),
                ],
            }
        );
        VideoOutput[] result = PlanStageHelpers.EnumerateVideo(profile: profile);

        result.Should().HaveCount(expected: 3);
        result[0].Width.Should().Be(expected: 854);
        result[0].BitrateKbps.Should().Be(expected: 800);
        result[1].Width.Should().Be(expected: 1280);
        result[2].Width.Should().Be(expected: 1920);
    }

    [Fact]
    public void EnumerateVideo_RungInheritsReferenceFields()
    {
        EncodingProfile profile = Profile(
            video: RefVideo(),
            ladder: new()
            {
                Mode = LadderMode.Manual,
                Rungs = [new(Width: 1280, Height: 720, Codec: VideoCodecType.H264, BitrateKbps: 2000, MaxBitrateKbps: 0, BufferSizeKbps: 0, Framerate: 24)],
            }
        );
        VideoOutput[] result = PlanStageHelpers.EnumerateVideo(profile: profile);

        // Preset / pixel format / keyframe interval / tune all come from
        // the reference video, not the rung.
        result[0].Preset.Should().Be(expected: "medium");
        result[0].PixelFormat.Should().Be(expected: "yuv420p");
        result[0].KeyframeIntervalSeconds.Should().Be(expected: 2);
    }

    [Fact]
    public void EnumerateVideo_AutoLadder_FallsBackToVideoSingle()
    {
        // AutoLadder is expanded by PlanStage via IAbrLadderGenerator BEFORE
        // this helper runs — so when EnumerateVideo sees Auto mode, it just
        // returns the single Video reference (or empty if null).
        EncodingProfile profile = Profile(video: RefVideo(), ladder: new() { Mode = LadderMode.Auto });
        VideoOutput[] result = PlanStageHelpers.EnumerateVideo(profile: profile);

        result.Should().ContainSingle();
    }

    [Fact]
    public void EnumerateVideo_ManualLadderNoReferenceVideo_SynthesisesFromRung()
    {
        EncodingProfile profile = Profile(
            video: null,
            ladder: new()
            {
                Mode = LadderMode.Manual,
                Rungs = [new(Width: 1920, Height: 1080, Codec: VideoCodecType.H265, BitrateKbps: 6000, MaxBitrateKbps: 7200, BufferSizeKbps: 12000, Framerate: 24)],
            }
        );
        VideoOutput[] result = PlanStageHelpers.EnumerateVideo(profile: profile);

        result.Should().ContainSingle();
        result[0].Codec.Should().Be(expected: VideoCodecType.H265);
        result[0].Width.Should().Be(expected: 1920);
        // Synthetic reference uses safe defaults — CRF mode + crf=23.
        result[0].Crf.Should().Be(expected: 23);
    }

    // ── ContainerToOutputFormat ─────────────────────────────────────────────

    [Theory]
    [InlineData(data: [Container.HlsTs, OutputFormat.Hls])]
    [InlineData(data: [Container.HlsFmp4, OutputFormat.Hls])]
    [InlineData(data: [Container.AudioHlsTs, OutputFormat.AudioHls])]
    [InlineData(data: [Container.AudioHlsFmp4, OutputFormat.AudioHls])]
    [InlineData(data: [Container.Mkv, OutputFormat.Mkv])]
    [InlineData(data: [Container.Mka, OutputFormat.Mkv])]
    [InlineData(data: [Container.Mks, OutputFormat.Mkv])]
    [InlineData(data: [Container.Mp4, OutputFormat.Mp4])]
    [InlineData(data: [Container.Aac, OutputFormat.Mp4])]
    [InlineData(data: [Container.Dash, OutputFormat.Dash])]
    [InlineData(data: [Container.Mp3, OutputFormat.Mp3])]
    [InlineData(data: [Container.Flac, OutputFormat.Flac])]
    [InlineData(data: [Container.Ogg, OutputFormat.Ogg])]
    public void ContainerToOutputFormat_KnownContainers(Container c, OutputFormat expected)
    {
        PlanStageHelpers.ContainerToOutputFormat(container: c).Should().Be(expected: expected);
    }
}
