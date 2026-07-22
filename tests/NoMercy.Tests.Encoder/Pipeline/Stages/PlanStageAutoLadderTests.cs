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
        _hardware.Setup(expression: h => h.HasGpu).Returns(value: false);
        _hardware.Setup(expression: h => h.CpuCores).Returns(value: 8);
        _hardware.Setup(expression: h => h.Gpus).Returns(value: []);
        _hardware.Setup(expression: h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(value: false);
        _hardware
            .Setup(expression: h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns(value: (GpuDevice?)null);

        _codecResolver
            .Setup(expression: r =>
                r.Resolve(
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(
                value: new ResolvedCodec(
                    FfmpegEncoderName: "libx264",
                    EncoderInfo: new(
                        FfmpegName: "libx264",
                        RequiredVendor: null,
                        Presets: ["medium"],
                        Profiles: ["high"],
                        Levels: ["4.1"],
                        QualityRange: new(Min: 0, Max: 51, Default: 23),
                        SupportedRateControl: [RateControlMode.Crf],
                        Supports10Bit: false,
                        SupportsHdr: false,
                        MaxConcurrentSessions: int.MaxValue,
                        PixelFormat10Bit: "yuv420p10le",
                        VendorSpecificFlags: new()
                    ),
                    Device: null,
                    DefaultRateControl: RateControlMode.Crf
                )
            );

        _stage = new(
            graphBuilder: new(),
            groupingStrategy: new(),
            costEstimator: new(),
            codecResolver: _codecResolver.Object,
            hardware: _hardware.Object,
            tonemapSelector: new TonemapSelector(),
            ffmpegCapabilities: new Mock<IFfmpegCapabilities>().Object,
            abrLadderGenerator: new AbrLadderGenerator(),
            cropDetector: new NoOpCropDetector(),
            logger: NullLogger<PlanStage>.Instance
        );
    }

    [Fact]
    public async Task AutoLadder_Off_PreservesManualVariants()
    {
        EncodingProfile profile = BuildProfile(
            autoLadder: false,
            rungs: [BuildVideo(width: 1920, height: 1080), BuildVideo(width: 1280, height: 720)]
        );

        OutputPlan plan = await RunPlan(media: BuildMedia(width: 1920, height: 1080), profile: profile);

        Assert.Equal(expected: 2, actual: plan.VideoOutputs.Length);
        Assert.Contains(collection: plan.VideoOutputs, filter: v => v.Height == 1080);
        Assert.Contains(collection: plan.VideoOutputs, filter: v => v.Height == 720);
    }

    [Fact]
    public async Task AutoLadder_On_Expand1080pReference_Produces360_480_720_1080()
    {
        EncodingProfile profile = BuildProfile(autoLadder: true, rungs: [BuildVideo(width: 1920, height: 1080)]);

        OutputPlan plan = await RunPlan(media: BuildMedia(width: 1920, height: 1080, bitrateKbps: 6000), profile: profile);

        int[] heights = plan.VideoOutputs.Select(selector: v => v.Height).ToArray();
        Assert.Contains(expected: 360, collection: heights);
        Assert.Contains(expected: 480, collection: heights);
        Assert.Contains(expected: 720, collection: heights);
        Assert.Contains(expected: 1080, collection: heights);
    }

    [Fact]
    public async Task AutoLadder_On_720pSource_SkipsHigherTiers()
    {
        EncodingProfile profile = BuildProfile(autoLadder: true, rungs: [BuildVideo(width: 1280, height: 720)]);

        OutputPlan plan = await RunPlan(media: BuildMedia(width: 1280, height: 720, bitrateKbps: 3000), profile: profile);

        Assert.All(collection: plan.VideoOutputs, action: v => Assert.True(condition: v.Height <= 720));
        Assert.DoesNotContain(expected: 1080, collection: plan.VideoOutputs.Select(selector: v => v.Height));
    }

    [Fact]
    public async Task AutoLadder_On_MultipleVariants_FallsBackToManual()
    {
        // AutoLadder requires exactly 1 reference profile — with more than 1,
        // the stage logs a warning and keeps the manual variants.
        EncodingProfile profile = BuildProfile(
            autoLadder: true,
            rungs: [BuildVideo(width: 1920, height: 1080), BuildVideo(width: 1280, height: 720)]
        );

        OutputPlan plan = await RunPlan(media: BuildMedia(width: 1920, height: 1080), profile: profile);

        Assert.Equal(expected: 2, actual: plan.VideoOutputs.Length);
    }

    [Fact]
    public async Task AutoLadder_On_AudioOnlySource_NoExpansion()
    {
        EncodingProfile profile = BuildProfile(autoLadder: true, rungs: [BuildVideo(width: 1920, height: 1080)]);

        // Source has no video streams → auto-ladder passthrough.
        OutputPlan plan = await RunPlan(media: BuildAudioOnlyMedia(), profile: profile);

        Assert.Empty(collection: plan.VideoOutputs);
    }

    private async Task<OutputPlan> RunPlan(MediaInfo media, EncodingProfile profile)
    {
        ValidateInput input = new(Media: media, Profile: profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await _stage.ExecuteAsync(input: input, context: context, ct: CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        return success.Value.OutputPlan;
    }

    private static MediaInfo BuildMedia(int width, int height, long bitrateKbps = 6000) =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: bitrateKbps + 500,
            FileSizeBytes: 4_000_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: width,
                    Height: height,
                    FrameRate: 24.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: bitrateKbps
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static MediaInfo BuildAudioOnlyMedia() =>
        new(
            FilePath: "/media/song.flac",
            Format: "flac",
            Duration: TimeSpan.FromMinutes(minutes: 4),
            OverallBitRateKbps: 800,
            FileSizeBytes: 20_000_000,
            VideoStreams: [],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static EncodingProfile BuildProfile(bool autoLadder, LadderRung[] rungs) =>
        new(
            Id: Ulid.NewUlid(),
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
            Width: width,
            Height: height,
            Codec: VideoCodecType.H264,
            BitrateKbps: 4000,
            MaxBitrateKbps: 6000,
            BufferSizeKbps: 8000,
            Framerate: 24.0,
            Preset: "medium",
            CodecProfile: CodecProfile.High,
            BitDepth: 8,
            PixelFormat: "yuv420p"
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
        _hardware.Setup(expression: h => h.HasGpu).Returns(value: false);
        _hardware.Setup(expression: h => h.CpuCores).Returns(value: 8);
        _hardware.Setup(expression: h => h.Gpus).Returns(value: []);
        _hardware.Setup(expression: h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(value: false);
        _hardware
            .Setup(expression: h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns(value: (GpuDevice?)null);

        _codecResolver
            .Setup(expression: r =>
                r.Resolve(
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(
                value: new ResolvedCodec(
                    FfmpegEncoderName: "libx264",
                    EncoderInfo: new(
                        FfmpegName: "libx264",
                        RequiredVendor: null,
                        Presets: ["medium"],
                        Profiles: ["high"],
                        Levels: ["4.1"],
                        QualityRange: new(Min: 0, Max: 51, Default: 23),
                        SupportedRateControl: [RateControlMode.Crf],
                        Supports10Bit: false,
                        SupportsHdr: false,
                        MaxConcurrentSessions: int.MaxValue,
                        PixelFormat10Bit: "yuv420p10le",
                        VendorSpecificFlags: new()
                    ),
                    Device: null,
                    DefaultRateControl: RateControlMode.Crf
                )
            );

        return new(
            graphBuilder: new(),
            groupingStrategy: new(),
            costEstimator: new(),
            codecResolver: _codecResolver.Object,
            hardware: _hardware.Object,
            tonemapSelector: new TonemapSelector(),
            ffmpegCapabilities: new Mock<IFfmpegCapabilities>().Object,
            abrLadderGenerator: _abrGenerator.Object,
            cropDetector: new NoOpCropDetector(),
            logger: NullLogger<PlanStage>.Instance
        );
    }

    private static MediaInfo Build1080pMedia() =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: 4500,
            FileSizeBytes: 3_000_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: 1920,
                    Height: 1080,
                    FrameRate: 24.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: 4000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static LadderRung[] ThreeKnownRungs() =>
        [
            new(Width: 640, Height: 360, Codec: VideoCodecType.H264, BitrateKbps: 400, MaxBitrateKbps: 600, BufferSizeKbps: 800, Framerate: 24.0),
            new(Width: 1280, Height: 720, Codec: VideoCodecType.H264, BitrateKbps: 2000, MaxBitrateKbps: 3000, BufferSizeKbps: 4000, Framerate: 24.0),
            new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 5000, MaxBitrateKbps: 7500, BufferSizeKbps: 10000, Framerate: 24.0),
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
            .Setup(expression: g =>
                g.GenerateLadder(
                    It.IsAny<MediaInfo>(),
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<AutoLadderConfig>(),
                    It.IsAny<VideoOutput?>()
                )
            )
            .Returns(value: mockedRungs);

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "AutoConfigTest",
            Container: Container.HlsTs,
            Video: new(
                Policy: NoMercy.Encoder.Profiles.StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: NoMercy.Encoder.Profiles.RateControlMode.Crf,
                Crf: 23,
                BitrateKbps: 4000,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "medium",
                CodecProfile: CodecProfile.High,
                Level: "4.1",
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "video/:label:",
                PlaylistNameTemplate: "video/:label:/playlist"
            ),
            Audio: [],
            Subtitles: [],
            Ladder: new() { Mode = LadderMode.Auto, AutoConfig = autoConfig }
        );

        PlanStage stage = BuildStage();
        ValidateInput input = new(Media: Build1080pMedia(), Profile: profile);
        EncodingContext context = EncodingContext.Create();

        StageResult result = await stage.ExecuteAsync(input: input, context: context, ct: CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        OutputPlan outputPlan = success.Value.OutputPlan;

        // GenerateLadder called once with the expected codec + AutoConfig.
        _abrGenerator.Verify(
            expression: g =>
                g.GenerateLadder(
                    It.IsAny<MediaInfo>(),
                    VideoCodecType.H264,
                    autoConfig,
                    It.IsAny<VideoOutput?>()
                ),
            times: Times.Once
        );

        // Legacy Generate must NOT be called.
        _abrGenerator.Verify(
            expression: g => g.Generate(It.IsAny<MediaInfo>(), It.IsAny<VideoOutput>()),
            times: Times.Never
        );

        // Resulting rungs must match the mocked array (by height).
        int[] heights = outputPlan.VideoOutputs.Select(selector: v => v.Height).ToArray();
        Assert.Contains(expected: 360, collection: heights);
        Assert.Contains(expected: 720, collection: heights);
        Assert.Contains(expected: 1080, collection: heights);
    }

    // ── Legacy path (AutoConfig null) ─────────────────────────────────────

    [Fact]
    public async Task AutoConfig_Null_CallsGenerate_NotGenerateLadder()
    {
        _abrGenerator
            .Setup(expression: g => g.Generate(It.IsAny<MediaInfo>(), It.IsAny<VideoOutput>()))
            .Returns(value:
            [
                new(
                    Policy: NoMercy.Encoder.Profiles.StreamPolicy.Transcode,
                    Codec: VideoCodecType.H264,
                    Width: 1920,
                    Height: 1080,
                    RateControl: NoMercy.Encoder.Profiles.RateControlMode.Crf,
                    Crf: 23,
                    BitrateKbps: 4000,
                    MaxBitrateKbps: null,
                    BufferSizeKbps: null,
                    Preset: "medium",
                    CodecProfile: CodecProfile.High,
                    Level: "4.1",
                    Tune: null,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    KeyframeIntervalSeconds: 2,
                    ConvertHdrToSdr: false,
                    SegmentNameTemplate: "video/:label:",
                    PlaylistNameTemplate: "video/:label:/playlist"
                ),
            ]);

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "LegacyAutoTest",
            Container: Container.HlsTs,
            Video: new(
                Policy: NoMercy.Encoder.Profiles.StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: NoMercy.Encoder.Profiles.RateControlMode.Crf,
                Crf: 23,
                BitrateKbps: 4000,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "medium",
                CodecProfile: CodecProfile.High,
                Level: "4.1",
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "video/:label:",
                PlaylistNameTemplate: "video/:label:/playlist"
            ),
            Audio: [],
            Subtitles: [],
            Ladder: new() { Mode = LadderMode.Auto, AutoConfig = null }
        );

        PlanStage stage = BuildStage();
        ValidateInput input = new(Media: Build1080pMedia(), Profile: profile);
        EncodingContext context = EncodingContext.Create();

        StageResult result = await stage.ExecuteAsync(input: input, context: context, ct: CancellationToken.None);

        Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);

        // Legacy Generate called once.
        _abrGenerator.Verify(
            expression: g => g.Generate(It.IsAny<MediaInfo>(), It.IsAny<VideoOutput>()),
            times: Times.Once
        );

        // New GenerateLadder must NOT be called.
        _abrGenerator.Verify(
            expression: g =>
                g.GenerateLadder(
                    It.IsAny<MediaInfo>(),
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<AutoLadderConfig>(),
                    It.IsAny<VideoOutput?>()
                ),
            times: Times.Never
        );
    }
}
