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
using NoMercy.Encoder.Profiles;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using LadderMode = NoMercy.Encoder.Profiles.LadderMode;
using RateControlMode = NoMercy.Encoder.Codecs.RateControlMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// Pins the HdrPolicy.EmitHdrAndSdr role-split inside PlanStage: for an HDR
/// source the 10-bit rung preserves HDR (passthrough, IsHdrOutput, color tags)
/// while the 8-bit rung carries the tonemapped SDR copy (TonemapFilterChain set,
/// not flagged HDR). An SDR source bypasses the split entirely — every rung is a
/// plain SDR output. The DisambiguateVideo path collision is covered separately.
/// </summary>
public class PlanStageEmitHdrAndSdrTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly Mock<IFfmpegCapabilities> _ffmpegCapabilities = new();
    private readonly PlanStage _stage;

    public PlanStageEmitHdrAndSdrTests()
    {
        _hardware.Setup(h => h.HasGpu).Returns(false);
        _hardware.Setup(h => h.CpuCores).Returns(8);
        _hardware.Setup(h => h.Gpus).Returns([]);
        _hardware.Setup(h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(false);
        _hardware
            .Setup(h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns((GpuDevice?)null);

        // Codec-aware: HEVC resolves to a 10-bit-capable software encoder, H.264
        // to an 8-bit one — so the bit-depth role-split has real encoders behind it.
        _codecResolver
            .Setup(r =>
                r.Resolve(
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(
                (VideoCodecType codec, IHardwareCapabilities _, EncoderPreference _) =>
                    codec == VideoCodecType.H265
                        ? BuildResolved("libx265", true)
                        : BuildResolved("libx264", false)
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
    public async Task HdrSource_SplitsIntoHdrPassthroughAndTonemappedSdr()
    {
        OutputPlan plan = await RunPlan(EmitHdrAndSdrProfile(), BuildHdrMedia());

        plan.VideoOutputs.Should().HaveCount(2);

        VideoOutputPlan hdr = plan.VideoOutputs.Single(v => v.EncoderName == "libx265");
        hdr.IsHdrOutput.Should().BeTrue("the 10-bit HEVC rung preserves HDR");
        hdr.TonemapFilterChain.Should().BeNull("HDR passthrough must not tonemap");
        hdr.ExtraFlags.Should().ContainKey("-color_trc").WhoseValue.Should().Be("smpte2084");

        VideoOutputPlan sdr = plan.VideoOutputs.Single(v => v.EncoderName == "libx264");
        sdr.IsHdrOutput.Should().BeFalse("the 8-bit H.264 rung is the SDR copy");
        sdr.TonemapFilterChain.Should().NotBeNullOrEmpty("the SDR copy must be tonemapped");
        sdr.ConvertHdrToSdr.Should().BeTrue();
    }

    [Fact]
    public async Task SdrSource_BypassesSplit_NoHdrOutputs()
    {
        OutputPlan plan = await RunPlan(EmitHdrAndSdrProfile(), BuildSdrMedia());

        plan.VideoOutputs.Should().HaveCount(2);
        plan.VideoOutputs.Should().OnlyContain(v => !v.IsHdrOutput);
        plan.VideoOutputs.Should().OnlyContain(v => v.TonemapFilterChain == null);
    }

    private async Task<OutputPlan> RunPlan(EncodingProfile profile, MediaInfo media)
    {
        ValidateInput input = new(media, profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await _stage.ExecuteAsync(input, context, CancellationToken.None);
        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        return success.Value.OutputPlan;
    }

    private static EncodingProfile EmitHdrAndSdrProfile() =>
        new(
            Ulid.NewUlid(),
            "EmitHdrAndSdr Test",
            Container.HlsTs,
            ReferenceVideo(),
            [],
            []
        )
        {
            HdrPolicies = HdrPolicies.EmitHdrAndSdr,
            Ladder = new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new(
                        1920,
                        Height: 1080,
                        Codec: VideoCodecType.H265,
                        BitrateKbps: 8000,
                        MaxBitrateKbps: 0,
                        BufferSizeKbps: 0,
                        Framerate: 24.0,
                        BitDepth: 10
                    ),
                    new(
                        1280,
                        Height: 720,
                        Codec: VideoCodecType.H264,
                        BitrateKbps: 3000,
                        MaxBitrateKbps: 0,
                        BufferSizeKbps: 0,
                        Framerate: 24.0,
                        BitDepth: 8
                    ),
                ],
            },
        };

    private static NoMercy.Encoder.Profiles.VideoOutput ReferenceVideo() =>
        new(
            StreamPolicy.Transcode,
            VideoCodecType.H265,
            1920,
            1080,
            V2RateControlMode.Crf,
            23,
            8000,
            null,
            null,
            "medium",
            CodecProfile.Main10,
            null,
            null,
            10,
            null,
            2,
            false,
            "video_:framesize:/:framesize:",
            "video_:framesize:/playlist"
        );

    private static MediaInfo BuildHdrMedia() =>
        BaseMedia() with
        {
            VideoStreams =
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
                    40000
                ),
            ],
        };

    private static MediaInfo BuildSdrMedia() =>
        BaseMedia() with
        {
            VideoStreams =
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
        };

    private static MediaInfo BaseMedia() =>
        new(
            "/media/source.mkv",
            "matroska",
            TimeSpan.FromMinutes(90),
            40000,
            20_000_000_000,
            [],
            [],
            [],
            []
        );

    private static ResolvedCodec BuildResolved(string ffmpegName, bool supports10Bit) =>
        new(
            ffmpegName,
            new(
                ffmpegName,
                null,
                ["medium"],
                ["high", "main10"],
                ["4.1", "5.1"],
                new(0, 51, 23),
                [RateControlMode.Crf],
                supports10Bit,
                supports10Bit,
                int.MaxValue,
                "yuv420p10le",
                new()
            ),
            null,
            RateControlMode.Crf
        );
}
