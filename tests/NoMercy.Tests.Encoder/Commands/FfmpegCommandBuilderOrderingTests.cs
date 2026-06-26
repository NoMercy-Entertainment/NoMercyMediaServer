using NoMercy.Encoder.Commands;

namespace NoMercy.Tests.Encoder.Commands;

/// <summary>
/// Sharpens <see cref="FfmpegCommandBuilderTests"/> with the structural
/// guarantees the existing presence-only tests don't enforce:
///
/// • Arg-block ORDER (global → input → filter_complex → output → output-path).
///   FFmpeg interprets options based on positional context — `-hwaccel` after
///   `-i` is silently dropped, `-filter_complex` after the output path treats
///   the filter as a new input.
/// • Culture-invariant decimal formatting for `-ss` / `-t`. On de-DE / nl-NL
///   the default culture uses comma — without InvariantCulture the seek arg
///   becomes "12,345" which ffmpeg reads as 12,345 seconds.
/// • Disabling default globals — Overwrite/HideBanner/ProgressPipe each
///   default true; tests must prove they actually drop out when set false.
/// • Output path is ALWAYS last in its output block. Anything emitted after
///   would be parsed as a new input.
/// • Builder fluency — every public method must return the builder.
///
/// Each failure mode pinned here corresponds to a real ffmpeg behavior cliff,
/// not a stylistic preference.
/// </summary>
public class FfmpegCommandBuilderOrderingTests
{
    // ── Disabling default globals ────────────────────────────────────────────

    [Fact]
    public void All_global_flags_disabled_emits_no_args()
    {
        // Proves the builder doesn't hard-code -y / -hide_banner / -progress.
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .Build("ffmpeg");

        cmd.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void Overwrite_false_drops_dash_y()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Overwrite: false))
            .Build("ffmpeg");

        cmd.Arguments.Should().NotContain("-y");
    }

    [Fact]
    public void HideBanner_false_drops_hide_banner()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(HideBanner: false))
            .Build("ffmpeg");

        cmd.Arguments.Should().NotContain("-hide_banner");
    }

    [Fact]
    public void ProgressPipe_false_drops_progress_arg_and_value()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(ProgressPipe: false))
            .Build("ffmpeg");

        cmd.Arguments.Should().NotContain("-progress").And.NotContain("pipe:1");
    }

    // ── AnalyzeDurationUs (uncovered by existing tests) ──────────────────────

    [Fact]
    public void AnalyzeDurationUs_emits_dash_analyzeduration()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(
                new(
                    Overwrite: false,
                    HideBanner: false,
                    ProgressPipe: false,
                    AnalyzeDurationUs: 5_000_000
                )
            )
            .Build("ffmpeg");

        cmd.Arguments.Should().Equal("-analyzeduration", "5000000");
    }

    // ── Culture-invariant decimal formatting ─────────────────────────────────

    [Theory]
    [InlineData("de-DE")]
    [InlineData("nl-NL")]
    [InlineData("fr-FR")]
    public void SeekTo_uses_invariant_culture_on_comma_decimal_locales(string culture)
    {
        // Without CultureInfo.InvariantCulture, TimeSpan.TotalSeconds.ToString("F3")
        // emits "12,345" on these cultures — ffmpeg then parses it as 12345 seconds.
        // This is a 4-hour seek when the user asked for 12.345 seconds.
        System.Globalization.CultureInfo previous = Thread
            .CurrentThread
            .CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(culture);
            FfmpegCommand cmd = new FfmpegCommandBuilder()
                .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
                .AddInput(new("/in.mkv", SeekTo: TimeSpan.FromMilliseconds(12_345)))
                .Build("ffmpeg");

            cmd.Arguments.Should().Contain("12.345").And.NotContain("12,345");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("nl-NL")]
    public void Duration_uses_invariant_culture_on_comma_decimal_locales(string culture)
    {
        System.Globalization.CultureInfo previous = Thread
            .CurrentThread
            .CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(culture);
            FfmpegCommand cmd = new FfmpegCommandBuilder()
                .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
                .AddInput(new("/in.mkv", Duration: TimeSpan.FromMilliseconds(7_500)))
                .Build("ffmpeg");

            cmd.Arguments.Should().Contain("7.500").And.NotContain("7,500");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    // ── Per-input arg ordering ───────────────────────────────────────────────

    [Fact]
    public void Hwaccel_args_appear_before_dash_i()
    {
        // FFmpeg pairs -hwaccel with the NEXT declared input. Emitting it after
        // -i silently drops the acceleration.
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(new("/in.mkv", HwAccelDevice: "cuda", HwAccelOutputFormat: "cuda"))
            .Build("ffmpeg");

        int hwIndex = Array.IndexOf(cmd.Arguments, "-hwaccel");
        int hwOutIndex = Array.IndexOf(cmd.Arguments, "-hwaccel_output_format");
        int iIndex = Array.IndexOf(cmd.Arguments, "-i");

        hwIndex.Should().BeLessThan(iIndex);
        hwOutIndex.Should().BeLessThan(iIndex);
    }

    [Fact]
    public void Seek_and_duration_appear_before_dash_i()
    {
        // -ss / -t are pre-input options — must appear before -i to affect THAT input.
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(
                new("/in.mkv", SeekTo: TimeSpan.FromSeconds(5), Duration: TimeSpan.FromSeconds(10))
            )
            .Build("ffmpeg");

        int ssIndex = Array.IndexOf(cmd.Arguments, "-ss");
        int tIndex = Array.IndexOf(cmd.Arguments, "-t");
        int iIndex = Array.IndexOf(cmd.Arguments, "-i");

        ssIndex.Should().BeLessThan(iIndex);
        tIndex.Should().BeLessThan(iIndex);
    }

    [Fact]
    public void Multiple_inputs_preserve_declared_order()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(new("/a.mkv"))
            .AddInput(new("/b.mkv"))
            .AddInput(new("/c.mkv"))
            .Build("ffmpeg");

        int a = Array.IndexOf(cmd.Arguments, "/a.mkv");
        int b = Array.IndexOf(cmd.Arguments, "/b.mkv");
        int c = Array.IndexOf(cmd.Arguments, "/c.mkv");

        a.Should().BeLessThan(b);
        b.Should().BeLessThan(c);
    }

    // ── Filter complex sits between inputs and outputs ───────────────────────

    [Fact]
    public void Filter_complex_sits_between_last_input_and_first_output_codec()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(new("/in.mkv"))
            .WithFilterComplex("[0:v]scale=1280:720[v]")
            .AddOutput(new("/out.mp4", VideoCodec: "libx264"))
            .Build("ffmpeg");

        int iIndex = Array.IndexOf(cmd.Arguments, "-i");
        int filterIndex = Array.IndexOf(cmd.Arguments, "-filter_complex");
        int codecIndex = Array.IndexOf(cmd.Arguments, "-c:v");
        int outIndex = Array.IndexOf(cmd.Arguments, "/out.mp4");

        iIndex.Should().BeLessThan(filterIndex);
        filterIndex.Should().BeLessThan(codecIndex);
        codecIndex.Should().BeLessThan(outIndex);
    }

    // ── Per-output ordering: map → codec → quality → output path ─────────────

    [Fact]
    public void Map_streams_appear_before_codec_args()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddOutput(
                new(
                    FilePath: "/out.mp4",
                    VideoCodec: "libx264",
                    AudioCodec: "aac",
                    MapStreams: ["0:v:0", "0:a:0"]
                )
            )
            .Build("ffmpeg");

        int firstMapIndex = Array.IndexOf(cmd.Arguments, "-map");
        int videoCodecIndex = Array.IndexOf(cmd.Arguments, "-c:v");

        firstMapIndex.Should().BeLessThan(videoCodecIndex);
    }

    [Fact]
    public void Multiple_map_streams_emit_dash_map_for_each_in_order()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddOutput(new(FilePath: "/out.mp4", MapStreams: ["0:v:0", "0:a:0", "0:s:0"]))
            .Build("ffmpeg");

        int mapCount = cmd.Arguments.Count(a => a == "-map");
        mapCount.Should().Be(3);
        cmd.Arguments.Should().ContainInOrder("-map", "0:v:0", "-map", "0:a:0", "-map", "0:s:0");
    }

    [Fact]
    public void Video_bitrate_and_audio_bitrate_carry_k_suffix()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddOutput(new(FilePath: "/out.mp4", VideoBitrateKbps: 5000, AudioBitrateKbps: 192))
            .Build("ffmpeg");

        cmd.Arguments.Should().Contain("5000k").And.Contain("192k");
        // k suffix is mandatory — a bare "5000" would mean bits/sec, not kbps.
        cmd.Arguments.Should().NotContain("5000");
        cmd.Arguments.Should().NotContain("192");
    }

    [Fact]
    public void Audio_channels_and_sample_rate_emit_dash_ac_and_dash_ar()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddOutput(new(FilePath: "/out.mp4", AudioChannels: "2", AudioSampleRate: 48000))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInOrder("-ac", "2", "-ar", "48000");
    }

    [Fact]
    public void Keyframe_interval_emits_dash_g()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddOutput(new(FilePath: "/out.mp4", KeyframeInterval: 120))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInOrder("-g", "120");
    }

    [Fact]
    public void Output_path_is_always_the_last_arg_of_its_block()
    {
        // Per output: FilePath emitted after ALL per-output args. Nothing should
        // sit between any codec/quality flag and the path.
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddOutput(
                new(
                    FilePath: "/out.mp4",
                    VideoCodec: "libx264",
                    AudioCodec: "aac",
                    Crf: 23,
                    Preset: "slow",
                    Profile: "high",
                    Level: "4.0",
                    PixelFormat: "yuv420p",
                    VideoBitrateKbps: 5000,
                    AudioBitrateKbps: 192,
                    KeyframeInterval: 120,
                    AudioChannels: "2",
                    AudioSampleRate: 48000,
                    MapStreams: ["0:v:0"],
                    ExtraFlags: new() { ["-tune"] = "film" }
                )
            )
            .Build("ffmpeg");

        cmd.Arguments[^1].Should().Be("/out.mp4");
    }

    [Fact]
    public void Multiple_outputs_each_have_path_after_their_per_output_args()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddOutput(new(FilePath: "/a.mp4", VideoCodec: "libx264"))
            .AddOutput(new(FilePath: "/b.mp4", VideoCodec: "libx265"))
            .Build("ffmpeg");

        // The block must be: -c:v libx264 /a.mp4 -c:v libx265 /b.mp4
        cmd.Arguments.Should().Equal("-c:v", "libx264", "/a.mp4", "-c:v", "libx265", "/b.mp4");
    }

    // ── End-to-end golden order ──────────────────────────────────────────────

    [Fact]
    public void End_to_end_global_then_input_then_filter_then_output_then_path()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Threads: 4))
            .AddInput(new("/in.mkv", SeekTo: TimeSpan.FromSeconds(5)))
            .WithFilterComplex("[0:v]scale=1280:720[v]")
            .AddOutput(new(FilePath: "/out.mp4", VideoCodec: "libx264", Crf: 23))
            .Build("ffmpeg");

        int yIndex = Array.IndexOf(cmd.Arguments, "-y");
        int threadsIndex = Array.IndexOf(cmd.Arguments, "-threads");
        int ssIndex = Array.IndexOf(cmd.Arguments, "-ss");
        int iIndex = Array.IndexOf(cmd.Arguments, "-i");
        int filterIndex = Array.IndexOf(cmd.Arguments, "-filter_complex");
        int codecIndex = Array.IndexOf(cmd.Arguments, "-c:v");

        yIndex.Should().BeLessThan(threadsIndex);
        threadsIndex.Should().BeLessThan(ssIndex);
        ssIndex.Should().BeLessThan(iIndex);
        iIndex.Should().BeLessThan(filterIndex);
        filterIndex.Should().BeLessThan(codecIndex);
        cmd.Arguments[^1].Should().Be("/out.mp4");
    }

    // ── Builder fluency ──────────────────────────────────────────────────────

    [Fact]
    public void Every_builder_method_returns_self_for_chaining()
    {
        FfmpegCommandBuilder builder = new();

        builder.WithGlobalOptions(new()).Should().BeSameAs(builder);
        builder.AddInput(new("/in.mkv")).Should().BeSameAs(builder);
        builder.WithFilterComplex("noop").Should().BeSameAs(builder);
        builder.AddOutput(new("/out.mp4")).Should().BeSameAs(builder);
    }
}
