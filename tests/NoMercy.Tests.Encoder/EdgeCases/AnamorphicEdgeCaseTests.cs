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
/// Guards anamorphic (non-square-pixel) handling. A source with SAR != 1:1
/// (e.g. a 720x576 DVD at SAR 64:45 for a 16:9 display) scaled by the ladder,
/// which works in square pixels, comes out stretched unless the pixels are
/// squared and SAR reset to 1:1 first (jellyfin #16665). Square-pixel and
/// unknown-SAR sources must be left alone.
/// </summary>
public class AnamorphicEdgeCaseTests
{
    private readonly BuildStage _stage;

    public AnamorphicEdgeCaseTests()
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
    public async Task AnamorphicSource_SquaresPixelsAndResetsSar()
    {
        string filter = await BuildFilterGraph(BuildMediaInfo(720, 576, "64:45"), 1280, 720);

        filter.Should().Contain("setsar=1", "anamorphic output must resolve to square pixels");
        filter
            .Should()
            .Contain(
                "iw*64/45",
                "the width is scaled by the sample aspect ratio to square the pixels"
            );
    }

    [Fact]
    public async Task SquarePixelSource_NoSetSar()
    {
        string filter = await BuildFilterGraph(BuildMediaInfo(1920, 1080, "1:1"), 1280, 720);

        filter.Should().NotContain("setsar=1", "a square-pixel source needs no un-anamorph pass");
    }

    [Fact]
    public async Task UnknownSar_TreatedAsSquare_NoSetSar()
    {
        // ffprobe reports "0:1" when SAR is unknown — treat as square.
        string filter = await BuildFilterGraph(BuildMediaInfo(1920, 1080, "0:1"), 1280, 720);

        filter.Should().NotContain("setsar=1");
    }

    [Fact]
    public void IsAnamorphic_TrueOnlyForKnownNonSquareSar()
    {
        BuildVideoStream("64:45").IsAnamorphic.Should().BeTrue();
        BuildVideoStream("1:1").IsAnamorphic.Should().BeFalse();
        BuildVideoStream("0:1").IsAnamorphic.Should().BeFalse();
        BuildVideoStream(null).IsAnamorphic.Should().BeFalse();
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

    private static VideoStreamInfo BuildVideoStream(string? sar) =>
        new(
            0,
            Codec: "mpeg2video",
            Width: 720,
            Height: 576,
            FrameRate: 25.0,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: null,
            ColorTransfer: null,
            ColorSpace: null,
            IsDefault: true,
            BitRateKbps: 6000,
            SampleAspectRatio: sar
        );

    private static MediaInfo BuildMediaInfo(int width, int height, string? sar) =>
        new(
            "/movies/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            8000,
            4_000_000_000,
            [
                new(
                    0,
                    Codec: "mpeg2video",
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
                    SampleAspectRatio: sar
                ),
            ],
            [],
            [],
            []
        );
}
