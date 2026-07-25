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
            options,
            new FontExtractor(TestStorageFactory.CreateLocal()),
            new SubtitleExtractor(),
            OutputStrategyFactoryTestHelper.Create(),
            [],
            NullLogger<BuildStage>.Instance,
            TestStorageFactory.CreateLocal()
        );
    }

    // Test 1: No acquired subtitles → main command contains only the source input
    [Fact]
    public async Task NoAcquiredSubtitles_CommandContainsOnlySourceInput()
    {
        ExecutionPlan plan = BuildPlan([]);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test");

        StageResult result = await _stage.ExecuteAsync(input, _context, CancellationToken.None);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        // Count -i occurrences — should be exactly 1 (source only)
        int inputCount = CountArg(args, "-i");
        inputCount.Should().Be(1);
        args.Should().Contain("/movies/test.mkv");
    }

    // Test 2: One ExactMatch subtitle → command adds that file as a second -i input
    [Fact]
    public async Task OneExactMatchSub_AddsInputToCommand()
    {
        AcquiredSubtitle sub = new(
            "en",
            "/tmp/subs/en.srt",
            "OpenSubtitles",
            true,
            8.0,
            1000,
            "srt"
        );

        ExecutionPlan plan = BuildPlan([sub]);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test");

        StageResult result = await _stage.ExecuteAsync(input, _context, CancellationToken.None);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        args.Should().Contain("/tmp/subs/en.srt");
        int inputCount = CountArg(args, "-i");
        inputCount.Should().Be(2);
    }

    // Test 3: Non-exact-match subtitle → NOT added as input
    [Fact]
    public async Task NonExactMatchSub_NotAddedAsInput()
    {
        AcquiredSubtitle sub = new(
            "en",
            "/tmp/subs/en.srt",
            "OpenSubtitles",
            false,
            6.0,
            100,
            "srt"
        );

        ExecutionPlan plan = BuildPlan([sub]);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test");

        StageResult result = await _stage.ExecuteAsync(input, _context, CancellationToken.None);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        args.Should().NotContain("/tmp/subs/en.srt");
        int inputCount = CountArg(args, "-i");
        inputCount.Should().Be(1);
    }

    // Test 4: Two exact-match subs → both added as inputs
    [Fact]
    public async Task TwoExactMatchSubs_BothAddedAsInputs()
    {
        AcquiredSubtitle enSub = new(
            "en",
            "/tmp/subs/en.srt",
            "OpenSubtitles",
            true,
            8.0,
            1000,
            "srt"
        );
        AcquiredSubtitle nlSub = new(
            "nl",
            "/tmp/subs/nl.srt",
            "OpenSubtitles",
            true,
            7.0,
            500,
            "srt"
        );

        ExecutionPlan plan = BuildPlan([enSub, nlSub]);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test");

        StageResult result = await _stage.ExecuteAsync(input, _context, CancellationToken.None);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        args.Should().Contain("/tmp/subs/en.srt");
        args.Should().Contain("/tmp/subs/nl.srt");
        int inputCount = CountArg(args, "-i");
        inputCount.Should().Be(3);
    }

    // Test 5: Mixed exact/non-exact → only exact one added
    [Fact]
    public async Task MixedExactAndNonExact_OnlyExactAdded()
    {
        AcquiredSubtitle exact = new(
            "en",
            "/tmp/subs/en.srt",
            "OpenSubtitles",
            true,
            8.0,
            1000,
            "srt"
        );
        AcquiredSubtitle notExact = new(
            "nl",
            "/tmp/subs/nl.srt",
            "OpenSubtitles",
            false,
            6.0,
            100,
            "srt"
        );

        ExecutionPlan plan = BuildPlan([exact, notExact]);
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test");

        StageResult result = await _stage.ExecuteAsync(input, _context, CancellationToken.None);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        args.Should().Contain("/tmp/subs/en.srt");
        args.Should().NotContain("/tmp/subs/nl.srt");
        int inputCount = CountArg(args, "-i");
        inputCount.Should().Be(2);
    }

    private static int CountArg(string[] args, string arg) => args.Count(a => a == arg);

    private static ExecutionPlan BuildPlan(IReadOnlyList<AcquiredSubtitle>? acquiredSubtitles)
    {
        OutputPlan outputPlan = new(
            OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    1920,
                    1080,
                    "libx264",
                    23,
                    4000,
                    "medium",
                    "high",
                    "4.1",
                    false,
                    "yuv420p",
                    "[v0]",
                    new()
                ),
            ],
            AudioOutputs:
            [
                new(
                    "aac",
                    192,
                    2,
                    48000,
                    StreamAction.Transcode,
                    "en",
                    "0:a:0"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null,
            AcquiredSubtitles: acquiredSubtitles
        );

        return new(
            [
                new(
                    "group_0",
                    [
                        new("decode_0", OperationType.Decode, [], new()),
                        new("encode_0", OperationType.Encode, ["decode_0"], new()),
                    ],
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
    }
}
