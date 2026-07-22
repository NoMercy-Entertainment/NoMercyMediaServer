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
            options: options,
            fontExtractor: new FontExtractor(storage: TestStorageFactory.CreateLocal()),
            subtitleExtractor: new SubtitleExtractor(),
            outputStrategyFactory: OutputStrategyFactoryTestHelper.Create(),
            drmProcessors: [],
            logger: NullLogger<BuildStage>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
    }

    [Fact]
    public async Task Pass1_EmitsSingleCommandWithPassFlag()
    {
        OutputPlan plan = PlanWith(video: BuildVideo(width: 1920, height: 1080, mapLabel: "[v0]"));
        string statsPath = $"/tmp/stats-{Guid.NewGuid():N}";

        FfmpegCommand[] commands = await RunBuild(plan: plan, pass: EncodingPass.One, statsFilePath: statsPath);

        Assert.Single(collection: commands);
        string[] args = commands[0].Arguments;
        AssertContainsPair(args: args, key: "-pass", value: "1");
        // BuildStage appends variant index to the stats base path so each
        // variant writes its own stats file.
        AssertContainsPair(args: args, key: "-passlogfile", value: $"{statsPath}_v0");
        AssertContains(args: args, flag: "-an");
        AssertContains(args: args, flag: "-sn");
        AssertContainsPair(args: args, key: "-f", value: "null");
    }

    [Fact]
    public async Task Pass1_WithoutStatsFilePath_ReturnsFailure()
    {
        OutputPlan plan = PlanWith(video: BuildVideo(width: 1920, height: 1080, mapLabel: "[v0]"));

        StageResult result = await ExecuteBuild(plan: plan, pass: EncodingPass.One, statsFilePath: null);

        Assert.IsType<StageFailure>(@object: result);
        Assert.Contains(expectedSubstring: "StatsFilePath", actualString: ((StageFailure)result).Error.Message);
    }

    [Fact]
    public async Task Pass1_WithMultipleVideoOutputs_EncodesTargetVariantOnly()
    {
        // Multi-variant profiles are now supported — pass 1 picks the variant
        // by Pass1VariantIndex (default 0) and produces that variant's stats.
        OutputPlan plan = PlanWith(video: [BuildVideo(width: 1920, height: 1080, mapLabel: "[v0]"), BuildVideo(width: 1280, height: 720, mapLabel: "[v1]")]);

        FfmpegCommand[] commands = await RunBuild(plan: plan, pass: EncodingPass.One, statsFilePath: "/tmp/stats");

        Assert.Single(collection: commands);
        string joined = string.Join(separator: " ", value: commands[0].Arguments);
        Assert.Contains(expectedSubstring: "-passlogfile /tmp/stats_v0", actualString: joined);
    }

    [Fact]
    public async Task Pass1_WithNoVideoOutputs_ReturnsFailure()
    {
        OutputPlan plan = PlanWith();

        StageResult result = await ExecuteBuild(plan: plan, pass: EncodingPass.One, statsFilePath: "/tmp/stats");

        Assert.IsType<StageFailure>(@object: result);
        Assert.Contains(expectedSubstring: "at least one video output", actualString: ((StageFailure)result).Error.Message);
    }

    [Fact]
    public async Task Pass2_InjectsPass2FlagsIntoVideoOutput()
    {
        OutputPlan plan = PlanWith(video: BuildVideo(width: 1920, height: 1080, mapLabel: "[v0]"));
        string statsPath = $"/tmp/stats-{Guid.NewGuid():N}";

        FfmpegCommand[] commands = await RunBuild(plan: plan, pass: EncodingPass.Two, statsFilePath: statsPath);

        // The main command should have -pass 2 + -passlogfile somewhere.
        Assert.NotEmpty(collection: commands);
        string joined = string.Join(separator: " ", value: commands[0].Arguments);
        Assert.Contains(expectedSubstring: "-pass 2", actualString: joined);
        Assert.Contains(expectedSubstring: $"-passlogfile {statsPath}", actualString: joined);
        // Pass 2 keeps HLS output (unlike pass 1)
        Assert.Contains(expectedSubstring: "-f hls", actualString: joined);
    }

    [Fact]
    public async Task Pass2_MixedCodecLadder_EachRungGetsOwnIndexedStatsAndCodec()
    {
        // A mixed-codec 2-pass ladder must give each rung its OWN indexed stats
        // file (-passlogfile _v0 / _v1) and its OWN -c:v — sharing one stats file
        // across different codecs corrupts the second rung's rate-control data.
        OutputPlan plan = PlanWith(video: [BuildVideo(width: 1920, height: 1080, mapLabel: "[v0]", encoder: "libx264"), BuildVideo(width: 1280, height: 720, mapLabel: "[v1]", encoder: "libx265")]
        );
        string statsPath = $"/tmp/stats-{Guid.NewGuid():N}";

        FfmpegCommand[] commands = await RunBuild(plan: plan, pass: EncodingPass.Two, statsFilePath: statsPath);

        string joined = string.Join(separator: " ", value: commands[0].Arguments);
        joined.Should().Contain(expected: "-c:v libx264");
        joined.Should().Contain(expected: "-c:v libx265");
        joined.Should().Contain(expected: $"-passlogfile {statsPath}_v0");
        joined.Should().Contain(expected: $"-passlogfile {statsPath}_v1");
        // -pass 2 applied to both rungs (one -pass per video output).
        commands[0].Arguments.Count(predicate: a => a == "-pass").Should().Be(expected: 2);
    }

    [Fact]
    public async Task SinglePass_DoesNotEmitPassFlags()
    {
        OutputPlan plan = PlanWith(video: BuildVideo(width: 1920, height: 1080, mapLabel: "[v0]"));

        FfmpegCommand[] commands = await RunBuild(plan: plan, pass: EncodingPass.Single, statsFilePath: null);

        string joined = string.Join(separator: " ", value: commands[0].Arguments);
        Assert.DoesNotContain(expectedSubstring: "-pass 1", actualString: joined);
        Assert.DoesNotContain(expectedSubstring: "-pass 2", actualString: joined);
        Assert.DoesNotContain(expectedSubstring: "-passlogfile", actualString: joined);
    }

    private async Task<FfmpegCommand[]> RunBuild(
        OutputPlan plan,
        EncodingPass pass,
        string? statsFilePath
    )
    {
        StageResult result = await ExecuteBuild(plan: plan, pass: pass, statsFilePath: statsFilePath);
        StageSuccess<FfmpegCommand[]> success = Assert.IsType<StageSuccess<FfmpegCommand[]>>(
            @object: result
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
            Plan: BuildPlan(outputPlan: plan),
            InputPath: "/media/test.mkv",
            OutputDirectory: Path.Combine(path1: Path.GetTempPath(), path2: $"bs-{Guid.NewGuid():N}"),
            MediaTitle: "Test.NoMercy",
            DurationLimit: null,
            Pass: pass,
            StatsFilePath: statsFilePath
        );
        Directory.CreateDirectory(path: input.OutputDirectory);

        EncodingContext context = new(
            CorrelationId: EncodingContext.Create().CorrelationId,
            MediaInfo: BuildMediaInfo(width: 1920, height: 1080)
        );

        return await _stage.ExecuteAsync(input: input, context: context, ct: default);
    }

    private static ExecutionPlan BuildPlan(OutputPlan outputPlan) =>
        new(
            Groups:
            [
                new(
                    GroupId: "group_0",
                    Nodes: [new(Id: "decode_0", Operation: OperationType.Decode, DependsOn: [], Parameters: new())],
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 4,
                    RequiresGpu: false,
                    Priority: 1
                ),
            ],
            EstimatedTotalDuration: TimeSpan.FromMinutes(minutes: 90),
            OutputPlan: outputPlan
        );

    private static OutputPlan PlanWith(params VideoOutputPlan[] video) =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs: video,
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

    private static VideoOutputPlan BuildVideo(
        int width,
        int height,
        string mapLabel,
        string encoder = "libx264"
    ) =>
        new(
            Width: width,
            Height: height,
            EncoderName: encoder,
            Crf: 23,
            BitrateKbps: 4000,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: mapLabel,
            ExtraFlags: new()
        );

    private static MediaInfo BuildMediaInfo(int width, int height) =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(hours: 2),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 7_200_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: width,
                    Height: height,
                    FrameRate: 24.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: 6000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static void AssertContainsPair(string[] args, string key, string value)
    {
        int idx = Array.IndexOf(array: args, value: key);
        Assert.True(condition: idx >= 0, userMessage: $"Expected '{key}' in args: {string.Join(separator: ' ', value: args)}");
        Assert.True(condition: idx + 1 < args.Length, userMessage: $"'{key}' is at end of args");
        Assert.Equal(expected: value, actual: args[idx + 1]);
    }

    private static void AssertContains(string[] args, string flag)
    {
        Assert.Contains(expected: flag, collection: args);
    }
}
