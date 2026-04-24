namespace NoMercy.Tests.Encoder.Pipeline.Stages;

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
using NoMercy.Tests.Encoder.Storage;

public class BuildStageBurnInTests
{
    private readonly BuildStage _stage;

    public BuildStageBurnInTests()
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
    public async Task BurnIn_AddsSubtitlesFilterToVideoChain()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(1920, 1080, "[v0]")],
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
                    Mode: SubtitleMode.BurnIn
                ),
            ],
            Thumbnails: null
        );

        string filterValue = await GetFilterComplex(outputPlan, "/movies/test.mkv", 1920, 1080);

        Assert.Contains("subtitles=", filterValue);
        Assert.Contains(":si=0", filterValue);
    }

    [Fact]
    public async Task BurnIn_EscapesColonsInInputPath()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(1920, 1080, "[v0]")],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.Ass,
                    Action: StreamAction.Transcode,
                    Language: "en",
                    SourceIndex: 0,
                    MapLabel: "0:s:0",
                    Mode: SubtitleMode.BurnIn
                ),
            ],
            Thumbnails: null
        );

        string filterValue = await GetFilterComplex(outputPlan, "C:/movies/test.mkv", 1920, 1080);

        Assert.Contains("C\\:/movies/test.mkv", filterValue);
    }

    [Fact]
    public async Task BurnIn_DoesNotEmitSeparateSubtitleOutput()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(1920, 1080, "[v0]")],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.Ass,
                    Action: StreamAction.Transcode,
                    Language: "en",
                    SourceIndex: 0,
                    MapLabel: "0:s:0",
                    Mode: SubtitleMode.BurnIn
                ),
            ],
            Thumbnails: null
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildMediaInfoWithSubtitle(1920, 1080, textBased: true)
        );

        StageResult result = await _stage.ExecuteAsync(input, context, default);
        Assert.IsType<StageSuccess<FfmpegCommand[]>>(result);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        // Count outputs in the main command — should be video only (no subtitle output).
        // -map 0:s:X flag would indicate a subtitle stream output.
        string args = string.Join(" ", commands[0].Arguments);
        Assert.DoesNotContain("-map 0:s:", args);
    }

    [Fact]
    public async Task BurnIn_AppliedAfterScale()
    {
        // When scaling + burn-in: filter chain should be scale → subtitles, not subtitles → scale.
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(1280, 720, "[v0]")],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.Ass,
                    Action: StreamAction.Transcode,
                    Language: "en",
                    SourceIndex: 0,
                    MapLabel: "0:s:0",
                    Mode: SubtitleMode.BurnIn
                ),
            ],
            Thumbnails: null
        );

        string filterValue = await GetFilterComplex(outputPlan, "/movies/test.mkv", 1920, 1080);

        int scaleIdx = filterValue.IndexOf("scale=1280", StringComparison.Ordinal);
        int burnIdx = filterValue.IndexOf("subtitles=", StringComparison.Ordinal);

        Assert.True(scaleIdx >= 0, "scale filter must be present");
        Assert.True(burnIdx >= 0, "subtitles filter must be present");
        Assert.True(scaleIdx < burnIdx, "burn-in must come after scale in the filter chain");
    }

    [Fact]
    public async Task ExtractMode_DoesNotAddSubtitlesFilter()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(1920, 1080, "[v0]")],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.WebVtt,
                    Action: StreamAction.Extract,
                    Language: "en",
                    SourceIndex: 0,
                    MapLabel: "0:s:0",
                    Mode: SubtitleMode.Extract
                ),
            ],
            Thumbnails: null
        );

        string filterValue = await GetFilterComplex(outputPlan, "/movies/test.mkv", 1920, 1080);

        Assert.DoesNotContain("subtitles=", filterValue);
    }

    private async Task<string> GetFilterComplex(
        OutputPlan outputPlan,
        string inputPath,
        int srcWidth,
        int srcHeight
    )
    {
        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, inputPath, "/output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildMediaInfoWithSubtitle(srcWidth, srcHeight, textBased: true)
        );

        StageResult result = await _stage.ExecuteAsync(input, context, default);
        Assert.IsType<StageSuccess<FfmpegCommand[]>>(result);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        int idx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        Assert.True(idx >= 0, "filter_complex flag must be present");
        return commands[0].Arguments[idx + 1];
    }

    private static ExecutionPlan BuildPlan(OutputPlan outputPlan) =>
        new(
            Groups:
            [
                new(
                    GroupId: "group_0",
                    Nodes: [new("decode_0", OperationType.Decode, [], new())],
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 4,
                    RequiresGpu: false,
                    Priority: 1
                ),
            ],
            EstimatedTotalDuration: TimeSpan.FromMinutes(90),
            OutputPlan: outputPlan
        );

    private static MediaInfo BuildMediaInfoWithSubtitle(int width, int height, bool textBased) =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(2),
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
