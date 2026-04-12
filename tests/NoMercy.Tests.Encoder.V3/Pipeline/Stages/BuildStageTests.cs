namespace NoMercy.Tests.Encoder.V3.Pipeline.Stages;

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Composition;
using NoMercy.Encoder.V3.Output;
using NoMercy.Encoder.V3.Pipeline;
using NoMercy.Encoder.V3.Pipeline.Optimizer;
using NoMercy.Encoder.V3.Pipeline.Stages;

public class BuildStageTests
{
    private readonly BuildStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public BuildStageTests()
    {
        EncoderOptions options = new("ffmpeg", "ffprobe");
        _stage = new BuildStage(options, NullLogger<BuildStage>.Instance);
    }

    private static ExecutionPlan BuildHlsPlan() =>
        new(
            Groups:
            [
                new ExecutionGroup(
                    GroupId: "group_0",
                    Nodes:
                    [
                        new ExecutionNode(
                            "decode_0",
                            OperationType.Decode,
                            [],
                            new Dictionary<string, string>()
                        ),
                        new ExecutionNode(
                            "encode_0",
                            OperationType.Encode,
                            ["decode_0"],
                            new Dictionary<string, string>()
                        ),
                    ],
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 4,
                    RequiresGpu: false,
                    Priority: 1
                ),
            ],
            EstimatedTotalDuration: TimeSpan.FromMinutes(90),
            OutputPlan: new OutputPlan(
                Format: OutputFormat.Hls,
                VideoOutputs:
                [
                    new VideoOutputPlan(
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
                        ExtraFlags: new Dictionary<string, string>()
                    ),
                ],
                AudioOutputs:
                [
                    new AudioOutputPlan(
                        EncoderName: "aac",
                        BitrateKbps: 192,
                        Channels: 2,
                        SampleRate: 48000,
                        Action: NoMercy.Encoder.V3.Pipeline.StreamAction.Transcode,
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
        BuildInput input = new(plan, "/movies/test.mkv", "/output/test");

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

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
        BuildInput input = new(plan, "/movies/test.mkv", "/output/test");

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        commands[0].Executable.Should().Be("ffmpeg");
    }

    // ------------------------------------------------------------------
    // Built command references the input file
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuiltCommand_ContainsInputPath()
    {
        ExecutionPlan plan = BuildHlsPlan();
        BuildInput input = new(plan, "/movies/test.mkv", "/output/test");

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        commands[0].Arguments.Should().Contain("/movies/test.mkv");
    }

    // ------------------------------------------------------------------
    // Built command references the encoder
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuiltCommand_ContainsVideoEncoder()
    {
        ExecutionPlan plan = BuildHlsPlan();
        BuildInput input = new(plan, "/movies/test.mkv", "/output/test");

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        commands[0].Arguments.Should().Contain("libx264");
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
        BuildInput input = new(plan, "/movies/test.mkv", "/output/test");

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
    }
}
