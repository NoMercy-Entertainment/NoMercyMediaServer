namespace NoMercy.Tests.Encoder.V3.Output;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Output;
using NoMercy.Encoder.V3.Pipeline;

public class DashOutputStrategyTests
{
    [Fact]
    public void ConfigureOutput_HasDashFormat()
    {
        DashOutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new InputOptions("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlan(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-f dash");
    }

    [Fact]
    public void ConfigureOutput_HasAdaptationSets()
    {
        DashOutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new InputOptions("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlan(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-adaptation_sets");
    }

    [Fact]
    public void ConfigureOutput_ProducesMpdOutput()
    {
        DashOutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new InputOptions("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlan(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().Contain(a => a.Contains("manifest.mpd"));
    }

    private static OutputPlan CreatePlan() =>
        new(
            Format: OutputFormat.Dash,
            VideoOutputs:
            [
                new VideoOutputPlan(
                    1920,
                    1080,
                    "libx264",
                    23,
                    8000,
                    "medium",
                    "high",
                    "4.0",
                    false,
                    "yuv420p",
                    "[v0]",
                    new Dictionary<string, string>()
                ),
            ],
            AudioOutputs:
            [
                new AudioOutputPlan("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "0:a:0"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );
}
