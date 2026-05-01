using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Profiles;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Output;

public class HlsOutputStrategyOptionsTests
{
    private static VideoOutputPlan BuildVideo() =>
        new(
            Width: 1920,
            Height: 1080,
            EncoderName: "libx264",
            Crf: 23,
            BitrateKbps: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v0]",
            ExtraFlags: new()
        );

    private static OutputPlan BuildPlan(HlsOptions? hlsOptions = null) =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideo()],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            HlsOptions: hlsOptions
        );

    private static string[] BuildArgs(OutputPlan plan)
    {
        HlsOutputStrategy strategy = new(Mock.Of<IStorage>());
        FfmpegCommandBuilder builder = new();
        strategy.ConfigureOutput(builder, plan, "/output");
        return builder.Build("ffmpeg").Arguments;
    }

    // Returns the value of a flag from a flat args array.
    // e.g. FlagValue(["-hls_playlist_type", "event", ...], "-hls_playlist_type") → "event"
    private static string? FlagValue(string[] args, string flag)
    {
        int idx = Array.IndexOf(args, flag);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    [Fact]
    public void Build_includes_hls_playlist_type_event_when_profile_set()
    {
        OutputPlan plan = BuildPlan(new HlsOptions { PlaylistType = "event" });

        string[] args = BuildArgs(plan);

        Assert.Equal("event", FlagValue(args, "-hls_playlist_type"));
    }

    [Fact]
    public void Build_includes_hls_segment_type_fmp4_when_profile_set()
    {
        OutputPlan plan = BuildPlan(new HlsOptions { SegmentType = "fmp4" });

        string[] args = BuildArgs(plan);

        Assert.Equal("fmp4", FlagValue(args, "-hls_segment_type"));
        Assert.Equal("init.mp4", FlagValue(args, "-hls_fmp4_init_filename"));
    }

    [Fact]
    public void Build_includes_independent_segments_in_flags_when_set()
    {
        OutputPlan plan = BuildPlan(new HlsOptions { IndependentSegments = true });

        string[] args = BuildArgs(plan);

        string? hlsFlags = FlagValue(args, "-hls_flags");
        Assert.NotNull(hlsFlags);
        Assert.Contains("independent_segments", hlsFlags, StringComparison.OrdinalIgnoreCase);
    }
}
