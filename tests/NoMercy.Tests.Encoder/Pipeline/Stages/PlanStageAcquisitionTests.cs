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
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Subtitles;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncoderRateControlMode = NoMercy.Encoder.Codecs.RateControlMode;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class PlanStageAcquisitionTests
{
    // Test 1: Acquisition disabled → service not called, OutputPlan.AcquiredSubtitles is empty
    [Fact]
    public async Task AcquisitionDisabled_ServiceNotCalled_AcquiredSubtitlesEmpty()
    {
        Mock<ISubtitleAcquisitionService> svc = new();
        PlanStage stage = BuildStage(svc.Object);

        EncodingProfile profile = BuildProfile(
            new() { Enabled = false, Languages = ["en"] }
        );

        OutputPlan plan = await RunPlan(stage, profile);

        plan.AcquiredSubtitles.Should().BeEmpty();
        svc.Verify(
            s => s.AcquireAsync(It.IsAny<AcquisitionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // Test 2: Acquisition enabled → service called, results populate OutputPlan.AcquiredSubtitles
    [Fact]
    public async Task AcquisitionEnabled_ServiceCalled_ResultsInOutputPlan()
    {
        AcquiredSubtitle acquired = new(
            "en",
            "/tmp/subs/en.srt",
            "OpenSubtitles",
            true,
            8.0,
            1000,
            "srt"
        );

        Mock<ISubtitleAcquisitionService> svc = new();
        svc.Setup(s =>
                s.AcquireAsync(It.IsAny<AcquisitionRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([acquired]);

        PlanStage stage = BuildStage(svc.Object);
        EncodingProfile profile = BuildProfile(
            new()
            {
                Enabled = true,
                Languages = ["en"],
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        OutputPlan plan = await RunPlan(stage, profile);

        plan.AcquiredSubtitles.Should().HaveCount(1);
        plan.AcquiredSubtitles[0].Language.Should().Be("en");
        plan.AcquiredSubtitles[0].IsExactMatch.Should().BeTrue();
        svc.Verify(
            s => s.AcquireAsync(It.IsAny<AcquisitionRequest>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // Test 3: Service throws → PlanStage still succeeds, AcquiredSubtitles is empty
    [Fact]
    public async Task ServiceThrows_PlanSucceeds_AcquiredSubtitlesEmpty()
    {
        Mock<ISubtitleAcquisitionService> svc = new();
        svc.Setup(s =>
                s.AcquireAsync(It.IsAny<AcquisitionRequest>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new InvalidOperationException("provider down"));

        PlanStage stage = BuildStage(svc.Object);
        EncodingProfile profile = BuildProfile(
            new()
            {
                Enabled = true,
                Languages = ["en"],
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        StageResult result = await RunPlanRaw(stage, profile);

        result.Should().BeOfType<StageSuccess<ExecutionPlan>>();
        StageSuccess<ExecutionPlan> success = (StageSuccess<ExecutionPlan>)result;
        success.Value.OutputPlan.AcquiredSubtitles.Should().BeEmpty();
    }

    private static PlanStage BuildStage(ISubtitleAcquisitionService? acquisitionService = null)
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
                        [EncoderRateControlMode.Crf],
                        false,
                        false,
                        int.MaxValue,
                        "yuv420p10le",
                        new()
                    ),
                    null,
                    EncoderRateControlMode.Crf
                )
            );

        return new(
            new(),
            groupingStrategy: new(),
            costEstimator: new(),
            codecResolver: codecResolver.Object,
            hardware: hardware.Object,
            tonemapSelector: new TonemapSelector(),
            ffmpegCapabilities: new Mock<IFfmpegCapabilities>().Object,
            abrLadderGenerator: new AbrLadderGenerator(),
            cropDetector: new Mock<ICropDetector>().Object,
            logger: NullLogger<PlanStage>.Instance,
            subtitleAcquisitionService: acquisitionService
        );
    }

    private static async Task<OutputPlan> RunPlan(PlanStage stage, EncodingProfile profile)
    {
        StageResult result = await RunPlanRaw(stage, profile);
        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        return success.Value.OutputPlan;
    }

    private static async Task<StageResult> RunPlanRaw(PlanStage stage, EncodingProfile profile)
    {
        ValidateInput input = new(BuildMedia(), profile);
        EncodingContext context = EncodingContext.Create();
        return await stage.ExecuteAsync(input, context, CancellationToken.None);
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

    private static EncodingProfile BuildProfile(SubtitleAcquisitionConfig? acquisition = null)
    {
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            "Acquisition Test",
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
                "video/{label}",
                "video/{label}/playlist"
            ),
            [],
            [
                new(
                    SubtitlePolicy.Extract,
                    SubtitleCodecType.WebVtt,
                    ["en"],
                    false,
                    null,
                    "subtitles/:filename:.:language:.:variant:"
                ),
            ]
        );

        if (acquisition is not null)
            profile = profile with { SubtitleAcquisition = acquisition };

        return profile;
    }
}
