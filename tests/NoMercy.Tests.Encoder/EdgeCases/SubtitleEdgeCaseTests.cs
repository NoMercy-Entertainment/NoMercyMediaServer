// Copyright (c) 2024-present NoMercy Entertainment.
// SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary

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
using NoMercy.Encoder.Profiles;
using NoMercy.Tests.Encoder.Pipeline.Stages;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.EdgeCases;

public class SubtitleEdgeCaseTests
{
    private readonly BuildStage _stage;

    public SubtitleEdgeCaseTests()
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

    private static MediaInfo BuildMediaInfoWithSubtitles(params SubtitleStreamInfo[] subtitles) =>
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
                    1920,
                    1080,
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
            subtitles,
            []
        );

    [Fact]
    public async Task FilterGraphAssembler_GenericTextSubtitleBurnIn_GeneratesSubtitlesFilter()
    {
        SubtitleOutputPlan burnInSub = new(
            SubtitleCodecType.Srt,
            Action: StreamAction.Extract,
            Language: "en",
            SourceIndex: 0,
            MapLabel: null,
            Policy: SubtitlePolicy.BurnIn
        );
        VideoOutputPlan videoOutput = BuildVideoOutput(1920, 1080, "[v0]");
        OutputPlan outputPlan = new(
            OutputFormat.Hls,
            [videoOutput],
            [BuildAudioOutput()],
            [burnInSub],
            null
        );
        ExecutionPlan plan = BuildPlan(outputPlan);
        SubtitleStreamInfo srtStream = new(
            0,
            "srt",
            "en",
            true,
            false
        );
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildMediaInfoWithSubtitles(srtStream)
        );
        StageResult result = await _stage.ExecuteAsync(input, context, default);
        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        int filterComplexIdx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        filterComplexIdx
            .Should()
            .BeGreaterThan(-1, "filter_complex must be present for subtitle burn-in");
        string filterValue = commands[0].Arguments[filterComplexIdx + 1];
        filterValue
            .Should()
            .Contain("subtitles=", "SRT subtitle burn-in must use subtitles= filter");
        filterValue
            .Should()
            .Contain(":si=0", "subtitles filter must include stream index parameter");
    }

    [Fact]
    public async Task FilterGraphAssembler_BitmapSubtitleBurnIn_DoesNotUseBurnInFilter()
    {
        SubtitleOutputPlan burnInSub = new(
            SubtitleCodecType.Pgs,
            Action: StreamAction.Extract,
            Language: "en",
            SourceIndex: 0,
            MapLabel: null,
            Policy: SubtitlePolicy.BurnIn
        );
        VideoOutputPlan videoOutput = BuildVideoOutput(1920, 1080, "[v0]");
        OutputPlan outputPlan = new(
            OutputFormat.Hls,
            [videoOutput],
            [BuildAudioOutput()],
            [burnInSub],
            null
        );
        ExecutionPlan plan = BuildPlan(outputPlan);
        SubtitleStreamInfo pgsStream = new(
            0,
            "hdmv_pgs_subtitle",
            "en",
            true,
            false
        );
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildMediaInfoWithSubtitles(pgsStream)
        );
        StageResult result = await _stage.ExecuteAsync(input, context, default);
        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        int filterComplexIdx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        if (filterComplexIdx > -1)
        {
            string filterValue = commands[0].Arguments[filterComplexIdx + 1];
            filterValue.Should().NotContain("ass=", "PGS bitmap subtitle must NOT use ass= filter");
            filterValue
                .Should()
                .NotContain("subtitles=", "PGS bitmap subtitle must NOT use subtitles= filter");
        }
    }

    [Fact]
    public async Task FilterGraphAssembler_SubtitleBurnIn_ForcesVideoReencode()
    {
        SubtitleOutputPlan burnInSub = new(
            SubtitleCodecType.Ass,
            Action: StreamAction.Extract,
            Language: "en",
            SourceIndex: 0,
            MapLabel: null,
            Policy: SubtitlePolicy.BurnIn
        );
        VideoOutputPlan videoOutput = BuildVideoOutput(1920, 1080, "[v0]") with
        {
            EncoderName = "libx264",
        };
        OutputPlan outputPlan = new(
            OutputFormat.Hls,
            [videoOutput],
            [BuildAudioOutput()],
            [burnInSub],
            null
        );
        ExecutionPlan plan = BuildPlan(outputPlan);
        SubtitleStreamInfo assStream = new(
            0,
            "ass",
            "en",
            true,
            false
        );
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildMediaInfoWithSubtitles(assStream)
        );
        StageResult result = await _stage.ExecuteAsync(input, context, default);
        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;
        int codecIdx = Array.IndexOf(args, "-c:v");
        codecIdx.Should().BeGreaterThan(-1, "video codec specifier must be present");
        string videoCodec = args[codecIdx + 1];
        videoCodec.Should().Be("libx264", "burn-in subtitle must NOT be copy; encoder must be set");
    }

    [Fact]
    public async Task FilterGraphAssembler_NoBurnInSubtitle_NoSubtitleFilterAppears()
    {
        VideoOutputPlan videoOutput = BuildVideoOutput(1920, 1080, "[v0]");
        OutputPlan outputPlan = new(
            OutputFormat.Hls,
            [videoOutput],
            [BuildAudioOutput()],
            [],
            null
        );
        ExecutionPlan plan = BuildPlan(outputPlan);
        SubtitleStreamInfo srtStream = new(
            0,
            "srt",
            "en",
            true,
            false
        );
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildMediaInfoWithSubtitles(srtStream)
        );
        StageResult result = await _stage.ExecuteAsync(input, context, default);
        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        int filterComplexIdx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        if (filterComplexIdx > -1)
        {
            string filterValue = commands[0].Arguments[filterComplexIdx + 1];
            filterValue
                .Should()
                .NotContain("subtitles=", "no subtitle filter without burn-in policy");
            filterValue.Should().NotContain("ass=", "no ASS filter without burn-in policy");
        }
    }
}
