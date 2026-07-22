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
        PlanStage stage = BuildStage(acquisitionService: svc.Object);

        EncodingProfile profile = BuildProfile(
            acquisition: new() { Enabled = false, Languages = ["en"] }
        );

        OutputPlan plan = await RunPlan(stage: stage, profile: profile);

        plan.AcquiredSubtitles.Should().BeEmpty();
        svc.Verify(
            expression: s => s.AcquireAsync(It.IsAny<AcquisitionRequest>(), It.IsAny<CancellationToken>()),
            times: Times.Never
        );
    }

    // Test 2: Acquisition enabled → service called, results populate OutputPlan.AcquiredSubtitles
    [Fact]
    public async Task AcquisitionEnabled_ServiceCalled_ResultsInOutputPlan()
    {
        AcquiredSubtitle acquired = new(
            Language: "en",
            LocalPath: "/tmp/subs/en.srt",
            Provider: "OpenSubtitles",
            IsExactMatch: true,
            Rating: 8.0,
            Downloads: 1000,
            Format: "srt"
        );

        Mock<ISubtitleAcquisitionService> svc = new();
        svc.Setup(expression: s =>
                s.AcquireAsync(It.IsAny<AcquisitionRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: [acquired]);

        PlanStage stage = BuildStage(acquisitionService: svc.Object);
        EncodingProfile profile = BuildProfile(
            acquisition: new()
            {
                Enabled = true,
                Languages = ["en"],
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        OutputPlan plan = await RunPlan(stage: stage, profile: profile);

        plan.AcquiredSubtitles.Should().HaveCount(expected: 1);
        plan.AcquiredSubtitles[index: 0].Language.Should().Be(expected: "en");
        plan.AcquiredSubtitles[index: 0].IsExactMatch.Should().BeTrue();
        svc.Verify(
            expression: s => s.AcquireAsync(It.IsAny<AcquisitionRequest>(), It.IsAny<CancellationToken>()),
            times: Times.Once
        );
    }

    // Test 3: Service throws → PlanStage still succeeds, AcquiredSubtitles is empty
    [Fact]
    public async Task ServiceThrows_PlanSucceeds_AcquiredSubtitlesEmpty()
    {
        Mock<ISubtitleAcquisitionService> svc = new();
        svc.Setup(expression: s =>
                s.AcquireAsync(It.IsAny<AcquisitionRequest>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "provider down"));

        PlanStage stage = BuildStage(acquisitionService: svc.Object);
        EncodingProfile profile = BuildProfile(
            acquisition: new()
            {
                Enabled = true,
                Languages = ["en"],
                Strategy = SubtitleMatchStrategy.HashOnly,
            }
        );

        StageResult result = await RunPlanRaw(stage: stage, profile: profile);

        result.Should().BeOfType<StageSuccess<ExecutionPlan>>();
        StageSuccess<ExecutionPlan> success = (StageSuccess<ExecutionPlan>)result;
        success.Value.OutputPlan.AcquiredSubtitles.Should().BeEmpty();
    }

    private static PlanStage BuildStage(ISubtitleAcquisitionService? acquisitionService = null)
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
                        SupportedRateControl: [EncoderRateControlMode.Crf],
                        Supports10Bit: false,
                        SupportsHdr: false,
                        MaxConcurrentSessions: int.MaxValue,
                        PixelFormat10Bit: "yuv420p10le",
                        VendorSpecificFlags: new()
                    ),
                    Device: null,
                    DefaultRateControl: EncoderRateControlMode.Crf
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
            cropDetector: new Mock<ICropDetector>().Object,
            logger: NullLogger<PlanStage>.Instance,
            subtitleAcquisitionService: acquisitionService
        );
    }

    private static async Task<OutputPlan> RunPlan(PlanStage stage, EncodingProfile profile)
    {
        StageResult result = await RunPlanRaw(stage: stage, profile: profile);
        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        return success.Value.OutputPlan;
    }

    private static async Task<StageResult> RunPlanRaw(PlanStage stage, EncodingProfile profile)
    {
        ValidateInput input = new(Media: BuildMedia(), Profile: profile);
        EncodingContext context = EncodingContext.Create();
        return await stage.ExecuteAsync(input: input, context: context, ct: CancellationToken.None);
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

    private static EncodingProfile BuildProfile(SubtitleAcquisitionConfig? acquisition = null)
    {
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "Acquisition Test",
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
            Subtitles:
            [
                new(
                    Policy: SubtitlePolicy.Extract,
                    Codec: SubtitleCodecType.WebVtt,
                    AllowedLanguages: ["en"],
                    IncludeForced: false,
                    OcrLanguage: null,
                    PlaylistNameTemplate: "subtitles/:filename:.:language:.:variant:"
                ),
            ]
        );

        if (acquisition is not null)
            profile = profile with { SubtitleAcquisition = acquisition };

        return profile;
    }
}
