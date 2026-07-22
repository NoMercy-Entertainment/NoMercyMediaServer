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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class BuildStageTests
{
    private readonly BuildStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public BuildStageTests()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = "ffmpeg",
            FfprobePathOverride = "ffprobe",
        };
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

    private static ExecutionPlan BuildHlsPlan() =>
        new(
            Groups:
            [
                new(
                    GroupId: "group_0",
                    Nodes:
                    [
                        new(Id: "decode_0", Operation: OperationType.Decode, DependsOn: [], Parameters: new()),
                        new(Id: "encode_0", Operation: OperationType.Encode, DependsOn: ["decode_0"], Parameters: new()),
                    ],
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 4,
                    RequiresGpu: false,
                    Priority: 1
                ),
            ],
            EstimatedTotalDuration: TimeSpan.FromMinutes(minutes: 90),
            OutputPlan: new(
                Format: OutputFormat.Hls,
                VideoOutputs:
                [
                    new(
                        Width: 1920,
                        Height: 1080,
                        EncoderName: "libx264",
                        Crf: 23,
                        BitrateKbps: 4000,
                        Preset: "medium",
                        Profile: "high",
                        Level: "4.1",
                        TenBit: false,
                        PixelFormat: "yuv420p",
                        MapLabel: "[v0]",
                        ExtraFlags: new()
                    ),
                ],
                AudioOutputs:
                [
                    new(
                        EncoderName: "aac",
                        BitrateKbps: 192,
                        Channels: 2,
                        SampleRate: 48000,
                        Action: StreamAction.Transcode,
                        Language: "en",
                        MapLabel: "0:a:0"
                    ),
                ],
                SubtitleOutputs: [],
                Thumbnails: null
            )
        );

    // ------------------------------------------------------------------
    // HLS plan → builds at least one FFmpeg command
    // ------------------------------------------------------------------

    [Fact]
    public async Task HlsPlan_BuildsAtLeastOneCommand()
    {
        ExecutionPlan plan = BuildHlsPlan();
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");

        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        commands.Should().NotBeEmpty();
    }

    // ------------------------------------------------------------------
    // Built command uses the correct ffmpeg executable
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuiltCommand_UsesConfiguredFfmpegPath()
    {
        ExecutionPlan plan = BuildHlsPlan();
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");

        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        commands[0].Executable.Should().Be(expected: "ffmpeg");
    }

    // ------------------------------------------------------------------
    // Built command references the input file
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuiltCommand_ContainsInputPath()
    {
        ExecutionPlan plan = BuildHlsPlan();
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");

        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        commands[0].Arguments.Should().Contain(expected: "/movies/test.mkv");
    }

    // ------------------------------------------------------------------
    // Built command references the encoder
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuiltCommand_ContainsVideoEncoder()
    {
        ExecutionPlan plan = BuildHlsPlan();
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");

        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        commands[0].Arguments.Should().Contain(expected: "libx264");
    }

    // ------------------------------------------------------------------
    // MKV plan → builds a command
    // ------------------------------------------------------------------

    [Fact]
    public async Task MkvPlan_BuildsCommand()
    {
        ExecutionPlan plan = BuildHlsPlan() with
        {
            OutputPlan = BuildHlsPlan().OutputPlan with { Format = OutputFormat.Mkv },
        };
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");

        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
    }
}
