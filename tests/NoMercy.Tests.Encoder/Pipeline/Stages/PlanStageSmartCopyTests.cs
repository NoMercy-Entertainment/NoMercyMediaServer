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
using LadderConfig = NoMercy.Encoder.Profiles.LadderConfig;
using LadderMode = NoMercy.Encoder.Profiles.LadderMode;
using LadderRung = NoMercy.Encoder.Profiles.LadderRung;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// End-to-end coverage for the smart-copy downgrade in
/// <see cref="PlanStage.ApplySmartCopyDowngrade"/>: a source stream that
/// already satisfies a Transcode <see cref="NoMercy.Encoder.Profiles.VideoOutput"/>
/// losslessly should route through Copy (<c>-c:v copy</c>) instead of
/// re-encoding.
/// </summary>
public class PlanStageSmartCopyTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly PlanStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public PlanStageSmartCopyTests()
    {
        _hardware.Setup(h => h.HasGpu).Returns(false);
        _hardware.Setup(h => h.CpuCores).Returns(8);
        _hardware.Setup(h => h.Gpus).Returns([]);
        _hardware.Setup(h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(false);
        _hardware
            .Setup(h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns((GpuDevice?)null);

        _codecResolver
            .Setup(r =>
                r.Resolve(
                    VideoCodecType.Copy,
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(BuildResolvedCodec("copy", supports10Bit: true));

        _codecResolver
            .Setup(r =>
                r.Resolve(
                    VideoCodecType.H265,
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(BuildResolvedCodec("libx265", supports10Bit: true));

        _codecResolver
            .Setup(r =>
                r.Resolve(
                    VideoCodecType.H264,
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(BuildResolvedCodec("libx264", supports10Bit: false));

        _stage = new(
            new(),
            new(),
            new(),
            _codecResolver.Object,
            _hardware.Object,
            new TonemapSelector(),
            new Mock<IFfmpegCapabilities>().Object,
            new AbrLadderGenerator(),
            new NoOpCropDetector(),
            NullLogger<PlanStage>.Instance
        );
    }

    private static ResolvedCodec BuildResolvedCodec(string ffmpegName, bool supports10Bit) =>
        new(
            FfmpegEncoderName: ffmpegName,
            EncoderInfo: new(
                FfmpegName: ffmpegName,
                RequiredVendor: null,
                Presets: ffmpegName == "copy" ? [] : ["medium"],
                Profiles: ffmpegName == "copy" ? [] : ["main", "main10"],
                Levels: ffmpegName == "copy" ? [] : ["5.1"],
                QualityRange: new(0, 51, 23),
                SupportedRateControl: [RateControlMode.Crf],
                Supports10Bit: supports10Bit,
                SupportsHdr: supports10Bit,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "yuv420p10le",
                VendorSpecificFlags: new()
            ),
            Device: null,
            DefaultRateControl: RateControlMode.Crf
        );

    private static MediaInfo BuildHevcMain10Media(
        int width = 1920,
        int height = 1080,
        int bitDepth = 10,
        bool hdr = false
    ) =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(2),
            OverallBitRateKbps: 16000,
            FileSizeBytes: 14_400_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "hevc",
                    Width: width,
                    Height: height,
                    FrameRate: 24.0,
                    BitDepth: bitDepth,
                    PixelFormat: bitDepth >= 10 ? "yuv420p10le" : "yuv420p",
                    ColorPrimaries: hdr ? "bt2020" : "bt709",
                    ColorTransfer: hdr ? "smpte2084" : "bt709",
                    ColorSpace: hdr ? "bt2020nc" : "bt709",
                    IsDefault: true,
                    BitRateKbps: 12000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static NoMercy.Encoder.Profiles.VideoOutput HevcMain10Video(
        int bitDepth = 10,
        bool convertHdrToSdr = false
    ) =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: VideoCodecType.H265,
            Width: 1920,
            Height: 1080,
            RateControl: V2RateControlMode.Crf,
            Crf: 23,
            BitrateKbps: 6000,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: "medium",
            CodecProfile: CodecProfile.Auto,
            Level: null,
            Tune: null,
            BitDepth: bitDepth,
            PixelFormat: null,
            KeyframeIntervalSeconds: 2,
            ConvertHdrToSdr: convertHdrToSdr,
            SegmentNameTemplate: "video/{label}",
            PlaylistNameTemplate: "video/{label}/playlist"
        );

    // GenerateSpriteVtt defaults true on every profile (see HlsDerivatives) and
    // ThumbnailPlanBuilder builds a filtergraph-based sprite plan for any
    // Transcode video output that doesn't opt out — a plan the smart-copy
    // downgrade must never coexist with (see the ApplySmartCopyDowngrade
    // thumbnail guard). Every fixture in this file that expects a downgrade
    // opts out explicitly so the guard doesn't mask what's under test.
    private static EncodingProfile BuildProfile(
        NoMercy.Encoder.Profiles.VideoOutput video,
        Container container = Container.HlsFmp4
    ) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "SmartCopy",
            Container: container,
            Video: video,
            Audio: [],
            Subtitles: [],
            HlsDerivatives: new NoMercy.Encoder.Profiles.HlsDerivatives
            {
                GenerateSpriteVtt = false,
            }
        );

    [Fact]
    public async Task HevcMain10SourceMatchingHlsFmp4Profile_DowngradesToCopy()
    {
        MediaInfo media = BuildHevcMain10Media();
        EncodingProfile profile = BuildProfile(HevcMain10Video());

        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        VideoOutputPlan video = Assert.Single(success.Value.OutputPlan.VideoOutputs);
        video.MapLabel.Should().Be("0:v:0");
        video.EncoderName.Should().Be("copy");
    }

    [Fact]
    public async Task HevcMain10SourceAgainstHlsTsContainer_StaysTranscode()
    {
        // HlsTs only carries H.264 — ContainerCompatibility.SupportsVideo blocks
        // the downgrade even though codec/res/bitrate/bit-depth all match.
        MediaInfo media = BuildHevcMain10Media();
        EncodingProfile profile = BuildProfile(HevcMain10Video(), Container.HlsTs);

        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        VideoOutputPlan video = Assert.Single(success.Value.OutputPlan.VideoOutputs);
        video.MapLabel.Should().Be("[v0]");
        video.EncoderName.Should().Be("libx265");
    }

    [Fact]
    public async Task HevcSourceAgainstH264Profile_StaysTranscode()
    {
        MediaInfo media = BuildHevcMain10Media(bitDepth: 8);
        NoMercy.Encoder.Profiles.VideoOutput h264Video = new(
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
            CodecProfile: CodecProfile.Auto,
            Level: null,
            Tune: null,
            BitDepth: 8,
            PixelFormat: null,
            KeyframeIntervalSeconds: 2,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: "video/{label}",
            PlaylistNameTemplate: "video/{label}/playlist"
        );
        EncodingProfile profile = BuildProfile(h264Video);

        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        VideoOutputPlan video = Assert.Single(success.Value.OutputPlan.VideoOutputs);
        video.MapLabel.Should().Be("[v0]");
        video.EncoderName.Should().Be("libx264");
    }

    [Fact]
    public async Task HdrSourceWithConvertHdrToSdrProfile_StaysTranscode()
    {
        MediaInfo media = BuildHevcMain10Media(hdr: true);
        EncodingProfile profile = BuildProfile(HevcMain10Video(convertHdrToSdr: true));

        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        VideoOutputPlan video = Assert.Single(success.Value.OutputPlan.VideoOutputs);
        video.MapLabel.Should().Be("[v0]");
        video.EncoderName.Should().Be("libx265");
    }

    [Fact]
    public async Task TenBitSourceAgainstEightBitProfile_StaysTranscode()
    {
        MediaInfo media = BuildHevcMain10Media();
        EncodingProfile profile = BuildProfile(HevcMain10Video(bitDepth: 8));

        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        VideoOutputPlan video = Assert.Single(success.Value.OutputPlan.VideoOutputs);
        video.MapLabel.Should().Be("[v0]");
        video.EncoderName.Should().Be("libx265");
    }

    [Fact]
    public async Task Ladder_SourceResolutionRungCopies_LowerRungsStayTranscode()
    {
        MediaInfo media = BuildHevcMain10Media();
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "SmartCopyLadder",
            Container: Container.HlsFmp4,
            Video: HevcMain10Video(),
            Audio: [],
            Subtitles: [],
            HlsDerivatives: new NoMercy.Encoder.Profiles.HlsDerivatives
            {
                GenerateSpriteVtt = false,
            },
            Ladder: new LadderConfig
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new LadderRung(
                        Width: 1920,
                        Height: 1080,
                        Codec: VideoCodecType.H265,
                        BitrateKbps: 6000,
                        MaxBitrateKbps: 9000,
                        BufferSizeKbps: 12000,
                        Framerate: 24.0,
                        Preset: "medium",
                        CodecProfile: CodecProfile.Auto,
                        BitDepth: 10,
                        PixelFormat: "yuv420p10le"
                    ),
                    new LadderRung(
                        Width: 1280,
                        Height: 720,
                        Codec: VideoCodecType.H265,
                        BitrateKbps: 3000,
                        MaxBitrateKbps: 4500,
                        BufferSizeKbps: 6000,
                        Framerate: 24.0,
                        Preset: "medium",
                        CodecProfile: CodecProfile.Auto,
                        BitDepth: 10,
                        PixelFormat: "yuv420p10le"
                    ),
                ],
            }
        );

        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        success.Value.OutputPlan.VideoOutputs.Should().HaveCount(2);
        success.Value.OutputPlan.VideoOutputs[0].MapLabel.Should().Be("0:v:0");
        success.Value.OutputPlan.VideoOutputs[0].EncoderName.Should().Be("copy");
        success.Value.OutputPlan.VideoOutputs[1].MapLabel.Should().Be("[v1]");
        success.Value.OutputPlan.VideoOutputs[1].EncoderName.Should().Be("libx265");
    }
}
