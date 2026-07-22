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
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// 3D stereo_mode preservation on stream-copy: PlanStage used to splice the
/// flag and its key=value pair together into one dictionary key
/// ("-metadata:s:v stereo_mode"), which FfmpegCommandBuilder emits as a
/// single argv token containing a literal space — ffmpeg never sees the two
/// separate arguments it expects.
/// </summary>
public class PlanStageStereoModeTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly Mock<IFfmpegCapabilities> _ffmpegCapabilities = new();
    private readonly PlanStage _stage;

    public PlanStageStereoModeTests()
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
                    FfmpegEncoderName: "copy",
                    EncoderInfo: new(
                        FfmpegName: "copy",
                        RequiredVendor: null,
                        Presets: [],
                        Profiles: [],
                        Levels: [],
                        QualityRange: new(Min: 0, Max: 51, Default: 23),
                        SupportedRateControl: [RateControlMode.Crf],
                        Supports10Bit: true,
                        SupportsHdr: true,
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
            ffmpegCapabilities: _ffmpegCapabilities.Object,
            abrLadderGenerator: new AbrLadderGenerator(),
            cropDetector: new NoOpCropDetector(),
            logger: NullLogger<PlanStage>.Instance
        );
    }

    [Fact]
    public async Task CopyModeWithStereoSource_EmitsMetadataFlagAsProperKeyValuePair()
    {
        EncodingProfile profile = BuildCopyProfile();
        MediaInfo media = BuildStereoMedia();

        ValidateInput input = new(Media: media, Profile: profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await _stage.ExecuteAsync(input: input, context: context, ct: CancellationToken.None);
        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        OutputPlan plan = success.Value.OutputPlan;

        VideoOutputPlan video = Assert.Single(collection: plan.VideoOutputs);
        video.ExtraFlags.Should().NotContainKey(unexpected: "-metadata:s:v stereo_mode");
        video
            .ExtraFlags.Should()
            .ContainKey(expected: "-metadata:s:v")
            .WhoseValue.Should()
            .Be(expected: "stereo_mode=side_by_side_left");
    }

    private static EncodingProfile BuildCopyProfile() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Stereo Copy Test",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Copy,
                Codec: VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: V2RateControlMode.Crf,
                Crf: 23,
                BitrateKbps: 5000,
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
            Subtitles: []
        );

    private static MediaInfo BuildStereoMedia() =>
        new(
            FilePath: "/media/stereo.mkv",
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
                    ColorPrimaries: "bt709",
                    ColorTransfer: "bt709",
                    ColorSpace: "bt709",
                    IsDefault: true,
                    BitRateKbps: 6000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: [],
            StereoMode: "side_by_side_left"
        );
}
