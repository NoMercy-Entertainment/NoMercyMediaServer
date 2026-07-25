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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using SubtitlePolicy = NoMercy.Encoder.Profiles.SubtitlePolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// CustomArguments is the encoder's customization escape hatch — the only way a
/// profile or an IEncoderPlugin can pass ffmpeg flags the schema doesn't model.
/// These pin that it actually reaches the output, not just that it validates.
/// </summary>
public class PlanStageCustomArgumentsTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly Mock<IFfmpegCapabilities> _ffmpegCapabilities = new();
    private readonly PlanStage _stage;

    public PlanStageCustomArgumentsTests()
    {
        _hardware.Setup(h => h.HasGpu).Returns(false);
        _hardware.Setup(h => h.CpuCores).Returns(8);
        _hardware.Setup(h => h.Gpus).Returns([]);
        _hardware.Setup(h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(false);
        _hardware
            .Setup(h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns((GpuDevice?)null);

        _codecResolver
            .Setup(r =>
                r.Resolve(
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(
                new ResolvedCodec(
                    "libx264",
                    new(
                        "libx264",
                        null,
                        ["medium"],
                        ["high"],
                        ["4.1"],
                        new(0, 51, 23),
                        [RateControlMode.Crf],
                        false,
                        false,
                        int.MaxValue,
                        "yuv420p10le",
                        new()
                    ),
                    null,
                    RateControlMode.Crf
                )
            );

        _stage = new(
            new(),
            new(),
            new(),
            _codecResolver.Object,
            _hardware.Object,
            new TonemapSelector(),
            _ffmpegCapabilities.Object,
            new AbrLadderGenerator(),
            new NoOpCropDetector(),
            NullLogger<PlanStage>.Instance
        );
    }

    [Fact]
    public async Task VideoCustomArguments_ReachTheOutputExtraFlags()
    {
        EncodingProfile profile = BuildProfile(
            new() { ["-x264-params"] = "keyint=48:min-keyint=48" }
        );

        OutputPlan plan = await RunPlan(profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video
            .ExtraFlags.Should()
            .ContainKey("-x264-params", "video CustomArguments must reach the encode")
            .WhoseValue.Should()
            .Be("keyint=48:min-keyint=48");
    }

    [Fact]
    public async Task ProfileCustomArguments_ReachTheGlobalExtraFlags()
    {
        EncodingProfile profile = BuildProfile(null) with
        {
            CustomArguments = new() { ["-max_muxing_queue_size"] = "1024" },
        };

        OutputPlan plan = await RunPlan(profile);

        plan.GlobalExtraFlags.Should()
            .NotBeNull("profile-level CustomArguments is the global escape hatch");
        plan.GlobalExtraFlags!.Should()
            .ContainKey("-max_muxing_queue_size")
            .WhoseValue.Should()
            .Be("1024");
    }

    [Fact]
    public async Task AudioCustomArguments_ReachTheAudioOutputExtraFlags()
    {
        EncodingProfile profile = BuildProfile(null) with
        {
            Audio =
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    [],
                    null,
                    null,
                    null,
                    "audio/{lang}",
                    "audio/{lang}/playlist",
                    new() { ["-aac_coder"] = "twoloop" }
                ),
            ],
        };

        OutputPlan plan = await RunPlan(profile, BuildMediaWithStreams());

        AudioOutputPlan audio = Assert.Single(plan.AudioOutputs);
        audio.ExtraFlags.Should().ContainKey("-aac_coder").WhoseValue.Should().Be("twoloop");
    }

    [Fact]
    public async Task SubtitleCustomArguments_ReachTheSubtitleOutputExtraFlags()
    {
        EncodingProfile profile = BuildProfile(null) with
        {
            Subtitles =
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    [],
                    false,
                    null,
                    "subtitles/{lang}",
                    new() { ["-canvas_size"] = "1920x1080" }
                ),
            ],
        };

        OutputPlan plan = await RunPlan(profile, BuildMediaWithStreams());

        SubtitleOutputPlan subtitle = Assert.Single(plan.SubtitleOutputs);
        subtitle.ExtraFlags.Should().ContainKey("-canvas_size").WhoseValue.Should().Be("1920x1080");
    }

    private async Task<OutputPlan> RunPlan(EncodingProfile profile) =>
        await RunPlan(profile, BuildSdrMedia());

    private async Task<OutputPlan> RunPlan(EncodingProfile profile, MediaInfo media)
    {
        ValidateInput input = new(media, profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await _stage.ExecuteAsync(input, context, CancellationToken.None);
        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        return success.Value.OutputPlan;
    }

    private static MediaInfo BuildMediaWithStreams() =>
        BuildSdrMedia() with
        {
            AudioStreams =
            [
                new(
                    1,
                    "aac",
                    6,
                    48000,
                    384,
                    "eng",
                    true,
                    false
                ),
            ],
            SubtitleStreams =
            [
                new(2, "subrip", "eng", true, false),
            ],
        };

    private static MediaInfo BuildSdrMedia() =>
        new(
            "/media/sdr.mkv",
            "matroska",
            TimeSpan.FromMinutes(90),
            8000,
            4_000_000_000,
            [
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
                    6000
                ),
            ],
            [],
            [],
            []
        );

    private static EncodingProfile BuildProfile(Dictionary<string, string>? customArgs) =>
        new(
            Ulid.NewUlid(),
            "CustomArgs Test",
            Container.HlsTs,
            new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                V2RateControlMode.Crf,
                23,
                5000,
                null,
                null,
                "medium",
                CodecProfile.High,
                "4.1",
                null,
                8,
                null,
                2,
                false,
                "video/{label}",
                "video/{label}/playlist",
                customArgs
            ),
            [],
            []
        );
}
