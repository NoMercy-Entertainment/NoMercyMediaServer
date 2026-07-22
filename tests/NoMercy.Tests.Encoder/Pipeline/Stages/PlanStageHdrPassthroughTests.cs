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

public class PlanStageHdrPassthroughTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly Mock<IFfmpegCapabilities> _ffmpegCapabilities = new();
    private readonly PlanStage _stage;

    public PlanStageHdrPassthroughTests()
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
                    FfmpegEncoderName: "libx265",
                    EncoderInfo: new(
                        FfmpegName: "libx265",
                        RequiredVendor: null,
                        Presets: ["medium"],
                        Profiles: ["main10"],
                        Levels: ["5.1"],
                        QualityRange: new(Min: 0, Max: 51, Default: 28),
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
    public async Task HdrSource_TenBitKeepHdr_EmitsColorMetadataFlags()
    {
        MediaInfo media = BuildHdrMedia(transfer: "smpte2084");
        EncodingProfile profile = BuildProfile(tenBit: true, convertHdrToSdr: false);

        OutputPlan plan = await RunPlan(media: media, profile: profile);

        VideoOutputPlan video = Assert.Single(collection: plan.VideoOutputs);
        Assert.Equal(expected: "bt2020", actual: video.ExtraFlags[key: "-color_primaries"]);
        Assert.Equal(expected: "smpte2084", actual: video.ExtraFlags[key: "-color_trc"]);
        Assert.Contains(expected: "-colorspace", collection: video.ExtraFlags.Keys);
        Assert.Equal(expected: "tv", actual: video.ExtraFlags[key: "-color_range"]);
    }

    [Fact]
    public async Task HdrSource_HlgTransfer_PreservesHlgTag()
    {
        MediaInfo media = BuildHdrMedia(transfer: "arib-std-b67");
        EncodingProfile profile = BuildProfile(tenBit: true, convertHdrToSdr: false);

        OutputPlan plan = await RunPlan(media: media, profile: profile);

        VideoOutputPlan video = Assert.Single(collection: plan.VideoOutputs);
        Assert.Equal(expected: "arib-std-b67", actual: video.ExtraFlags[key: "-color_trc"]);
    }

    [Fact]
    public async Task HdrSource_ConvertToSdr_DoesNotEmitBt2020()
    {
        MediaInfo media = BuildHdrMedia(transfer: "smpte2084");
        EncodingProfile profile = BuildProfile(tenBit: false, convertHdrToSdr: true);

        OutputPlan plan = await RunPlan(media: media, profile: profile);

        VideoOutputPlan video = Assert.Single(collection: plan.VideoOutputs);
        Assert.DoesNotContain(expected: "-color_primaries", collection: video.ExtraFlags.Keys);
        Assert.DoesNotContain(expected: "-color_trc", collection: video.ExtraFlags.Keys);
    }

    [Fact]
    public async Task SdrSource_NoColorMetadataAdded()
    {
        MediaInfo media = BuildSdrMedia();
        EncodingProfile profile = BuildProfile(tenBit: false, convertHdrToSdr: false);

        OutputPlan plan = await RunPlan(media: media, profile: profile);

        VideoOutputPlan video = Assert.Single(collection: plan.VideoOutputs);
        Assert.DoesNotContain(expected: "-color_primaries", collection: video.ExtraFlags.Keys);
    }

    [Fact]
    public async Task HdrSource_TenBitButExplicitSdrConversion_NoPassthrough()
    {
        // TenBit=true with ConvertHdrToSdr=true is a contradiction, but guard for it:
        // explicit SDR conversion wins.
        MediaInfo media = BuildHdrMedia(transfer: "smpte2084");
        EncodingProfile profile = BuildProfile(tenBit: true, convertHdrToSdr: true);

        OutputPlan plan = await RunPlan(media: media, profile: profile);

        VideoOutputPlan video = Assert.Single(collection: plan.VideoOutputs);
        Assert.DoesNotContain(expected: "-color_primaries", collection: video.ExtraFlags.Keys);
    }

    private async Task<OutputPlan> RunPlan(MediaInfo media, EncodingProfile profile)
    {
        ValidateInput input = new(Media: media, Profile: profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await _stage.ExecuteAsync(input: input, context: context, ct: CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        return success.Value.OutputPlan;
    }

    private static MediaInfo BuildHdrMedia(string transfer) =>
        new(
            FilePath: "/media/hdr.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: 50000,
            FileSizeBytes: 30_000_000_000,
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
                    ColorTransfer: transfer,
                    ColorSpace: "bt2020nc",
                    IsDefault: true,
                    BitRateKbps: 45000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static MediaInfo BuildSdrMedia() =>
        new(
            FilePath: "/media/sdr.mkv",
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
            Chapters: []
        );

    private static EncodingProfile BuildProfile(bool tenBit, bool convertHdrToSdr) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "HDR Test",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H265,
                Width: 3840,
                Height: 2160,
                RateControl: V2RateControlMode.Crf,
                Crf: 22,
                BitrateKbps: 20000,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "medium",
                CodecProfile: CodecProfile.Main10,
                Level: "5.1",
                Tune: null,
                BitDepth: tenBit ? 10 : 8,
                PixelFormat: tenBit ? "yuv420p10le" : null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: convertHdrToSdr,
                SegmentNameTemplate: "video/{label}",
                PlaylistNameTemplate: "video/{label}/playlist"
            ),
            Audio: [],
            Subtitles: []
        );
}
