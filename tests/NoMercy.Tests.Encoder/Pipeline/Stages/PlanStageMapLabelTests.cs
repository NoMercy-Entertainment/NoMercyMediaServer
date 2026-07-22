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
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using LadderConfig = NoMercy.Encoder.Profiles.LadderConfig;
using LadderMode = NoMercy.Encoder.Profiles.LadderMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class PlanStageMapLabelTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly PlanStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public PlanStageMapLabelTests()
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
            .Returns(value: BuildSoftwareH264Codec());

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

    private static ResolvedCodec BuildSoftwareH264Codec() =>
        new(
            FfmpegEncoderName: "libx264",
            EncoderInfo: new(
                FfmpegName: "libx264",
                RequiredVendor: null,
                Presets: ["slow", "medium", "fast"],
                Profiles: ["high"],
                Levels: ["4.1"],
                QualityRange: new(Min: 0, Max: 51, Default: 23),
                SupportedRateControl: [RateControlMode.Crf, RateControlMode.Cbr],
                Supports10Bit: false,
                SupportsHdr: false,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "yuv420p10le",
                VendorSpecificFlags: new()
            ),
            Device: null,
            DefaultRateControl: RateControlMode.Crf
        );

    private static MediaInfo BuildMediaInfo(int width = 1920, int height = 1080) =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(hours: 2),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 7_200_000_000,
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
                    // Below every profile's target bitrate in this file so the
                    // smart-copy downgrade (PlanStage.ApplySmartCopyDowngrade)
                    // never fires here — these tests exist to pin MapLabel
                    // bracket-vs-direct format for genuine Transcode outputs,
                    // not to double as smart-copy fixtures.
                    BitRateKbps: 3000
                ),
            ],
            AudioStreams:
            [
                new(
                    Index: 1,
                    Codec: "aac",
                    Channels: 2,
                    SampleRate: 48000,
                    BitRateKbps: 192,
                    Language: "en",
                    IsDefault: true,
                    IsForced: false
                ),
            ],
            SubtitleStreams: [],
            Chapters: []
        );

    // ------------------------------------------------------------------
    // Test 5: single video where input matches output → still uses [v0]
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuildOutputPlan_SingleVideoSameResolution_UsesFilterLabel()
    {
        // Profile output exactly matches source — previously this used "0:v:0", now must use "[v0]"
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "SameRes",
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
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["en"],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "audio/{lang}-{codec}",
                    PlaylistNameTemplate: "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles: []
        );

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        result.Should().BeOfType<StageSuccess<ExecutionPlan>>();
        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;

        plan.OutputPlan.VideoOutputs.Should().HaveCount(expected: 1);
        plan.OutputPlan.VideoOutputs[0].MapLabel.Should().Be(expected: "[v0]");
    }

    // ------------------------------------------------------------------
    // Test 6: two video outputs → [v0] and [v1]
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuildOutputPlan_MultipleVideos_UsesIncrementingLabels()
    {
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "ABR",
            Container: Container.HlsTs,
            Video: null,
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["en"],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "audio/{lang}-{codec}",
                    PlaylistNameTemplate: "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles: [],
            Ladder: new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new(
                        Width: 1920,
                        Height: 1080,
                        Codec: VideoCodecType.H264,
                        BitrateKbps: 4000,
                        MaxBitrateKbps: 6000,
                        BufferSizeKbps: 8000,
                        Framerate: 24.0,
                        Preset: "medium",
                        CodecProfile: CodecProfile.High,
                        BitDepth: 8,
                        PixelFormat: "yuv420p"
                    ),
                    new(
                        Width: 1280,
                        Height: 720,
                        Codec: VideoCodecType.H264,
                        BitrateKbps: 2500,
                        MaxBitrateKbps: 3750,
                        BufferSizeKbps: 5000,
                        Framerate: 24.0,
                        Preset: "medium",
                        CodecProfile: CodecProfile.High,
                        BitDepth: 8,
                        PixelFormat: "yuv420p"
                    ),
                ],
            }
        );

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        result.Should().BeOfType<StageSuccess<ExecutionPlan>>();
        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;

        plan.OutputPlan.VideoOutputs.Should().HaveCount(expected: 2);
        plan.OutputPlan.VideoOutputs[0].MapLabel.Should().Be(expected: "[v0]");
        plan.OutputPlan.VideoOutputs[1].MapLabel.Should().Be(expected: "[v1]");
    }
}
