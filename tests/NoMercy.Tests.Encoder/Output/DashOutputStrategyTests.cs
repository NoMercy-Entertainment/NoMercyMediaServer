using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Output;

public class DashOutputStrategyTests
{
    [Fact]
    public void ConfigureOutput_HasDashFormat()
    {
        DashOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlan(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-f dash");
    }

    [Fact]
    public void ConfigureOutput_HasAdaptationSets()
    {
        DashOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlan(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-adaptation_sets");
    }

    [Fact]
    public void ConfigureOutput_ProducesMpdOutput()
    {
        DashOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlan(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().Contain(a => a.Contains("manifest.mpd"));
    }

    [Fact]
    public void ConfigureOutput_UsesTemplateAndTimeline()
    {
        // DASH dynamic playlists need both -use_template and -use_timeline
        // for live + on-demand support; missing either flag breaks shaka /
        // dash.js playback.
        DashOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlan(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-use_template 1");
        args.Should().Contain("-use_timeline 1");
    }

    [Fact]
    public void ConfigureOutput_AdaptationSets_GroupsVideoAndAudio()
    {
        // Adaptation set ids 0=video, 1=audio — shape verified by the
        // dash.js / shaka.player adapter selection logic.
        DashOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlan(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("id=0,streams=v id=1,streams=a");
    }

    [Fact]
    public void ConfigureOutput_SegmentNames_UseRepresentationIdPlaceholder()
    {
        // Init and media segment names use $RepresentationID$ — the DASH
        // spec placeholder ffmpeg expands per stream. Hard-coded names
        // would collide on multi-variant outputs.
        DashOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, CreatePlan(), "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("init_$RepresentationID$.m4s");
        args.Should().Contain("seg_$RepresentationID$_$Number%05d$.m4s");
    }

    [Fact]
    public void ConfigureOutput_RespectsCustomSegmentDuration()
    {
        DashOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        OutputPlan plan = CreatePlan() with { SegmentDurationSeconds = 4 };
        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-seg_duration 4");
    }

    [Fact]
    public void ConfigureOutput_AudioCopy_EmitsCopyToken()
    {
        DashOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        OutputPlan plan = CreatePlan() with
        {
            AudioOutputs = [new("aac", 0, 2, 48000, StreamAction.Copy, "eng", "0:a:0")],
        };

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-c:a copy");
    }

    [Fact]
    public void ConfigureOutput_AudioFilter_AppliedWhenTranscoding()
    {
        DashOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        OutputPlan plan = CreatePlan() with
        {
            AudioOutputs =
            [
                new("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "0:a:0")
                {
                    AudioFilter = "loudnorm=I=-16",
                },
            ],
        };

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("loudnorm=I=-16");
    }

    [Fact]
    public void ConfigureOutput_DropAudio_NotMapped()
    {
        DashOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        OutputPlan plan = CreatePlan() with
        {
            AudioOutputs = [new("aac", 0, 2, 48000, StreamAction.Drop, "eng", "0:a:0")],
        };

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().NotContain("-map 0:a:0");
    }

    [Fact]
    public void Format_IsDash()
    {
        new DashOutputStrategy(TestStorageFactory.CreateLocal())
            .Format.Should()
            .Be(OutputFormat.Dash);
    }

    [Fact]
    public void GetOutputSubdirectories_ReturnsEmpty()
    {
        new DashOutputStrategy(TestStorageFactory.CreateLocal())
            .GetOutputSubdirectories(CreatePlan())
            .Should()
            .BeEmpty();
    }

    private static OutputPlan CreatePlan() =>
        new(
            Format: OutputFormat.Dash,
            VideoOutputs:
            [
                new(
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
                    new()
                ),
            ],
            AudioOutputs: [new("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null
        );
}
