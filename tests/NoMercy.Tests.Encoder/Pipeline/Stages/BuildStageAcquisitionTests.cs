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
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Encoder.Subtitles;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class BuildStageAcquisitionTests
{
    private readonly BuildStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public BuildStageAcquisitionTests()
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

    // Test 1: No acquired subtitles → main command contains only the source input
    [Fact]
    public async Task NoAcquiredSubtitles_CommandContainsOnlySourceInput()
    {
        ExecutionPlan plan = BuildPlan(acquiredSubtitles: []);
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test");

        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: CancellationToken.None);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        // Count -i occurrences — should be exactly 1 (source only)
        int inputCount = CountArg(args: args, arg: "-i");
        inputCount.Should().Be(expected: 1);
        args.Should().Contain(expected: "/movies/test.mkv");
    }

    // Test 2: One ExactMatch subtitle → command adds that file as a second -i input
    [Fact]
    public async Task OneExactMatchSub_AddsInputToCommand()
    {
        AcquiredSubtitle sub = new(
            Language: "en",
            LocalPath: "/tmp/subs/en.srt",
            Provider: "OpenSubtitles",
            IsExactMatch: true,
            Rating: 8.0,
            Downloads: 1000,
            Format: "srt"
        );

        ExecutionPlan plan = BuildPlan(acquiredSubtitles: [sub]);
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test");

        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: CancellationToken.None);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        args.Should().Contain(expected: "/tmp/subs/en.srt");
        int inputCount = CountArg(args: args, arg: "-i");
        inputCount.Should().Be(expected: 2);
    }

    // Test 3: Non-exact-match subtitle → NOT added as input
    [Fact]
    public async Task NonExactMatchSub_NotAddedAsInput()
    {
        AcquiredSubtitle sub = new(
            Language: "en",
            LocalPath: "/tmp/subs/en.srt",
            Provider: "OpenSubtitles",
            IsExactMatch: false,
            Rating: 6.0,
            Downloads: 100,
            Format: "srt"
        );

        ExecutionPlan plan = BuildPlan(acquiredSubtitles: [sub]);
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test");

        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: CancellationToken.None);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        args.Should().NotContain(unexpected: "/tmp/subs/en.srt");
        int inputCount = CountArg(args: args, arg: "-i");
        inputCount.Should().Be(expected: 1);
    }

    // Test 4: Two exact-match subs → both added as inputs
    [Fact]
    public async Task TwoExactMatchSubs_BothAddedAsInputs()
    {
        AcquiredSubtitle enSub = new(
            Language: "en",
            LocalPath: "/tmp/subs/en.srt",
            Provider: "OpenSubtitles",
            IsExactMatch: true,
            Rating: 8.0,
            Downloads: 1000,
            Format: "srt"
        );
        AcquiredSubtitle nlSub = new(
            Language: "nl",
            LocalPath: "/tmp/subs/nl.srt",
            Provider: "OpenSubtitles",
            IsExactMatch: true,
            Rating: 7.0,
            Downloads: 500,
            Format: "srt"
        );

        ExecutionPlan plan = BuildPlan(acquiredSubtitles: [enSub, nlSub]);
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test");

        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: CancellationToken.None);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        args.Should().Contain(expected: "/tmp/subs/en.srt");
        args.Should().Contain(expected: "/tmp/subs/nl.srt");
        int inputCount = CountArg(args: args, arg: "-i");
        inputCount.Should().Be(expected: 3);
    }

    // Test 5: Mixed exact/non-exact → only exact one added
    [Fact]
    public async Task MixedExactAndNonExact_OnlyExactAdded()
    {
        AcquiredSubtitle exact = new(
            Language: "en",
            LocalPath: "/tmp/subs/en.srt",
            Provider: "OpenSubtitles",
            IsExactMatch: true,
            Rating: 8.0,
            Downloads: 1000,
            Format: "srt"
        );
        AcquiredSubtitle notExact = new(
            Language: "nl",
            LocalPath: "/tmp/subs/nl.srt",
            Provider: "OpenSubtitles",
            IsExactMatch: false,
            Rating: 6.0,
            Downloads: 100,
            Format: "srt"
        );

        ExecutionPlan plan = BuildPlan(acquiredSubtitles: [exact, notExact]);
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test");

        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: CancellationToken.None);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        args.Should().Contain(expected: "/tmp/subs/en.srt");
        args.Should().NotContain(unexpected: "/tmp/subs/nl.srt");
        int inputCount = CountArg(args: args, arg: "-i");
        inputCount.Should().Be(expected: 2);
    }

    private static int CountArg(string[] args, string arg) => args.Count(predicate: a => a == arg);

    private static ExecutionPlan BuildPlan(IReadOnlyList<AcquiredSubtitle>? acquiredSubtitles)
    {
        OutputPlan outputPlan = new(
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
            Thumbnails: null,
            AcquiredSubtitles: acquiredSubtitles
        );

        return new(
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
            OutputPlan: outputPlan
        );
    }
}
