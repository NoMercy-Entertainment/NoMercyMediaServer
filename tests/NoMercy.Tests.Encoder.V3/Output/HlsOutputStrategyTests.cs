namespace NoMercy.Tests.Encoder.V3.Output;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Output;
using NoMercy.Encoder.V3.Pipeline;

public class HlsOutputStrategyTests
{
    [Fact]
    public void ConfigureOutput_AddsFmp4Flags()
    {
        HlsOutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new InputOptions("/input.mkv"));
        OutputPlan plan = CreateSimplePlan();

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-f hls");
        args.Should().Contain("-hls_segment_type fmp4");
        args.Should().Contain("-hls_playlist_type vod");
    }

    [Fact]
    public void GetOutputSubdirectories_ReturnsVideoAndAudioDirs()
    {
        HlsOutputStrategy strategy = new();
        OutputPlan plan = CreateSimplePlan();

        string[] dirs = strategy.GetOutputSubdirectories(plan);

        dirs.Should().Contain("video_1920x1080");
        dirs.Should().Contain("audio_eng_2ch");
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
                    Action: StreamAction.Transcode,
                    Language: "eng",
                    MapLabel: "0:a:0"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );
    }
}
