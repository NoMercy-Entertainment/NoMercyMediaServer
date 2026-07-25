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
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class BuildStageTwoPassTests
{
    private readonly BuildStage _stage;

    public BuildStageTwoPassTests()
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
    public async Task Pass1_EmitsSingleCommandWithPassFlag()
    {
        OutputPlan plan = PlanWith(BuildVideo(1920, 1080, "[v0]"));
        string statsPath = $"/tmp/stats-{Guid.NewGuid():N}";

        FfmpegCommand[] commands = await RunBuild(plan, EncodingPass.One, statsPath);

        Assert.Single(commands);
        string[] args = commands[0].Arguments;
        AssertContainsPair(args, "-pass", "1");
        // BuildStage appends variant index to the stats base path so each
        // variant writes its own stats file.
        AssertContainsPair(args, "-passlogfile", $"{statsPath}_v0");
        AssertContains(args, "-an");
        AssertContains(args, "-sn");
        AssertContainsPair(args, "-f", "null");
    }

    [Fact]
    public async Task Pass1_WithoutStatsFilePath_ReturnsFailure()
    {
        OutputPlan plan = PlanWith(BuildVideo(1920, 1080, "[v0]"));

        StageResult result = await ExecuteBuild(plan, EncodingPass.One, null);

        Assert.IsType<StageFailure>(result);
        Assert.Contains("StatsFilePath", ((StageFailure)result).Error.Message);
    }

    [Fact]
    public async Task Pass1_WithMultipleVideoOutputs_EncodesTargetVariantOnly()
    {
        // Multi-variant profiles are now supported — pass 1 picks the variant
        // by Pass1VariantIndex (default 0) and produces that variant's stats.
        OutputPlan plan = PlanWith([BuildVideo(1920, 1080, "[v0]"), BuildVideo(1280, 720, "[v1]")]);

        FfmpegCommand[] commands = await RunBuild(plan, EncodingPass.One, "/tmp/stats");

        Assert.Single(commands);
        string joined = string.Join(" ", commands[0].Arguments);
        Assert.Contains("-passlogfile /tmp/stats_v0", joined);
    }

    [Fact]
    public async Task Pass1_WithNoVideoOutputs_ReturnsFailure()
    {
        OutputPlan plan = PlanWith();

        StageResult result = await ExecuteBuild(plan, EncodingPass.One, "/tmp/stats");

        Assert.IsType<StageFailure>(result);
        Assert.Contains("at least one video output", ((StageFailure)result).Error.Message);
    }

    [Fact]
    public async Task Pass2_InjectsPass2FlagsIntoVideoOutput()
    {
        OutputPlan plan = PlanWith(BuildVideo(1920, 1080, "[v0]"));
        string statsPath = $"/tmp/stats-{Guid.NewGuid():N}";

        FfmpegCommand[] commands = await RunBuild(plan, EncodingPass.Two, statsPath);

        // The main command should have -pass 2 + -passlogfile somewhere.
        Assert.NotEmpty(commands);
        string joined = string.Join(" ", commands[0].Arguments);
        Assert.Contains("-pass 2", joined);
        Assert.Contains($"-passlogfile {statsPath}", joined);
        // Pass 2 keeps HLS output (unlike pass 1)
        Assert.Contains("-f hls", joined);
    }

    [Fact]
    public async Task Pass2_MixedCodecLadder_EachRungGetsOwnIndexedStatsAndCodec()
    {
        // A mixed-codec 2-pass ladder must give each rung its OWN indexed stats
        // file (-passlogfile _v0 / _v1) and its OWN -c:v — sharing one stats file
        // across different codecs corrupts the second rung's rate-control data.
        OutputPlan plan = PlanWith([BuildVideo(1920, 1080, "[v0]", "libx264"), BuildVideo(1280, 720, "[v1]", "libx265")]
        );
        string statsPath = $"/tmp/stats-{Guid.NewGuid():N}";

        FfmpegCommand[] commands = await RunBuild(plan, EncodingPass.Two, statsPath);

        string joined = string.Join(" ", commands[0].Arguments);
        joined.Should().Contain("-c:v libx264");
        joined.Should().Contain("-c:v libx265");
        joined.Should().Contain($"-passlogfile {statsPath}_v0");
        joined.Should().Contain($"-passlogfile {statsPath}_v1");
        // -pass 2 applied to both rungs (one -pass per video output).
        commands[0].Arguments.Count(a => a == "-pass").Should().Be(2);
    }

    [Fact]
    public async Task SinglePass_DoesNotEmitPassFlags()
    {
        OutputPlan plan = PlanWith(BuildVideo(1920, 1080, "[v0]"));

        FfmpegCommand[] commands = await RunBuild(plan, EncodingPass.Single, null);

        string joined = string.Join(" ", commands[0].Arguments);
        Assert.DoesNotContain("-pass 1", joined);
        Assert.DoesNotContain("-pass 2", joined);
        Assert.DoesNotContain("-passlogfile", joined);
    }

    private async Task<FfmpegCommand[]> RunBuild(
        OutputPlan plan,
        EncodingPass pass,
        string? statsFilePath
    )
    {
        StageResult result = await ExecuteBuild(plan, pass, statsFilePath);
        StageSuccess<FfmpegCommand[]> success = Assert.IsType<StageSuccess<FfmpegCommand[]>>(
            result
        );
        return success.Value;
    }

    private async Task<StageResult> ExecuteBuild(
        OutputPlan plan,
        EncodingPass pass,
        string? statsFilePath
    )
    {
        BuildInput input = new(
            BuildPlan(plan),
            "/media/test.mkv",
            Path.Combine(Path.GetTempPath(), $"bs-{Guid.NewGuid():N}"),
            "Test.NoMercy",
            null,
            pass,
            statsFilePath
        );
        Directory.CreateDirectory(input.OutputDirectory);

        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildMediaInfo(1920, 1080)
        );

        return await _stage.ExecuteAsync(input, context, default);
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

    private static OutputPlan PlanWith(params VideoOutputPlan[] video) =>
        new(
            OutputFormat.Hls,
            video,
            [],
            [],
            null
        );

    private static VideoOutputPlan BuildVideo(
        int width,
        int height,
        string mapLabel,
        string encoder = "libx264"
    ) =>
        new(
            width,
            height,
            encoder,
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

    private static MediaInfo BuildMediaInfo(int width, int height) =>
        new(
            "/media/test.mkv",
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
                    6000
                ),
            ],
            [],
            [],
            []
        );

    private static void AssertContainsPair(string[] args, string key, string value)
    {
        int idx = Array.IndexOf(args, key);
        Assert.True(idx >= 0, $"Expected '{key}' in args: {string.Join(' ', args)}");
        Assert.True(idx + 1 < args.Length, $"'{key}' is at end of args");
        Assert.Equal(value, args[idx + 1]);
    }

    private static void AssertContains(string[] args, string flag)
    {
        Assert.Contains(flag, args);
    }
}
