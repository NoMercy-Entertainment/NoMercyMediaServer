using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Output;

public class HlsOutputStrategyTests
{
    [Fact]
    public void ConfigureOutput_AddsHlsFlags()
    {
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));
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
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        OutputPlan plan = CreateSimplePlan();

        string[] dirs = strategy.GetOutputSubdirectories(plan);

        dirs.Should().Contain("video_1920x1080_SDR");
        dirs.Should().Contain("audio_eng_aac");
    }

    [Fact]
    public void ConfigureOutput_UsesTemplateForNaming()
    {
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));
        OutputPlan plan = CreateSimplePlan();

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("video_1920x1080_SDR");
        args.Should().Contain("audio_eng_aac");
    }

    [Fact]
    public void ConfigureOutput_HevcEncoder_AddsHvc1Tag()
    {
        // HLS spec requires hvc1 tag for HEVC playback in Safari / iOS;
        // without it Apple devices refuse to play the variant.
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));
        OutputPlan plan = CreateSimplePlan(videoEncoder: "libx265");

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-tag:v hvc1");
    }

    [Fact]
    public void ConfigureOutput_NonHevcEncoder_DoesNotAddHvc1Tag()
    {
        // libx264 must NOT emit the HEVC tag — players reject the stream
        // when the codec parameter and tag don't match.
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));
        OutputPlan plan = CreateSimplePlan(videoEncoder: "libx264");

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().NotContain("-tag:v hvc1");
    }

    [Fact]
    public void ConfigureOutput_DolbyVisionPreserveOnHevc_UsesDvh1Tag()
    {
        // Dolby Vision passes through HEVC only when the muxer tags the
        // stream as dvh1 — hvc1 (regular HEVC) skips the DV decoder.
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));
        OutputPlan plan = CreateSimplePlan(videoEncoder: "libx265") with
        {
            PreserveDolbyVision = true,
        };

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-tag:v dvh1");
        args.Should().NotContain("-tag:v hvc1");
    }

    [Fact]
    public void ConfigureOutput_Fmp4SegmentType_EmitsInitFilename()
    {
        // fMP4 segments share an init.mp4 alongside the playlist. Without
        // -hls_fmp4_init_filename FFmpeg picks a random name and the
        // dashboard manifest can't reference it deterministically.
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));
        OutputPlan plan = CreateSimplePlan() with
        {
            HlsOptions = new HlsPlanOptions { SegmentType = "fmp4" },
        };

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-hls_fmp4_init_filename init.mp4");
        args.Should().Contain("_%05d.m4s");
        args.Should().NotContain("_%05d.ts");
    }

    [Fact]
    public void ConfigureOutput_DefaultSegmentType_EmitsTsSegments()
    {
        // Default = mpegts (.ts) segments — broad player compatibility.
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));
        OutputPlan plan = CreateSimplePlan();

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("_%05d.ts");
        args.Should().NotContain("-hls_fmp4_init_filename");
    }

    [Fact]
    public void ConfigureOutput_IndependentSegmentsFlag_IsAlwaysPresent()
    {
        // independent_segments is the foundation of clean HLS seek behaviour.
        // Players need it to know each segment is independently decodable.
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));
        OutputPlan plan = CreateSimplePlan();

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-hls_flags independent_segments");
    }

    [Fact]
    public void ConfigureOutput_ForceKeyFramesExpr_MatchesSegmentDuration()
    {
        // The -force_key_frames expression must align with the segment
        // duration so every segment starts on a keyframe — non-aligned
        // keyframes break seek and bandwidth switching.
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));
        OutputPlan plan = CreateSimplePlan() with { SegmentDurationSeconds = 4 };

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("expr:gte(t,n_forced*4)");
        args.Should().Contain("-hls_time 4");
    }

    [Fact]
    public void ConfigureOutput_PlaylistTypeVod_Default()
    {
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));
        OutputPlan plan = CreateSimplePlan();

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("-hls_playlist_type vod");
    }

    [Fact]
    public void ConfigureOutput_AudioCopyAction_PreservesStream()
    {
        // Copy = no transcoding. Even so, HLS audio still needs its own
        // segmented playlist alongside the video variant.
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));
        OutputPlan plan = CreateSimplePlan(audioAction: StreamAction.Copy);

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        // Audio segment output still appears in the command.
        args.Should().Contain("audio_eng_aac");
    }

    [Fact]
    public void ConfigureOutput_MixedCodecLadder_EmitsPerRungCodecMapAndTag()
    {
        // A mixed-codec ladder (H.264 1080p + HEVC 720p) must emit each rung's
        // own -c:v, its own -map, and the hvc1 tag on the HEVC rung ONLY — a
        // shared tag would make players reject the H.264 variant.
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));
        OutputPlan plan = CreateMixedCodecPlan();

        strategy.ConfigureOutput(builder, plan, "/output");

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);

        args.Should().Contain("-c:v libx264");
        args.Should().Contain("-c:v libx265");
        // hvc1 belongs only to the HEVC rung — exactly one occurrence overall.
        cmd.Arguments.Count(a => a == "hvc1").Should().Be(1);
        // Each rung maps its own video label; no cross-mapping.
        args.Should().Contain("-map [v0]");
        args.Should().Contain("-map [v1]");
    }

    private static OutputPlan CreateMixedCodecPlan()
    {
        return new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                CreateVideoOutput(1920, 1080, "libx264", "[v0]"),
                CreateVideoOutput(1280, 720, "libx265", "[v1]"),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );
    }

    private static VideoOutputPlan CreateVideoOutput(
        int width,
        int height,
        string encoder,
        string mapLabel
    ) =>
        new(
            Width: width,
            Height: height,
            EncoderName: encoder,
            Crf: 23,
            BitrateKbps: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.0",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: mapLabel,
            ExtraFlags: new(),
            SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
        );

    private static OutputPlan CreateSimplePlan(
        string videoEncoder = "libx264",
        StreamAction audioAction = StreamAction.Transcode
    )
    {
        return new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: videoEncoder,
                    Crf: 23,
                    BitrateKbps: 0,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new(),
                    SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                    PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
                ),
            ],
            AudioOutputs:
            [
                new(
                    EncoderName: "aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: audioAction,
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
