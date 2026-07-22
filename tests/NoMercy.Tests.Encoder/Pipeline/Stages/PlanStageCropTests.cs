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
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class PlanStageCropTests
{
    [Fact]
    public async Task AutoDetectCropOff_DetectorNotInvoked()
    {
        Mock<ICropDetector> detector = new();
        PlanStage stage = BuildStage(cropDetector: detector.Object);

        EncodingProfile profile = BuildProfile(autoDetectCrop: false);
        OutputPlan plan = await RunPlan(stage: stage, profile: profile);

        detector.Verify(
            expression: d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
        plan.VideoOutputs[0].CropFilter.Should().BeNull();
    }

    [Fact]
    public async Task AutoDetectCropOn_ShouldCrop_PopulatesCropFilterOnAllOutputs()
    {
        Mock<ICropDetector> detector = new();
        detector
            .Setup(expression: d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new CropResult(Width: 1920, Height: 800, X: 0, Y: 140, ShouldCrop: true));

        PlanStage stage = BuildStage(cropDetector: detector.Object);
        EncodingProfile profile = BuildProfile(autoDetectCrop: true);

        OutputPlan plan = await RunPlan(stage: stage, profile: profile);

        plan.VideoOutputs.Should().HaveCountGreaterThan(expected: 0);
        plan.VideoOutputs[0].CropFilter.Should().Be(expected: "1920:800:0:140");
        detector.Verify(
            expression: d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task AutoDetectCropOn_NoCropNeeded_CropFilterNull()
    {
        Mock<ICropDetector> detector = new();
        detector
            .Setup(expression: d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new CropResult(Width: 0, Height: 0, X: 0, Y: 0, ShouldCrop: false));

        PlanStage stage = BuildStage(cropDetector: detector.Object);
        EncodingProfile profile = BuildProfile(autoDetectCrop: true);

        OutputPlan plan = await RunPlan(stage: stage, profile: profile);

        plan.VideoOutputs[0].CropFilter.Should().BeNull();
    }

    [Fact]
    public async Task AutoDetectCropOn_DetectorThrows_ContinuesWithoutCrop()
    {
        Mock<ICropDetector> detector = new();
        detector
            .Setup(expression: d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "boom"));

        PlanStage stage = BuildStage(cropDetector: detector.Object);
        EncodingProfile profile = BuildProfile(autoDetectCrop: true);

        OutputPlan plan = await RunPlan(stage: stage, profile: profile);

        plan.VideoOutputs[0].CropFilter.Should().BeNull();
    }

    [Fact]
    public async Task AutoDetectCropOn_BarsWithinTolerance_CropFilterNull()
    {
        // Source 1920x1080; detected content 1920x1000 → only 80px of vertical
        // bar, under the 100px threshold. A stray border must NOT force a crop
        // (which would disable stream-copy on every rung it touches).
        Mock<ICropDetector> detector = new();
        detector
            .Setup(expression: d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new CropResult(Width: 1920, Height: 1000, X: 0, Y: 40, ShouldCrop: true));

        PlanStage stage = BuildStage(cropDetector: detector.Object);
        OutputPlan plan = await RunPlan(stage: stage, profile: BuildProfile(autoDetectCrop: true));

        plan.VideoOutputs[0].CropFilter.Should().BeNull();
    }

    [Fact]
    public async Task AutoDetectCropOn_HorizontalBarsOverThreshold_PopulatesCropFilter()
    {
        // Pillarbox: 1920x1080 source, content 1700x1080 → 220px horizontal bar,
        // over the threshold, so the crop is honoured even though it forces a
        // re-encode.
        Mock<ICropDetector> detector = new();
        detector
            .Setup(expression: d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new CropResult(Width: 1700, Height: 1080, X: 110, Y: 0, ShouldCrop: true)
            );

        PlanStage stage = BuildStage(cropDetector: detector.Object);
        OutputPlan plan = await RunPlan(stage: stage, profile: BuildProfile(autoDetectCrop: true));

        plan.VideoOutputs[0].CropFilter.Should().Be(expected: "1700:1080:110:0");
    }

    [Fact]
    public async Task AutoDetectCropOn_HdrSource_PassesHdrFlagToDetector()
    {
        // The HDR flag must reach the detector so it can pick the HDR cropdetect
        // limit — the whole reason letterbox on HDR sources was being missed.
        bool? capturedHdr = null;
        Mock<ICropDetector> detector = new();
        detector
            .Setup(expression: d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(action: (string _, Guid? _, bool? isHdr, CancellationToken _) => capturedHdr = isHdr)
            .ReturnsAsync(value: new CropResult(Width: 0, Height: 0, X: 0, Y: 0, ShouldCrop: false));

        PlanStage stage = BuildStage(cropDetector: detector.Object);
        ValidateInput input = new(Media: BuildHdrMedia(), Profile: BuildProfile(autoDetectCrop: true));
        await stage.ExecuteAsync(input: input, context: EncodingContext.Create(), ct: CancellationToken.None);

        capturedHdr.Should().BeTrue();
    }

    private static PlanStage BuildStage(ICropDetector cropDetector)
    {
        Mock<IHardwareCapabilities> hardware = new();
        hardware.Setup(expression: h => h.HasGpu).Returns(value: false);
        hardware.Setup(expression: h => h.CpuCores).Returns(value: 8);
        hardware.Setup(expression: h => h.Gpus).Returns(value: []);
        hardware.Setup(expression: h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(value: false);
        hardware.Setup(expression: h => h.GetGpuForCodec(It.IsAny<VideoCodecType>())).Returns(value: (GpuDevice?)null);

        Mock<ICodecResolver> codecResolver = new();
        codecResolver
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
            codecResolver: codecResolver.Object,
            hardware: hardware.Object,
            tonemapSelector: new TonemapSelector(),
            ffmpegCapabilities: new Mock<IFfmpegCapabilities>().Object,
            abrLadderGenerator: new AbrLadderGenerator(),
            cropDetector: cropDetector,
            logger: NullLogger<PlanStage>.Instance
        );
    }

    private static async Task<OutputPlan> RunPlan(PlanStage stage, EncodingProfile profile)
    {
        ValidateInput input = new(Media: BuildMedia(), Profile: profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await stage.ExecuteAsync(input: input, context: context, ct: CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        return success.Value.OutputPlan;
    }

    private static MediaInfo BuildMedia() =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 4_000_000_000,
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
                    BitRateKbps: 6000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static MediaInfo BuildHdrMedia() =>
        new(
            FilePath: "/media/test-hdr.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: 40000,
            FileSizeBytes: 20_000_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "hevc",
                    Width: 3840,
                    Height: 2160,
                    FrameRate: 24.0,
                    BitDepth: 10,
                    PixelFormat: "yuv420p10le",
                    ColorPrimaries: "bt2020",
                    ColorTransfer: "smpte2084",
                    ColorSpace: "bt2020nc",
                    IsDefault: true,
                    BitRateKbps: 35000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static EncodingProfile BuildProfile(bool autoDetectCrop) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Crop Test",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: V2RateControlMode.Crf,
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
                SegmentNameTemplate: "video/{label}",
                PlaylistNameTemplate: "video/{label}/playlist"
            ),
            Audio: [],
            Subtitles: [],
            AutoDetectCrop: autoDetectCrop
        );
}
