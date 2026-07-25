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
using AutoLadderConfig = NoMercy.Encoder.Profiles.AutoLadderConfig;
using BitrateStrategy = NoMercy.Encoder.Profiles.BitrateStrategy;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using LadderConfig = NoMercy.Encoder.Profiles.LadderConfig;
using LadderMode = NoMercy.Encoder.Profiles.LadderMode;
using LadderRung = NoMercy.Encoder.Profiles.LadderRung;
using LadderTiers = NoMercy.Encoder.Profiles.LadderTiers;
using VideoOutput = NoMercy.Encoder.Profiles.VideoOutput;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class PlanStageAutoLadderTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly PlanStage _stage;

    public PlanStageAutoLadderTests()
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
            new Mock<IFfmpegCapabilities>().Object,
            new AbrLadderGenerator(),
            new NoOpCropDetector(),
            NullLogger<PlanStage>.Instance
        );
    }

    [Fact]
    public async Task AutoLadder_Off_PreservesManualVariants()
    {
        EncodingProfile profile = BuildProfile(
            false,
            [BuildVideo(1920, 1080), BuildVideo(1280, 720)]
        );

        OutputPlan plan = await RunPlan(BuildMedia(1920, 1080), profile);

        Assert.Equal(2, plan.VideoOutputs.Length);
        Assert.Contains(plan.VideoOutputs, v => v.Height == 1080);
        Assert.Contains(plan.VideoOutputs, v => v.Height == 720);
    }

    [Fact]
    public async Task AutoLadder_On_Expand1080pReference_Produces360_480_720_1080()
    {
        EncodingProfile profile = BuildProfile(true, [BuildVideo(1920, 1080)]);

        OutputPlan plan = await RunPlan(BuildMedia(1920, 1080, 6000), profile);

        int[] heights = plan.VideoOutputs.Select(v => v.Height).ToArray();
        Assert.Contains(360, heights);
        Assert.Contains(480, heights);
        Assert.Contains(720, heights);
        Assert.Contains(1080, heights);
    }

    [Fact]
    public async Task AutoLadder_On_720pSource_SkipsHigherTiers()
    {
        EncodingProfile profile = BuildProfile(true, [BuildVideo(1280, 720)]);

        OutputPlan plan = await RunPlan(BuildMedia(1280, 720, 3000), profile);

        Assert.All(plan.VideoOutputs, v => Assert.True(v.Height <= 720));
        Assert.DoesNotContain(1080, plan.VideoOutputs.Select(v => v.Height));
    }

    [Fact]
    public async Task AutoLadder_On_MultipleVariants_FallsBackToManual()
    {
        // AutoLadder requires exactly 1 reference profile — with more than 1,
        // the stage logs a warning and keeps the manual variants.
        EncodingProfile profile = BuildProfile(
            true,
            [BuildVideo(1920, 1080), BuildVideo(1280, 720)]
        );

        OutputPlan plan = await RunPlan(BuildMedia(1920, 1080), profile);

        Assert.Equal(2, plan.VideoOutputs.Length);
    }

    [Fact]
    public async Task AutoLadder_On_AudioOnlySource_NoExpansion()
    {
        EncodingProfile profile = BuildProfile(true, [BuildVideo(1920, 1080)]);

        // Source has no video streams → auto-ladder passthrough.
        OutputPlan plan = await RunPlan(BuildAudioOnlyMedia(), profile);

        Assert.Empty(plan.VideoOutputs);
    }

    private async Task<OutputPlan> RunPlan(MediaInfo media, EncodingProfile profile)
    {
        ValidateInput input = new(media, profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await _stage.ExecuteAsync(input, context, CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        return success.Value.OutputPlan;
    }

    private static MediaInfo BuildMedia(int width, int height, long bitrateKbps = 6000) =>
        new(
            "/media/test.mkv",
            "matroska",
            TimeSpan.FromMinutes(90),
            bitrateKbps + 500,
            4_000_000_000,
            [
                new(
                    0,
                    "h264",
                    width,
                    height,
                    24.0,
                    8,
                    "yuv420p",
                    null,
                    null,
                    null,
                    true,
                    bitrateKbps
                ),
            ],
            [],
            [],
            []
        );

    private static MediaInfo BuildAudioOnlyMedia() =>
        new(
            "/media/song.flac",
            "flac",
            TimeSpan.FromMinutes(4),
            800,
            20_000_000,
            [],
            [],
            [],
            []
        );

    private static EncodingProfile BuildProfile(bool autoLadder, LadderRung[] rungs) =>
        new(
            Ulid.NewUlid(),
            Name: "Test",
            Container: Container.HlsTs,
            Video: null,
            Audio: [],
            Subtitles: [],
            Ladder: new()
            {
                Mode = autoLadder ? LadderMode.Auto : LadderMode.Manual,
                Rungs = rungs,
            }
        );

    private static LadderRung BuildVideo(int width, int height) =>
        new(
            width,
            height,
            VideoCodecType.H264,
            4000,
            6000,
            8000,
            24.0,
            "medium",
            CodecProfile.High,
            8,
            "yuv420p"
        );
}

/// <summary>
/// Verifies that <see cref="AutoLadderExpander.Expand"/> routes to the correct
/// <see cref="IAbrLadderGenerator"/> method depending on whether
/// <see cref="LadderConfig.AutoConfig"/> is set.
/// </summary>
public class PlanStageAutoLadderRoutingTests
{
    private readonly Mock<IAbrLadderGenerator> _abrGenerator = new();
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();

    private PlanStage BuildStage()
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

        return new(
            new(),
            new(),
            new(),
            _codecResolver.Object,
            _hardware.Object,
            new TonemapSelector(),
            new Mock<IFfmpegCapabilities>().Object,
            _abrGenerator.Object,
            new NoOpCropDetector(),
            NullLogger<PlanStage>.Instance
        );
    }

    private static MediaInfo Build1080pMedia() =>
        new(
            "/media/test.mkv",
            "matroska",
            TimeSpan.FromMinutes(90),
            4500,
            3_000_000_000,
            [
                new(
                    0,
                    "h264",
                    1920,
                    1080,
                    24.0,
                    8,
                    "yuv420p",
                    null,
                    null,
                    null,
                    true,
                    4000
                ),
            ],
            [],
            [],
            []
        );

    private static LadderRung[] ThreeKnownRungs() =>
        [
            new(640, 360, VideoCodecType.H264, 400, 600, 800, 24.0),
            new(1280, 720, VideoCodecType.H264, 2000, 3000, 4000, 24.0),
            new(1920, 1080, VideoCodecType.H264, 5000, 7500, 10000, 24.0),
        ];

    // ── New path (AutoConfig set) ──────────────────────────────────────────

    [Fact]
    public async Task AutoConfig_Set_CallsGenerateLadder_NotGenerate()
    {
        AutoLadderConfig autoConfig = new()
        {
            Tiers = LadderTiers.Standard,
            BitrateStrategy = BitrateStrategy.AppleHlsRecommended,
        };

        LadderRung[] mockedRungs = ThreeKnownRungs();

        _abrGenerator
            .Setup(g =>
                g.GenerateLadder(
                    It.IsAny<MediaInfo>(),
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<AutoLadderConfig>(),
                    It.IsAny<VideoOutput?>()
                )
            )
            .Returns(mockedRungs);

        EncodingProfile profile = new(
            Ulid.NewUlid(),
            Name: "AutoConfigTest",
            Container: Container.HlsTs,
            Video: new(
                NoMercy.Encoder.Profiles.StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                NoMercy.Encoder.Profiles.RateControlMode.Crf,
                23,
                4000,
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
                "video/:label:",
                "video/:label:/playlist"
            ),
            Audio: [],
            Subtitles: [],
            Ladder: new() { Mode = LadderMode.Auto, AutoConfig = autoConfig }
        );

        PlanStage stage = BuildStage();
        ValidateInput input = new(Build1080pMedia(), profile);
        EncodingContext context = EncodingContext.Create();

        StageResult result = await stage.ExecuteAsync(input, context, CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        OutputPlan outputPlan = success.Value.OutputPlan;

        // GenerateLadder called once with the expected codec + AutoConfig.
        _abrGenerator.Verify(
            g =>
                g.GenerateLadder(
                    It.IsAny<MediaInfo>(),
                    VideoCodecType.H264,
                    autoConfig,
                    It.IsAny<VideoOutput?>()
                ),
            Times.Once
        );

        // Legacy Generate must NOT be called.
        _abrGenerator.Verify(
            g => g.Generate(It.IsAny<MediaInfo>(), It.IsAny<VideoOutput>()),
            Times.Never
        );

        // Resulting rungs must match the mocked array (by height).
        int[] heights = outputPlan.VideoOutputs.Select(v => v.Height).ToArray();
        Assert.Contains(360, heights);
        Assert.Contains(720, heights);
        Assert.Contains(1080, heights);
    }

    // ── Legacy path (AutoConfig null) ─────────────────────────────────────

    [Fact]
    public async Task AutoConfig_Null_CallsGenerate_NotGenerateLadder()
    {
        _abrGenerator
            .Setup(g => g.Generate(It.IsAny<MediaInfo>(), It.IsAny<VideoOutput>()))
            .Returns([
                new(
                    NoMercy.Encoder.Profiles.StreamPolicy.Transcode,
                    VideoCodecType.H264,
                    1920,
                    1080,
                    NoMercy.Encoder.Profiles.RateControlMode.Crf,
                    23,
                    4000,
                    null,
                    null,
                    "medium",
                    CodecProfile.High,
                    "4.1",
                    null,
                    8,
                    "yuv420p",
                    2,
                    false,
                    "video/:label:",
                    "video/:label:/playlist"
                ),
            ]);

        EncodingProfile profile = new(
            Ulid.NewUlid(),
            Name: "LegacyAutoTest",
            Container: Container.HlsTs,
            Video: new(
                NoMercy.Encoder.Profiles.StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                NoMercy.Encoder.Profiles.RateControlMode.Crf,
                23,
                4000,
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
                "video/:label:",
                "video/:label:/playlist"
            ),
            Audio: [],
            Subtitles: [],
            Ladder: new() { Mode = LadderMode.Auto, AutoConfig = null }
        );

        PlanStage stage = BuildStage();
        ValidateInput input = new(Build1080pMedia(), profile);
        EncodingContext context = EncodingContext.Create();

        StageResult result = await stage.ExecuteAsync(input, context, CancellationToken.None);

        Assert.IsType<StageSuccess<ExecutionPlan>>(result);

        // Legacy Generate called once.
        _abrGenerator.Verify(
            g => g.Generate(It.IsAny<MediaInfo>(), It.IsAny<VideoOutput>()),
            Times.Once
        );

        // New GenerateLadder must NOT be called.
        _abrGenerator.Verify(
            g =>
                g.GenerateLadder(
                    It.IsAny<MediaInfo>(),
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<AutoLadderConfig>(),
                    It.IsAny<VideoOutput?>()
                ),
            Times.Never
        );
    }
}
