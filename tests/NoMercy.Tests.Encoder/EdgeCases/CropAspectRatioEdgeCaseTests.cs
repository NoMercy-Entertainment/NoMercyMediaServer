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

namespace NoMercy.Tests.Encoder.EdgeCases;

public class CropAspectRatioEdgeCaseTests
{
    [Fact]
    public async Task CropFilter_WhenDetected_PopulatesOnAllOutputs()
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
    }

    [Fact]
    public async Task CropDisabled_CropFilterNull()
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
        EncodingProfile profile = BuildProfile(false);
        OutputPlan plan = await RunPlan(stage, profile);

        plan.VideoOutputs.Should().HaveCountGreaterThan(0);
        plan.VideoOutputs[0]
            .CropFilter.Should()
            .BeNull("when auto-detect crop is disabled, no crop filter should be applied");
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
