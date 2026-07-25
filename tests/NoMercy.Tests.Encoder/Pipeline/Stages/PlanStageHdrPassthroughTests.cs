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
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(
                new ResolvedCodec(
                    "libx265",
                    new(
                        "libx265",
                        null,
                        ["medium"],
                        ["main10"],
                        ["5.1"],
                        new(0, 51, 28),
                        [RateControlMode.Crf],
                        true,
                        true,
                        int.MaxValue,
                        "yuv420p10le",
                        new()
                    ),
                    null,
                    RateControlMode.Crf
                )
            );

        _stage = new(
            new(),
            new(),
            new(),
            _codecResolver.Object,
            _hardware.Object,
            new TonemapSelector(),
            _ffmpegCapabilities.Object,
            new AbrLadderGenerator(),
            new NoOpCropDetector(),
            NullLogger<PlanStage>.Instance
        );
    }

    [Fact]
    public async Task HdrSource_TenBitKeepHdr_EmitsColorMetadataFlags()
    {
        MediaInfo media = BuildHdrMedia("smpte2084");
        EncodingProfile profile = BuildProfile(true, false);

        OutputPlan plan = await RunPlan(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        Assert.Equal("bt2020", video.ExtraFlags["-color_primaries"]);
        Assert.Equal("smpte2084", video.ExtraFlags["-color_trc"]);
        Assert.Contains("-colorspace", video.ExtraFlags.Keys);
        Assert.Equal("tv", video.ExtraFlags["-color_range"]);
    }

    [Fact]
    public async Task HdrSource_HlgTransfer_PreservesHlgTag()
    {
        MediaInfo media = BuildHdrMedia("arib-std-b67");
        EncodingProfile profile = BuildProfile(true, false);

        OutputPlan plan = await RunPlan(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        Assert.Equal("arib-std-b67", video.ExtraFlags["-color_trc"]);
    }

    [Fact]
    public async Task HdrSource_ConvertToSdr_DoesNotEmitBt2020()
    {
        MediaInfo media = BuildHdrMedia("smpte2084");
        EncodingProfile profile = BuildProfile(false, true);

        OutputPlan plan = await RunPlan(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        Assert.DoesNotContain("-color_primaries", video.ExtraFlags.Keys);
        Assert.DoesNotContain("-color_trc", video.ExtraFlags.Keys);
    }

    [Fact]
    public async Task SdrSource_NoColorMetadataAdded()
    {
        MediaInfo media = BuildSdrMedia();
        EncodingProfile profile = BuildProfile(false, false);

        OutputPlan plan = await RunPlan(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        Assert.DoesNotContain("-color_primaries", video.ExtraFlags.Keys);
    }

    [Fact]
    public async Task HdrSource_TenBitButExplicitSdrConversion_NoPassthrough()
    {
        // TenBit=true with ConvertHdrToSdr=true is a contradiction, but guard for it:
        // explicit SDR conversion wins.
        MediaInfo media = BuildHdrMedia("smpte2084");
        EncodingProfile profile = BuildProfile(true, true);

        OutputPlan plan = await RunPlan(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        Assert.DoesNotContain("-color_primaries", video.ExtraFlags.Keys);
    }

    private async Task<OutputPlan> RunPlan(MediaInfo media, EncodingProfile profile)
    {
        ValidateInput input = new(media, profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await _stage.ExecuteAsync(input, context, CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        return success.Value.OutputPlan;
    }

    private static MediaInfo BuildHdrMedia(string transfer) =>
        new(
            "/media/hdr.mkv",
            "matroska",
            TimeSpan.FromMinutes(90),
            50000,
            30_000_000_000,
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
                    transfer,
                    "bt2020nc",
                    true,
                    45000
                ),
            ],
            [],
            [],
            []
        );

    private static MediaInfo BuildSdrMedia() =>
        new(
            "/media/sdr.mkv",
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
                    "bt709",
                    "bt709",
                    "bt709",
                    true,
                    6000
                ),
            ],
            [],
            [],
            []
        );

    private static EncodingProfile BuildProfile(bool tenBit, bool convertHdrToSdr) =>
        new(
            Ulid.NewUlid(),
            "HDR Test",
            Container.HlsTs,
            new(
                StreamPolicy.Transcode,
                VideoCodecType.H265,
                3840,
                2160,
                V2RateControlMode.Crf,
                22,
                20000,
                null,
                null,
                "medium",
                CodecProfile.Main10,
                "5.1",
                null,
                tenBit ? 10 : 8,
                tenBit ? "yuv420p10le" : null,
                2,
                convertHdrToSdr,
                "video/{label}",
                "video/{label}/playlist"
            ),
            [],
            []
        );
}
