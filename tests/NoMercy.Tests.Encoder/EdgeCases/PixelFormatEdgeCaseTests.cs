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
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Tests.Encoder.Pipeline.Stages;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.EdgeCases;

public class PixelFormatEdgeCaseTests
{
    private readonly BuildStage _stage;

    public PixelFormatEdgeCaseTests()
    {
        EncoderOptions options = new() { FfmpegPathOverride = "ffmpeg" };
        _stage = new(
            options,
            new FontExtractor(TestStorageFactory.CreateLocal()),
            new SubtitleExtractor(),
            OutputStrategyFactoryTestHelper.Create(),
            [],
            NullLogger<BuildStage>.Instance,
            TestStorageFactory.CreateLocal()
        );
    }

    private static ExecutionPlan BuildPlan(OutputPlan outputPlan) =>
        new(
            [
                new(
                    "group_0",
                    [new("decode_0", OperationType.Decode, [], new())],
                    null,
                    0,
                    4,
                    false,
                    1
                ),
            ],
            TimeSpan.FromMinutes(90),
            outputPlan
        );

    private static VideoOutputPlan BuildVideoOutput(
        int width,
        int height,
        string mapLabel,
        string encoder = "libx264",
        bool tenBit = false,
        string pixelFormat = "yuv420p"
    ) =>
        new(
            width,
            height,
            encoder,
            23,
            4000,
            "medium",
            tenBit ? "high10" : "high",
            "4.1",
            tenBit,
            pixelFormat,
            mapLabel,
            new()
        );

    private static AudioOutputPlan BuildAudioOutput() =>
        new(
            "aac",
            192,
            2,
            48000,
            StreamAction.Transcode,
            "en",
            "0:a:0"
        );

    private static MediaInfo Build10BitMediaInfo(int width = 3840, int height = 2160) =>
        new(
            "/movies/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            50000,
            30_000_000_000,
            [
                new(
                    0,
                    "hevc",
                    width,
                    height,
                    24.0,
                    10,
                    "yuv420p10le",
                    "bt709",
                    "bt709",
                    "bt709",
                    true,
                    45000
                ),
            ],
            [],
            [],
            []
        );

    private static MediaInfo Build8BitMediaInfo(int width = 1920, int height = 1080) =>
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

    [Fact]
    public async Task BuildStage_10BitSourceTo8BitProfile_OutputPixelFormatIsEightBit()
    {
        VideoOutputPlan output = BuildVideoOutput(
            1280,
            720,
            "[v0]",
            "libx264",
            false,
            "yuv420p"
        );

        OutputPlan outputPlan = new(
            OutputFormat.Hls,
            [output],
            [BuildAudioOutput()],
            [],
            null
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            Build10BitMediaInfo(3840, 2160)
        );

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        int filterComplexIdx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        filterComplexIdx
            .Should()
            .BeGreaterThan(
                -1,
                "scaling from 10-bit source to 8-bit target requires filter_complex"
            );

        string filterValue = commands[0].Arguments[filterComplexIdx + 1];
        filterValue
            .Should()
            .Contain(
                "format=yuv420p",
                "8-bit target must output yuv420p, not 10-bit p010 or p010le"
            );
        filterValue.Should().NotContain("p010", "8-bit target must not output 10-bit pixel format");
    }

    [Fact]
    public async Task BuildStage_OutputWithOddDimensions_MakeDimensionsEven()
    {
        VideoOutputPlan output = BuildVideoOutput(
            1279,
            719,
            "[v0]",
            "libx264",
            false,
            "yuv420p"
        );

        OutputPlan outputPlan = new(
            OutputFormat.Hls,
            [output],
            [BuildAudioOutput()],
            [],
            null
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            Build8BitMediaInfo(1920, 1080)
        );

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        int filterComplexIdx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        filterComplexIdx.Should().BeGreaterThan(-1, "filter_complex must be present when scaling");

        string filterValue = commands[0].Arguments[filterComplexIdx + 1];

        filterValue
            .Should()
            .Contain("scale=1279:-2", "requested 1279 width must be preserved in scale filter");

        filterValue.Should().NotContain("1279:719", "odd dimensions should not be preserved as-is");
    }
}
