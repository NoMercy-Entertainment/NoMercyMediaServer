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
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Encoder.Subtitles;
using NoMercy.Tests.Encoder.Storage;
using SubtitlePolicy = NoMercy.Encoder.Profiles.SubtitlePolicy;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class BuildStageBurnInTests
{
    private readonly BuildStage _stage;

    public BuildStageBurnInTests()
    {
        EncoderOptions options = new() { FfmpegPathOverride = "ffmpeg" };
        _stage = new(
            options: options,
            fontExtractor: new FontExtractor(storage: TestStorageFactory.CreateLocal()),
            subtitleExtractor: new SubtitleExtractor(),
            outputStrategyFactory: OutputStrategyFactoryTestHelper.Create(),
            drmProcessors: [],
            logger: NullLogger<BuildStage>.Instance,
            storage: TestStorageFactory.CreateLocal(),
            assBurnInFilterBuilder: new(),
            pgsBurnInFilterBuilder: new()
        );
    }

    [Fact]
    public async Task BurnIn_AddsSubtitlesFilterToVideoChain()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(width: 1920, height: 1080, mapLabel: "[v0]")],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.Ass,
                    Action: StreamAction.Transcode,
                    Language: "en",
                    SourceIndex: 0,
                    MapLabel: "0:s:0",
                    PlaylistNameTemplate: "subtitles/burn",
                    Policy: SubtitlePolicy.BurnIn
                ),
            ],
            Thumbnails: null
        );

        string filterValue = await GetFilterComplex(outputPlan: outputPlan, inputPath: "/movies/test.mkv", srcWidth: 1920, srcHeight: 1080);

        // Phase 4.6: ASS source uses the dedicated `ass=` filter (was `subtitles=`).
        Assert.Contains(expectedSubstring: "ass=", actualString: filterValue);
    }

    [Fact]
    public async Task BurnIn_EscapesColonsInInputPath()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(width: 1920, height: 1080, mapLabel: "[v0]")],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.Ass,
                    Action: StreamAction.Transcode,
                    Language: "en",
                    SourceIndex: 0,
                    MapLabel: "0:s:0",
                    Policy: SubtitlePolicy.BurnIn
                ),
            ],
            Thumbnails: null
        );

        string filterValue = await GetFilterComplex(outputPlan: outputPlan, inputPath: "C:/movies/test.mkv", srcWidth: 1920, srcHeight: 1080);

        Assert.Contains(expectedSubstring: "C\\:/movies/test.mkv", actualString: filterValue);
    }

    [Fact]
    public async Task BurnIn_DoesNotEmitSeparateSubtitleOutput()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(width: 1920, height: 1080, mapLabel: "[v0]")],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.Ass,
                    Action: StreamAction.Transcode,
                    Language: "en",
                    SourceIndex: 0,
                    MapLabel: "0:s:0",
                    Policy: SubtitlePolicy.BurnIn
                ),
            ],
            Thumbnails: null
        );

        ExecutionPlan plan = BuildPlan(outputPlan: outputPlan);
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");
        EncodingContext context = new(
            CorrelationId: EncodingContext.Create().CorrelationId,
            MediaInfo: BuildMediaInfoWithSubtitle(width: 1920, height: 1080, textBased: true)
        );

        StageResult result = await _stage.ExecuteAsync(input: input, context: context, ct: default);
        Assert.IsType<StageSuccess<FfmpegCommand[]>>(@object: result);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        // Count outputs in the main command — should be video only (no subtitle output).
        // -map 0:s:X flag would indicate a subtitle stream output.
        string args = string.Join(separator: " ", value: commands[0].Arguments);
        Assert.DoesNotContain(expectedSubstring: "-map 0:s:", actualString: args);
    }

    [Fact]
    public async Task BurnIn_AppliedAfterScale()
    {
        // When scaling + burn-in: filter chain should be scale → subtitles, not subtitles → scale.
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(width: 1280, height: 720, mapLabel: "[v0]")],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.Ass,
                    Action: StreamAction.Transcode,
                    Language: "en",
                    SourceIndex: 0,
                    MapLabel: "0:s:0",
                    Policy: SubtitlePolicy.BurnIn
                ),
            ],
            Thumbnails: null
        );

        string filterValue = await GetFilterComplex(outputPlan: outputPlan, inputPath: "/movies/test.mkv", srcWidth: 1920, srcHeight: 1080);

        int scaleIdx = filterValue.IndexOf(value: "scale=1280", comparisonType: StringComparison.Ordinal);
        int burnIdx = filterValue.IndexOf(value: "ass=", comparisonType: StringComparison.Ordinal);

        Assert.True(condition: scaleIdx >= 0, userMessage: "scale filter must be present");
        Assert.True(condition: burnIdx >= 0, userMessage: "ass filter must be present");
        Assert.True(condition: scaleIdx < burnIdx, userMessage: "burn-in must come after scale in the filter chain");
    }

    [Fact]
    public async Task ExtractMode_DoesNotAddSubtitlesFilter()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(width: 1920, height: 1080, mapLabel: "[v0]")],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.WebVtt,
                    Action: StreamAction.Extract,
                    Language: "en",
                    SourceIndex: 0,
                    MapLabel: "0:s:0",
                    Policy: SubtitlePolicy.Extract
                ),
            ],
            Thumbnails: null
        );

        string filterValue = await GetFilterComplex(outputPlan: outputPlan, inputPath: "/movies/test.mkv", srcWidth: 1920, srcHeight: 1080);

        Assert.DoesNotContain(expectedSubstring: "subtitles=", actualString: filterValue);
    }

    private async Task<string> GetFilterComplex(
        OutputPlan outputPlan,
        string inputPath,
        int srcWidth,
        int srcHeight
    )
    {
        ExecutionPlan plan = BuildPlan(outputPlan: outputPlan);
        BuildInput input = new(Plan: plan, InputPath: inputPath, OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");
        EncodingContext context = new(
            CorrelationId: EncodingContext.Create().CorrelationId,
            MediaInfo: BuildMediaInfoWithSubtitle(width: srcWidth, height: srcHeight, textBased: true)
        );

        StageResult result = await _stage.ExecuteAsync(input: input, context: context, ct: default);
        Assert.IsType<StageSuccess<FfmpegCommand[]>>(@object: result);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        int idx = Array.IndexOf(array: commands[0].Arguments, value: "-filter_complex");
        Assert.True(condition: idx >= 0, userMessage: "filter_complex flag must be present");
        return commands[0].Arguments[idx + 1];
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

    private static MediaInfo BuildMediaInfoWithSubtitle(int width, int height, bool textBased) =>
        new(
            FilePath: "/movies/test.mkv",
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
            SubtitleStreams:
            [
                new(
                    Index: 0,
                    Codec: textBased ? "ass" : "hdmv_pgs_subtitle",
                    Language: "en",
                    IsDefault: true,
                    IsForced: false,
                    Title: null
                ),
            ],
            Chapters: []
        );

    [Fact]
    public async Task AssBurnIn_UsesAssFilterNotSubtitlesFilter()
    {
        // When the source codec is ASS the builder must emit `ass=` not
        // the generic `subtitles=` so libass handles the rendering path.
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(width: 1920, height: 1080, mapLabel: "[v0]")],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.Ass,
                    Action: StreamAction.Transcode,
                    Language: "en",
                    SourceIndex: 0,
                    MapLabel: "0:s:0",
                    Policy: SubtitlePolicy.BurnIn
                ),
            ],
            Thumbnails: null
        );

        string filterValue = await GetFilterComplexWithCodec(
            outputPlan: outputPlan,
            inputPath: "/movies/test.mkv",
            srcWidth: 1920,
            srcHeight: 1080,
            subtitleCodec: "ass"
        );

        Assert.Contains(expectedSubstring: "ass=", actualString: filterValue);
    }

    [Fact]
    public async Task PgsBurnIn_UsesOverlayFilterComplex()
    {
        // PGS burn-in must produce `overlay=format=auto` in -filter_complex,
        // not the text-subtitle `subtitles=` filter.
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(width: 1920, height: 1080, mapLabel: "[v0]")],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.Ass,
                    Action: StreamAction.Transcode,
                    Language: "en",
                    SourceIndex: 0,
                    MapLabel: "0:s:0",
                    Policy: SubtitlePolicy.BurnIn
                ),
            ],
            Thumbnails: null
        );

        ExecutionPlan plan = BuildPlan(outputPlan: outputPlan);
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");
        EncodingContext context = new(
            CorrelationId: EncodingContext.Create().CorrelationId,
            MediaInfo: BuildMediaInfoWithSubtitle(width: 1920, height: 1080, textBased: false)
        );

        StageResult result = await _stage.ExecuteAsync(input: input, context: context, ct: default);
        Assert.IsType<StageSuccess<FfmpegCommand[]>>(@object: result);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string args = string.Join(separator: " ", value: commands[0].Arguments);

        Assert.Contains(expectedSubstring: "overlay=format=auto", actualString: args);
    }

    [Fact]
    public async Task BurnIn_EmitsBurnInPermanentDecisionLog()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(width: 1920, height: 1080, mapLabel: "[v0]")],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.Ass,
                    Action: StreamAction.Transcode,
                    Language: "en",
                    SourceIndex: 0,
                    MapLabel: "0:s:0",
                    Policy: SubtitlePolicy.BurnIn
                ),
            ],
            Thumbnails: null
        );

        ExecutionPlan plan = BuildPlan(outputPlan: outputPlan);
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");
        ScopedDecisionLog decisions = new();
        EncodingContext context = new(
            CorrelationId: EncodingContext.Create().CorrelationId,
            MediaInfo: BuildMediaInfoWithSubtitle(width: 1920, height: 1080, textBased: true),
            Decisions: decisions
        );

        await _stage.ExecuteAsync(input: input, context: context, ct: default);

        IReadOnlyList<DecisionLog> snapshot = decisions.Snapshot();
        Assert.Contains(collection: snapshot, filter: d => d.Key == EncoderRuleId.SubtitlesBurnInPermanent);
    }

    [Fact]
    public async Task ExtractMode_DoesNotEmitBurnInDecisionLog()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(width: 1920, height: 1080, mapLabel: "[v0]")],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.WebVtt,
                    Action: StreamAction.Extract,
                    Language: "en",
                    SourceIndex: 0,
                    MapLabel: "0:s:0",
                    Policy: SubtitlePolicy.Extract
                ),
            ],
            Thumbnails: null
        );

        ExecutionPlan plan = BuildPlan(outputPlan: outputPlan);
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");
        ScopedDecisionLog decisions = new();
        EncodingContext context = new(
            CorrelationId: EncodingContext.Create().CorrelationId,
            MediaInfo: BuildMediaInfoWithSubtitle(width: 1920, height: 1080, textBased: true),
            Decisions: decisions
        );

        await _stage.ExecuteAsync(input: input, context: context, ct: default);

        IReadOnlyList<DecisionLog> snapshot = decisions.Snapshot();
        Assert.DoesNotContain(collection: snapshot, filter: d => d.Key == EncoderRuleId.SubtitlesBurnInPermanent);
    }

    /// <summary>
    /// Variant of <see cref="GetFilterComplex"/> that lets the caller
    /// specify which subtitle codec the source stream carries, so tests
    /// can distinguish ASS filter dispatch from generic subtitles= dispatch.
    /// </summary>
    private async Task<string> GetFilterComplexWithCodec(
        OutputPlan outputPlan,
        string inputPath,
        int srcWidth,
        int srcHeight,
        string subtitleCodec
    )
    {
        ExecutionPlan plan = BuildPlan(outputPlan: outputPlan);
        BuildInput input = new(Plan: plan, InputPath: inputPath, OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");
        EncodingContext context = new(
            CorrelationId: EncodingContext.Create().CorrelationId,
            MediaInfo: BuildMediaInfoWithCodec(width: srcWidth, height: srcHeight, codec: subtitleCodec)
        );

        StageResult result = await _stage.ExecuteAsync(input: input, context: context, ct: default);
        Assert.IsType<StageSuccess<FfmpegCommand[]>>(@object: result);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        int idx = Array.IndexOf(array: commands[0].Arguments, value: "-filter_complex");
        Assert.True(condition: idx >= 0, userMessage: "filter_complex flag must be present");
        return commands[0].Arguments[idx + 1];
    }

    private static MediaInfo BuildMediaInfoWithCodec(int width, int height, string codec) =>
        new(
            FilePath: "/movies/test.mkv",
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
            SubtitleStreams:
            [
                new(
                    Index: 0,
                    Codec: codec,
                    Language: "en",
                    IsDefault: true,
                    IsForced: false,
                    Title: null
                ),
            ],
            Chapters: []
        );

    private static VideoOutputPlan BuildVideoOutput(int width, int height, string mapLabel) =>
        new(
            Width: width,
            Height: height,
            EncoderName: "libx264",
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
}
