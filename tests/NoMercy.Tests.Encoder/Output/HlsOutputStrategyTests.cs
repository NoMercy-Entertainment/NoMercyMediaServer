namespace NoMercy.Tests.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

public class HlsOutputStrategyTests
{
    [Fact]
    public void ConfigureOutput_AddsHlsFlags()
    {
        HlsOutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new InputOptions("/input.mkv"));
        OutputPlan plan = CreateSimplePlan();

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-f hls");
        args.Should().Contain("-hls_playlist_type vod");
        args.Should().Contain("_%05d.ts");
    }

    [Fact]
    public void GetOutputSubdirectories_ReturnsTemplateResolvedDirs()
    {
        HlsOutputStrategy strategy = new();
        OutputPlan plan = CreateSimplePlan();

        string[] dirs = strategy.GetOutputSubdirectories(plan);

        dirs.Should().Contain("video_1920x1080_SDR");
        dirs.Should().Contain("audio_eng_aac");
    }

    [Fact]
    public void ConfigureOutput_UsesTemplateForNaming()
    {
        HlsOutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new InputOptions("/input.mkv"));
        OutputPlan plan = CreateSimplePlan();

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("video_1920x1080_SDR");
        args.Should().Contain("audio_eng_aac");
    }

    private static OutputPlan CreateSimplePlan()
    {
        return new OutputPlan(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new VideoOutputPlan(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 0,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new Dictionary<string, string>(),
                    SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                    PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
                ),
            ],
            AudioOutputs:
            [
                new AudioOutputPlan(
                    EncoderName: "aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: "eng",
                    MapLabel: "0:a:0",
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );
    }
}
