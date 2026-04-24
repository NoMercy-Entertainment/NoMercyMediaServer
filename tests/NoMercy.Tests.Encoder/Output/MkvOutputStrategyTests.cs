namespace NoMercy.Tests.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Tests.Encoder.Storage;

public class MkvOutputStrategyTests
{
    [Fact]
    public void ConfigureOutput_ProducesOutputMkv()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, CreateSimplePlan(OutputFormat.Mkv), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().Contain(a => a.Contains("output.mkv"));
    }

    [Fact]
    public void ConfigureOutput_MapsAllStreams()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, CreateSimplePlan(OutputFormat.Mkv), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-map [v0]");
        args.Should().Contain("-map 0:a:0");
    }

    [Fact]
    public void GetOutputSubdirectories_ReturnsEmpty()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        strategy.GetOutputSubdirectories(CreateSimplePlan(OutputFormat.Mkv)).Should().BeEmpty();
    }

    private static OutputPlan CreateSimplePlan(OutputFormat format) =>
        new(
            Format: format,
            VideoOutputs:
            [
                new(
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
                    new()
                ),
            ],
            AudioOutputs: [new("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null
        );
}
