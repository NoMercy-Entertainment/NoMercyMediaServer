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

        StageResult result = await ExecuteBuild(plan, EncodingPass.One, statsFilePath: null);

        Assert.IsType<StageFailure>(result);
        Assert.Contains("StatsFilePath", ((StageFailure)result).Error.Message);
    }

    [Fact]
    public async Task Pass1_WithMultipleVideoOutputs_EncodesTargetVariantOnly()
    {
        // Multi-variant profiles are now supported — pass 1 picks the variant
        // by Pass1VariantIndex (default 0) and produces that variant's stats.
        OutputPlan plan = PlanWith(BuildVideo(1920, 1080, "[v0]"), BuildVideo(1280, 720, "[v1]"));

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
    public async Task SinglePass_DoesNotEmitPassFlags()
    {
        OutputPlan plan = PlanWith(BuildVideo(1920, 1080, "[v0]"));

        FfmpegCommand[] commands = await RunBuild(plan, EncodingPass.Single, statsFilePath: null);

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
            InputPath: "/media/test.mkv",
            OutputDirectory: Path.Combine(Path.GetTempPath(), $"bs-{Guid.NewGuid():N}"),
            MediaTitle: "Test.NoMercy",
            DurationLimit: null,
            Pass: pass,
            StatsFilePath: statsFilePath
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

    private static OutputPlan PlanWith(params VideoOutputPlan[] video) =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs: video,
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

    private static VideoOutputPlan BuildVideo(int width, int height, string mapLabel) =>
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

    private static MediaInfo BuildMediaInfo(int width, int height) =>
        new(
            FilePath: "/media/test.mkv",
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
            SubtitleStreams: [],
            Chapters: []
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
