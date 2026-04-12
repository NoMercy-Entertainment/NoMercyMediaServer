namespace NoMercy.Tests.Encoder.V3.Output;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Output;
using NoMercy.Encoder.V3.Pipeline;

public class Mp4OutputStrategyTests
{
    [Fact]
    public void ConfigureOutput_HasFaststart()
    {
        Mp4OutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new InputOptions("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlan(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-movflags +faststart");
    }

    [Fact]
    public void ConfigureOutput_ProducesMp4Output()
    {
        Mp4OutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new InputOptions("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlan(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().Contain(a => a.Contains("output.mp4"));
    }

    private static OutputPlan CreatePlan() =>
        new(
            Format: OutputFormat.Mp4,
            VideoOutputs:
            [
                new VideoOutputPlan(
                    1920,
                    1080,
                    "libx264",
                    23,
                    0,
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
