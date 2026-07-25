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
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using LadderMode = NoMercy.Encoder.Profiles.LadderMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class PlanStageTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly ExecutionGraphBuilder _graphBuilder = new();
    private readonly GroupingStrategy _groupingStrategy = new();
    private readonly CostEstimator _costEstimator = new();
    private readonly PlanStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public PlanStageTests()
    {
        // Default hardware: no GPU
        _hardware.Setup(h => h.HasGpu).Returns(false);
        _hardware.Setup(h => h.CpuCores).Returns(8);
        _hardware.Setup(h => h.Gpus).Returns([]);
        _hardware.Setup(h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(false);
        _hardware
            .Setup(h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns((GpuDevice?)null);

        // Default codec resolver — return software H264 encoder
        _codecResolver
            .Setup(r =>
                r.Resolve(
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(BuildSoftwareH264Codec());

        _stage = new(
            _graphBuilder,
            _groupingStrategy,
            _costEstimator,
            _codecResolver.Object,
            _hardware.Object,
            new TonemapSelector(),
            new Mock<IFfmpegCapabilities>().Object,
            new AbrLadderGenerator(),
            new NoOpCropDetector(),
            NullLogger<PlanStage>.Instance
        );
    }

    private static ResolvedCodec BuildSoftwareH264Codec() =>
        new(
            "libx264",
            new(
                "libx264",
                null,
                ["slow", "medium", "fast"],
                ["high"],
                ["4.1"],
                new(0, 51, 23),
                [RateControlMode.Crf, RateControlMode.Cbr],
                false,
                false,
                int.MaxValue,
                "yuv420p10le",
                new()
            ),
            null,
            RateControlMode.Crf
        );

    private static MediaInfo BuildMediaInfo() =>
        new(
            "/movies/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            8000,
            7_200_000_000,
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
                    6000
                ),
            ],
            [
                new(
                    1,
                    "aac",
                    2,
                    48000,
                    192,
                    "en",
                    true,
                    false
                ),
            ],
            [],
            []
        );

    private static EncodingProfile BuildSimpleProfile() =>
        new(
            Ulid.NewUlid(),
            "Test",
            Container.HlsTs,
            new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                V2RateControlMode.Crf,
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
                ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
            ),
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    ["en"],
                    null,
                    null,
                    null,
                    ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            []
        );

    // ------------------------------------------------------------------
    // Simple profile → execution plan with at least one group
    // ------------------------------------------------------------------

    [Fact]
    public async Task SimpleProfile_ProducesExecutionPlanWithGroups()
    {
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = BuildSimpleProfile();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageSuccess<ExecutionPlan>>();
        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.Groups.Should().NotBeEmpty();
    }

    // ------------------------------------------------------------------
    // Plan has a non-zero estimated duration
    // ------------------------------------------------------------------

    [Fact]
    public async Task SimpleProfile_EstimatedDurationIsPositive()
    {
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = BuildSimpleProfile();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.EstimatedTotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    // ------------------------------------------------------------------
    // Plan has an OutputPlan with matching format
    // ------------------------------------------------------------------

    [Fact]
    public async Task SimpleProfile_OutputPlanMatchesFormat()
    {
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = BuildSimpleProfile();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.OutputPlan.Should().NotBeNull();
        plan.OutputPlan.Format.Should().Be(OutputFormat.Hls);
    }

    // ------------------------------------------------------------------
    // Plan has video + audio outputs
    // ------------------------------------------------------------------

    [Fact]
    public async Task SimpleProfile_OutputPlanContainsVideoAndAudio()
    {
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = BuildSimpleProfile();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.OutputPlan.VideoOutputs.Should().HaveCount(1);
        plan.OutputPlan.VideoOutputs[0].EncoderName.Should().Be("libx264");
        plan.OutputPlan.AudioOutputs.Should().HaveCount(1);
    }

    // ------------------------------------------------------------------
    // Multi-output profile → multiple video output plans
    // ------------------------------------------------------------------

    // ------------------------------------------------------------------
    // 10-bit downgrade guard — encoders that don't support 10-bit must
    // fall back to 8-bit instead of emitting an empty pixel format.
    // Regression: previously "v.TenBit ? encoder.PixelFormat10Bit : yuv420p"
    // would emit "" when PixelFormat10Bit was empty, and ffmpeg would
    // either pick the source format or fail with "Invalid pixel format".
    // ------------------------------------------------------------------

    [Fact]
    public async Task TenBitRequested_EncoderLacks10Bit_DowngradedTo8Bit()
    {
        // Default resolver returns libx264 with Supports10Bit=false + empty PixelFormat10Bit.
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            "TenBitDowngrade",
            Container.HlsTs,
            new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                V2RateControlMode.Crf,
                23,
                4000,
                null,
                null,
                "medium",
                CodecProfile.High,
                "4.1",
                null,
                10,
                null,
                2,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    ["en"],
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            []
        );

        StageResult result = await _stage.ExecuteAsync(new(media, profile), _context, default);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.OutputPlan.VideoOutputs[0].TenBit.Should().BeFalse("encoder doesn't support 10-bit");
        plan.OutputPlan.VideoOutputs[0].PixelFormat.Should().Be("yuv420p");
    }

    [Fact]
    public async Task TenBitRequested_EncoderSupports10Bit_KeepsTenBit()
    {
        // Override resolver to return an encoder that DOES support 10-bit.
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
                    "libx265",
                    new(
                        "libx265",
                        null,
                        ["slow", "medium", "fast"],
                        ["main", "main10"],
                        ["4.1"],
                        new(0, 51, 28),
                        [RateControlMode.Crf],
                        true,
                        true,
                        int.MaxValue,
                        "yuv420p10le",
                        new()
                    ),
                    null,
                    RateControlMode.Crf
                )
            );

        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            "TenBit",
            Container.HlsTs,
            new(
                StreamPolicy.Transcode,
                VideoCodecType.H265,
                1920,
                1080,
                V2RateControlMode.Crf,
                28,
                4000,
                null,
                null,
                "medium",
                CodecProfile.Main10,
                "4.1",
                null,
                10,
                null,
                2,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    ["en"],
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            []
        );

        StageResult result = await _stage.ExecuteAsync(new(media, profile), _context, default);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.OutputPlan.VideoOutputs[0].TenBit.Should().BeTrue();
        plan.OutputPlan.VideoOutputs[0].PixelFormat.Should().Be("yuv420p10le");
    }

    [Fact]
    public async Task MultiOutputProfile_ProducesMultipleVideoOutputPlans()
    {
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            Name: "Multi",
            Container: Container.HlsTs,
            Video: null,
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    ["en"],
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles: [],
            Ladder: new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new(
                        1920,
                        1080,
                        VideoCodecType.H264,
                        4000,
                        6000,
                        8000,
                        24.0,
                        "medium",
                        CodecProfile.High,
                        8,
                        "yuv420p"
                    ),
                    new(
                        1280,
                        720,
                        VideoCodecType.H264,
                        2500,
                        3750,
                        5000,
                        24.0,
                        "medium",
                        CodecProfile.High,
                        8,
                        "yuv420p"
                    ),
                ],
            }
        );

        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageSuccess<ExecutionPlan>>();
        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.OutputPlan.VideoOutputs.Should().HaveCount(2);
    }

    // ------------------------------------------------------------------
    // OutputNamingResolver integration — when MediaItemRef is present
    // in the context, PlanStage must call IOutputNamingResolver.Resolve
    // and attach the resulting BundleLayout to OutputPlan.Layout.
    // ------------------------------------------------------------------

    [Fact]
    public async Task OutputNamingResolver_WhenMediaItemProvided_LayoutAttachedToOutputPlan()
    {
        Mock<IOutputNamingResolver> namingResolver = new();
        BundleLayout fakeLayout = new(
            "mfa",
            "test",
            false,
            "encodes/test",
            "mfa_master.m3u8",
            "encodes/test/manifest.json",
            "encodes/test/reconstruction.json",
            string.Empty
        );

        namingResolver
            .Setup(r => r.Resolve(It.IsAny<MediaItemRef>(), It.IsAny<EncodingProfile>()))
            .Returns(fakeLayout);

        PlanStage stage = new(
            _graphBuilder,
            groupingStrategy: _groupingStrategy,
            costEstimator: _costEstimator,
            codecResolver: _codecResolver.Object,
            hardware: _hardware.Object,
            tonemapSelector: new TonemapSelector(),
            ffmpegCapabilities: new Mock<IFfmpegCapabilities>().Object,
            abrLadderGenerator: new AbrLadderGenerator(),
            cropDetector: new NoOpCropDetector(),
            logger: NullLogger<PlanStage>.Instance,
            outputNamingResolver: namingResolver.Object
        );

        MediaItemRef mediaItem = new(MediaType.Movie, 550, "Fight Club", 1999);
        EncodingContext contextWithItem = _context with { MediaItem = mediaItem };

        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = BuildSimpleProfile();

        StageResult result = await stage.ExecuteAsync(
            new(media, profile),
            contextWithItem,
            default
        );

        result.Should().BeOfType<StageSuccess<ExecutionPlan>>();
        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.OutputPlan.Layout.Should().NotBeNull();
        plan.OutputPlan.Layout.Should().Be(fakeLayout);

        namingResolver.Verify(
            r => r.Resolve(It.Is<MediaItemRef>(m => m.Id == 550), It.IsAny<EncodingProfile>()),
            Times.Once
        );
    }

    [Fact]
    public async Task OutputNamingResolver_WhenNoMediaItemInContext_LayoutIsNull()
    {
        Mock<IOutputNamingResolver> namingResolver = new();

        PlanStage stage = new(
            _graphBuilder,
            groupingStrategy: _groupingStrategy,
            costEstimator: _costEstimator,
            codecResolver: _codecResolver.Object,
            hardware: _hardware.Object,
            tonemapSelector: new TonemapSelector(),
            ffmpegCapabilities: new Mock<IFfmpegCapabilities>().Object,
            abrLadderGenerator: new AbrLadderGenerator(),
            cropDetector: new NoOpCropDetector(),
            logger: NullLogger<PlanStage>.Instance,
            outputNamingResolver: namingResolver.Object
        );

        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = BuildSimpleProfile();

        StageResult result = await stage.ExecuteAsync(
            new(media, profile),
            _context, // no MediaItem
            default
        );

        result.Should().BeOfType<StageSuccess<ExecutionPlan>>();
        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.OutputPlan.Layout.Should().BeNull();

        namingResolver.Verify(
            r => r.Resolve(It.IsAny<MediaItemRef>(), It.IsAny<EncodingProfile>()),
            Times.Never
        );
    }
}
