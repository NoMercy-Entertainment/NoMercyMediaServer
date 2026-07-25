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
        PlanStage stage = BuildStage(detector.Object);

        EncodingProfile profile = BuildProfile(false);
        OutputPlan plan = await RunPlan(stage, profile);

        detector.Verify(
            d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        plan.VideoOutputs[0].CropFilter.Should().BeNull();
    }

    [Fact]
    public async Task AutoDetectCropOn_ShouldCrop_PopulatesCropFilterOnAllOutputs()
    {
        Mock<ICropDetector> detector = new();
        detector
            .Setup(d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new CropResult(1920, 800, 0, 140, true));

        PlanStage stage = BuildStage(detector.Object);
        EncodingProfile profile = BuildProfile(true);

        OutputPlan plan = await RunPlan(stage, profile);

        plan.VideoOutputs.Should().HaveCountGreaterThan(0);
        plan.VideoOutputs[0].CropFilter.Should().Be("1920:800:0:140");
        detector.Verify(
            d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task AutoDetectCropOn_NoCropNeeded_CropFilterNull()
    {
        Mock<ICropDetector> detector = new();
        detector
            .Setup(d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new CropResult(0, 0, 0, 0, false));

        PlanStage stage = BuildStage(detector.Object);
        EncodingProfile profile = BuildProfile(true);

        OutputPlan plan = await RunPlan(stage, profile);

        plan.VideoOutputs[0].CropFilter.Should().BeNull();
    }

    [Fact]
    public async Task AutoDetectCropOn_DetectorThrows_ContinuesWithoutCrop()
    {
        Mock<ICropDetector> detector = new();
        detector
            .Setup(d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("boom"));

        PlanStage stage = BuildStage(detector.Object);
        EncodingProfile profile = BuildProfile(true);

        OutputPlan plan = await RunPlan(stage, profile);

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
            .Setup(d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new CropResult(1920, 1000, 0, 40, true));

        PlanStage stage = BuildStage(detector.Object);
        OutputPlan plan = await RunPlan(stage, BuildProfile(true));

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
            .Setup(d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new CropResult(1700, 1080, 110, 0, true)
            );

        PlanStage stage = BuildStage(detector.Object);
        OutputPlan plan = await RunPlan(stage, BuildProfile(true));

        plan.VideoOutputs[0].CropFilter.Should().Be("1700:1080:110:0");
    }

    [Fact]
    public async Task AutoDetectCropOn_HdrSource_PassesHdrFlagToDetector()
    {
        // The HDR flag must reach the detector so it can pick the HDR cropdetect
        // limit — the whole reason letterbox on HDR sources was being missed.
        bool? capturedHdr = null;
        Mock<ICropDetector> detector = new();
        detector
            .Setup(d =>
                d.DetectAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback((string _, Guid? _, bool? isHdr, CancellationToken _) => capturedHdr = isHdr)
            .ReturnsAsync(new CropResult(0, 0, 0, 0, false));

        PlanStage stage = BuildStage(detector.Object);
        ValidateInput input = new(BuildHdrMedia(), BuildProfile(true));
        await stage.ExecuteAsync(input, EncodingContext.Create(), CancellationToken.None);

        capturedHdr.Should().BeTrue();
    }

    private static PlanStage BuildStage(ICropDetector cropDetector)
    {
        Mock<IHardwareCapabilities> hardware = new();
        hardware.Setup(h => h.HasGpu).Returns(false);
        hardware.Setup(h => h.CpuCores).Returns(8);
        hardware.Setup(h => h.Gpus).Returns([]);
        hardware.Setup(h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(false);
        hardware.Setup(h => h.GetGpuForCodec(It.IsAny<VideoCodecType>())).Returns((GpuDevice?)null);

        Mock<ICodecResolver> codecResolver = new();
        codecResolver
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
            codecResolver.Object,
            hardware.Object,
            new TonemapSelector(),
            new Mock<IFfmpegCapabilities>().Object,
            new AbrLadderGenerator(),
            cropDetector,
            NullLogger<PlanStage>.Instance
        );
    }

    private static async Task<OutputPlan> RunPlan(PlanStage stage, EncodingProfile profile)
    {
        ValidateInput input = new(BuildMedia(), profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await stage.ExecuteAsync(input, context, CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        return success.Value.OutputPlan;
    }

    private static MediaInfo BuildMedia() =>
        new(
            "/media/test.mkv",
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
                    null,
                    null,
                    null,
                    true,
                    6000
                ),
            ],
            [],
            [],
            []
        );

    private static MediaInfo BuildHdrMedia() =>
        new(
            "/media/test-hdr.mkv",
            "matroska",
            TimeSpan.FromMinutes(90),
            40000,
            20_000_000_000,
            [
                new(
                    0,
                    "hevc",
                    3840,
                    2160,
                    24.0,
                    10,
                    "yuv420p10le",
                    "bt2020",
                    "smpte2084",
                    "bt2020nc",
                    true,
                    35000
                ),
            ],
            [],
            [],
            []
        );

    private static EncodingProfile BuildProfile(bool autoDetectCrop) =>
        new(
            Ulid.NewUlid(),
            Name: "Crop Test",
            Container: Container.HlsTs,
            Video: new(
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
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio: [],
            Subtitles: [],
            AutoDetectCrop: autoDetectCrop
        );
}
