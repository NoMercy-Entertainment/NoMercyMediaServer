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
using NoMercy.Tests.Encoder.Storage;

public class BuildStageFilterGraphTests
{
    private readonly BuildStage _stage;

    public BuildStageFilterGraphTests()
    {
        EncoderOptions options = new() { FfmpegPathOverride = "ffmpeg" };
        _stage = new(
            options,
            new FontExtractor(TestStorageFactory.CreateLocal()),
            new SubtitleExtractor(),
            OutputStrategyFactoryTestHelper.Create(),
            [],
            NullLogger<BuildStage>.Instance
        );
    }

    // ------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------

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

    private static MediaInfo BuildMediaInfo(int width, int height) =>
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
            SubtitleStreams: [],
            Chapters: []
        );

    private static VideoOutputPlan BuildVideoOutput(
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

    private static AudioOutputPlan BuildAudioOutput() =>
        new(
            EncoderName: "aac",
            BitrateKbps: 192,
            Channels: 2,
            SampleRate: 48000,
            Action: StreamAction.Transcode,
            Language: "en",
            MapLabel: "0:a:0"
        );

    // ------------------------------------------------------------------
    // Test 1: single video same resolution → copy filter
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuildStage_SingleVideoSameResolution_GeneratesCopyFilter()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(320, 180, "[v0]")],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildMediaInfo(320, 180)
        );

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        int filterComplexIdx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        filterComplexIdx.Should().BeGreaterThan(-1, "a -filter_complex argument must be present");

        string filterValue = commands[0].Arguments[filterComplexIdx + 1];
        filterValue.Should().Contain("copy");
    }

    // ------------------------------------------------------------------
    // Test 2: single video with scaling → scale filter
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuildStage_SingleVideoWithScaling_GeneratesScaleFilter()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput(1280, 720, "[v0]")],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildMediaInfo(1920, 1080)
        );

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        int filterComplexIdx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        filterComplexIdx.Should().BeGreaterThan(-1, "a -filter_complex argument must be present");

        string filterValue = commands[0].Arguments[filterComplexIdx + 1];
        filterValue.Should().Contain("scale=1280:-2");
    }

    // ------------------------------------------------------------------
    // Test 3: multiple video outputs → split + scale/copy
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuildStage_MultipleVideos_GeneratesSplitAndScale()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                BuildVideoOutput(1920, 1080, "[v0]"),
                BuildVideoOutput(1280, 720, "[v1]"),
            ],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildMediaInfo(1920, 1080)
        );

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        int filterComplexIdx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        filterComplexIdx.Should().BeGreaterThan(-1, "a -filter_complex argument must be present");

        string filterValue = commands[0].Arguments[filterComplexIdx + 1];
        filterValue.Should().Contain("split=2");
        filterValue.Should().Contain("copy");
        filterValue.Should().Contain("scale=1280:-2");
    }

    // ------------------------------------------------------------------
    // Test 4: audio-only profile → no filter_complex in command args
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuildStage_AudioOnlyProfile_NoFilterComplex()
    {
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildMediaInfo(1920, 1080)
        );

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        commands[0].Arguments.Should().NotContain("-filter_complex");
    }

    // ------------------------------------------------------------------
    // Test 5: video with CropFilter → crop=... filter emitted
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuildStage_CropFilterSet_EmitsCropFilterBeforeScale()
    {
        VideoOutputPlan videoWithCrop = BuildVideoOutput(1280, 720, "[v0]") with
        {
            CropFilter = "1920:800:0:140",
        };

        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [videoWithCrop],
            AudioOutputs: [BuildAudioOutput()],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        ExecutionPlan plan = BuildPlan(outputPlan);
        BuildInput input = new(plan, "/movies/test.mkv", "/output/test", "Test.NoMercy");
        EncodingContext context = new(
            EncodingContext.Create().CorrelationId,
            BuildMediaInfo(1920, 1080)
        );

        StageResult result = await _stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;

        int filterComplexIdx = Array.IndexOf(commands[0].Arguments, "-filter_complex");
        string filterValue = commands[0].Arguments[filterComplexIdx + 1];

        filterValue.Should().Contain("crop=1920:800:0:140");

        // Crop must appear before scale so the scaler operates on the cropped picture.
        int cropIdx = filterValue.IndexOf("crop=", StringComparison.Ordinal);
        int scaleIdx = filterValue.IndexOf("scale=", StringComparison.Ordinal);
        cropIdx.Should().BeLessThan(scaleIdx);
    }
}
