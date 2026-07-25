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
/// Guards constant-frame-rate normalization for variable-frame-rate sources.
/// A VFR source muxed into HLS/DASH without -fps_mode cfr drifts its segment
/// durations off the -hls_time/-seg_duration target and desyncs across an ABR
/// switch (jellyfin #10485). CFR sources must be left alone.
/// </summary>
public class VfrEdgeCaseTests
{
    private readonly BuildStage _stage;

    public VfrEdgeCaseTests()
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
    public async Task VfrNormalized_HlsOutput_EmitsFpsModeCfr()
    {
        string[] args = await BuildArgs(OutputFormat.Hls, true);

        int idx = Array.IndexOf(args, "-fps_mode");
        idx.Should()
            .BeGreaterThan(-1, "a VFR source must be muxed at a constant frame rate for HLS");
        args[idx + 1].Should().Be("cfr");
    }

    [Fact]
    public async Task CfrSource_HlsOutput_NoFpsMode()
    {
        string[] args = await BuildArgs(OutputFormat.Hls, false);

        args.Should().NotContain("-fps_mode", "a constant-frame-rate source needs no reshaping");
    }

    [Fact]
    public async Task VfrNormalized_DashOutput_EmitsFpsModeCfr()
    {
        string[] args = await BuildArgs(OutputFormat.Dash, true);

        int idx = Array.IndexOf(args, "-fps_mode");
        idx.Should()
            .BeGreaterThan(-1, "a VFR source must be muxed at a constant frame rate for DASH");
        args[idx + 1].Should().Be("cfr");
    }

    [Fact]
    public void IsVariableFrameRate_TrueOnlyWhenRealAndAverageDiffer()
    {
        BuildVideoStream(30.0, 24.0).IsVariableFrameRate.Should().BeTrue();
        BuildVideoStream(24.0, 24.0).IsVariableFrameRate.Should().BeFalse();
    }

    private async Task<string[]> BuildArgs(OutputFormat format, bool normalizeCfr)
    {
        OutputPlan outputPlan = new(
            format,
            VideoOutputs: [BuildVideoOutput(1280, 720, "[v0]")],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null,
            NormalizeToConstantFrameRate: normalizeCfr
        );
        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(EncodingContext.Create().CorrelationId, BuildMediaInfo());

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        return ((StageSuccess<FfmpegCommand[]>)result).Value[0].Arguments;
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

    private static VideoStreamInfo BuildVideoStream(double real, double avg) =>
        new(
            0,
            "h264",
            1920,
            1080,
            real,
            8,
            "yuv420p",
            null,
            null,
            null,
            true,
            6000,
            avg,
            real
        );

    private static MediaInfo BuildMediaInfo() =>
        new(
            "/movies/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            8000,
            7_200_000_000,
            [BuildVideoStream(30.0, 24.0)],
            [],
            [],
            []
        );
}
