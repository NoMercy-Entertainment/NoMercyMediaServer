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

/// <summary>
/// Guards deinterlace handling: an interlaced source scaled without a
/// deinterlace filter produces combing artifacts in the output (jellyfin
/// #4314). The deinterlace must run before the scale, once on the shared
/// source, and must NOT be added for progressive sources.
/// </summary>
public class DeinterlaceEdgeCaseTests
{
    private readonly BuildStage _stage;

    public DeinterlaceEdgeCaseTests()
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

    [Fact]
    public async Task InterlacedSource_ScaledOutput_EmitsDeinterlaceBeforeScale()
    {
        string filter = await BuildFilterGraph(BuildMediaInfo(1920, 1080, "tt"), 1280, 720);

        filter
            .Should()
            .Contain("yadif", "interlaced source scaled to progressive must deinterlace");
        int deintIdx = filter.IndexOf("yadif", StringComparison.Ordinal);
        int scaleIdx = filter.IndexOf("scale=", StringComparison.Ordinal);
        deintIdx
            .Should()
            .BeLessThan(scaleIdx, "deinterlace reconstructs full frames before scaling");
    }

    [Fact]
    public async Task ProgressiveSource_NoDeinterlaceFilter()
    {
        string filter = await BuildFilterGraph(
            BuildMediaInfo(1920, 1080, "progressive"),
            1280,
            720
        );

        filter.Should().NotContain("yadif", "a progressive source must never be deinterlaced");
    }

    [Fact]
    public async Task UnknownFieldOrder_NoDeinterlaceFilter()
    {
        // Absent field_order (null) is treated as progressive — do not deinterlace.
        string filter = await BuildFilterGraph(
            BuildMediaInfo(1920, 1080, null),
            1280,
            720
        );

        filter.Should().NotContain("yadif");
    }

    private async Task<string> BuildFilterGraph(MediaInfo media, int outWidth, int outHeight)
    {
        OutputPlan outputPlan = new(
            OutputFormat.Hls,
            [BuildVideoOutput(outWidth, outHeight, "[v0]")],
            [BuildAudioOutput()],
            [],
            null
        );
        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, media);

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        int idx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        idx.Should().BeGreaterThan(-1, "a scaled output must build a filter graph");
        return commands[0].Arguments[idx + 1];
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

    private static VideoOutputPlan BuildVideoOutput(int width, int height, string mapLabel) =>
        new(
            width,
            height,
            "libx264",
            23,
            4000,
            "medium",
            "high",
            "4.1",
            false,
            "yuv420p",
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

    private static MediaInfo BuildMediaInfo(int width, int height, string? fieldOrder) =>
        new(
            "/movies/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            8000,
            7_200_000_000,
            [
                new(
                    0,
                    Codec: "h264",
                    Width: width,
                    Height: height,
                    FrameRate: 25.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: 6000,
                    FieldOrder: fieldOrder
                ),
            ],
            [],
            [],
            []
        );
}
