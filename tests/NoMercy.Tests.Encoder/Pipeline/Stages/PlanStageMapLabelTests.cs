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
            .Returns(BuildSoftwareH264Codec());

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

    private static ResolvedCodec BuildSoftwareH264Codec() =>
        new(
            "libx264",
            new(
                "libx264",
                null,
                ["slow", "medium", "fast"],
                ["high"],
                ["4.1"],
                new(0, 51, 23),
                [RateControlMode.Crf, RateControlMode.Cbr],
                false,
                false,
                int.MaxValue,
                "yuv420p10le",
                new()
            ),
            null,
            RateControlMode.Crf
        );

    private static MediaInfo BuildMediaInfo(int width = 1920, int height = 1080) =>
        new(
            "/movies/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            8000,
            7_200_000_000,
            [
                new(
                    0,
                    "h264",
                    width,
                    height,
                    24.0,
                    8,
                    "yuv420p",
                    null,
                    null,
                    null,
                    true,
                    // Below every profile's target bitrate in this file so the
                    // smart-copy downgrade (PlanStage.ApplySmartCopyDowngrade)
                    // never fires here — these tests exist to pin MapLabel
                    // bracket-vs-direct format for genuine Transcode outputs,
                    // not to double as smart-copy fixtures.
                    3000
                ),
            ],
            [
                new(
                    1,
                    "aac",
                    2,
                    48000,
                    192,
                    "en",
                    true,
                    false
                ),
            ],
            [],
            []
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
            Ulid.NewUlid(),
            "SameRes",
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
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    ["en"],
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            []
        );

        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageSuccess<ExecutionPlan>>();
        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;

        plan.OutputPlan.VideoOutputs.Should().HaveCount(1);
        plan.OutputPlan.VideoOutputs[0].MapLabel.Should().Be("[v0]");
    }

    // ------------------------------------------------------------------
    // Test 6: two video outputs → [v0] and [v1]
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuildOutputPlan_MultipleVideos_UsesIncrementingLabels()
    {
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            Name: "ABR",
            Container: Container.HlsTs,
            Video: null,
            Audio:
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    ["en"],
                    null,
                    null,
                    null,
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles: [],
            Ladder: new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new(
                        1920,
                        1080,
                        VideoCodecType.H264,
                        4000,
                        6000,
                        8000,
                        24.0,
                        "medium",
                        CodecProfile.High,
                        8,
                        "yuv420p"
                    ),
                    new(
                        1280,
                        720,
                        VideoCodecType.H264,
                        2500,
                        3750,
                        5000,
                        24.0,
                        "medium",
                        CodecProfile.High,
                        8,
                        "yuv420p"
                    ),
                ],
            }
        );

        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageSuccess<ExecutionPlan>>();
        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;

        plan.OutputPlan.VideoOutputs.Should().HaveCount(2);
        plan.OutputPlan.VideoOutputs[0].MapLabel.Should().Be("[v0]");
        plan.OutputPlan.VideoOutputs[1].MapLabel.Should().Be("[v1]");
    }
}
