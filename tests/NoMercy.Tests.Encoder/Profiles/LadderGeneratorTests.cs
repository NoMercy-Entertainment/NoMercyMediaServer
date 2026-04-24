namespace NoMercy.Tests.Encoder.Profiles;

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;

public class LadderGeneratorTests
{
    private readonly LadderGenerator _gen = new();

    // ── helpers ─────────────────────────────────────────────────────────────

    private static VideoOutput Reference(
        VideoCodecType codec = VideoCodecType.H264,
        string preset = "slow",
        int crf = 22
    ) =>
        new(
            Codec: codec,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 5000,
            Crf: crf,
            Preset: preset,
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );

    private static VideoStreamInfo Source(int width, int height, long bitrateKbps = 6000) =>
        new(
            Index: 0,
            Codec: "h264",
            Width: width,
            Height: height,
            FrameRate: 24.0,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: null,
            ColorTransfer: null,
            ColorSpace: null,
            IsDefault: true,
            BitRateKbps: bitrateKbps
        );

    private static ScopedDecisionLog Sink() => new();

    // ── tests ────────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_uses_user_rungs_verbatim_when_supplied()
    {
        LadderRung[] userRungs =
        [
            new(Width: 1280, Height: 720, BitrateKbps: 3000),
            new(Width: 854, Height: 480, BitrateKbps: 1500),
        ];

        IReadOnlyList<VideoOutput> result = _gen.Generate(
            Reference(),
            Source(1920, 1080),
            userRungs,
            Sink()
        );

        Assert.Equal(2, result.Count);
        Assert.Equal(1280, result[0].Width);
        Assert.Equal(720, result[0].Height);
        Assert.Equal(3000, result[0].BitrateKbps);
        Assert.Equal(854, result[1].Width);
        Assert.Equal(480, result[1].Height);
        Assert.Equal(1500, result[1].BitrateKbps);
    }

    [Fact]
    public void Generate_skips_rungs_above_source_resolution()
    {
        ScopedDecisionLog sink = Sink();

        IReadOnlyList<VideoOutput> result = _gen.Generate(
            Reference(),
            Source(1280, 720),
            null,
            sink
        );

        Assert.All(result, v => Assert.True(v.Height <= 720));
        Assert.DoesNotContain(result, v => v.Height > 720);

        IReadOnlyList<DecisionLog> decisions = sink.Snapshot();
        Assert.Contains(decisions, d => d.Key == "plan.ladder_rung_skipped_above_source");
    }

    [Fact]
    public void Generate_includes_360p_rung_for_720p_source()
    {
        IReadOnlyList<VideoOutput> result = _gen.Generate(
            Reference(),
            Source(1280, 720),
            null,
            Sink()
        );

        Assert.Contains(result, v => v.Height == 360);
    }

    [Fact]
    public void Generate_animated_thins_rungs()
    {
        ScopedDecisionLog sink = Sink();

        IReadOnlyList<VideoOutput> liveAction = _gen.Generate(
            Reference(),
            Source(1920, 1080),
            null,
            Sink(),
            ComplexityHint.LiveAction
        );

        IReadOnlyList<VideoOutput> animated = _gen.Generate(
            Reference(),
            Source(1920, 1080),
            null,
            sink,
            ComplexityHint.Animated
        );

        Assert.True(
            animated.Count < liveAction.Count,
            $"Animated ({animated.Count}) should have fewer rungs than LiveAction ({liveAction.Count})"
        );

        IReadOnlyList<DecisionLog> decisions = sink.Snapshot();
        Assert.Contains(decisions, d => d.Key == "plan.ladder_animated_thinned");
    }

    [Fact]
    public void Generate_grainy_keeps_all_rungs()
    {
        ScopedDecisionLog sink = Sink();

        IReadOnlyList<VideoOutput> liveAction = _gen.Generate(
            Reference(),
            Source(1920, 1080),
            null,
            Sink(),
            ComplexityHint.LiveAction
        );

        IReadOnlyList<VideoOutput> grainy = _gen.Generate(
            Reference(),
            Source(1920, 1080),
            null,
            sink,
            ComplexityHint.Grainy
        );

        // Grainy must retain at least as many rungs as live-action.
        Assert.True(
            grainy.Count >= liveAction.Count,
            $"Grainy ({grainy.Count}) should have >= rungs vs LiveAction ({liveAction.Count})"
        );

        IReadOnlyList<DecisionLog> decisions = sink.Snapshot();
        Assert.Contains(decisions, d => d.Key == "plan.ladder_grainy_full");
    }

    [Fact]
    public void Generate_h264_uses_h264_bitrate_column()
    {
        IReadOnlyList<VideoOutput> result = _gen.Generate(
            Reference(VideoCodecType.H264),
            Source(1920, 1080),
            null,
            Sink()
        );

        VideoOutput rung1080 = Assert.Single(result, v => v.Height == 1080);

        // Default table: H.264 1080p = 5000 kbps
        Assert.Equal(5000, rung1080.BitrateKbps);
    }

    [Fact]
    public void Generate_hevc_uses_60_percent_of_h264()
    {
        IReadOnlyList<VideoOutput> h264Result = _gen.Generate(
            Reference(VideoCodecType.H264),
            Source(1920, 1080),
            null,
            Sink()
        );

        IReadOnlyList<VideoOutput> hevcResult = _gen.Generate(
            Reference(VideoCodecType.H265),
            Source(1920, 1080),
            null,
            Sink()
        );

        VideoOutput h264Rung = Assert.Single(h264Result, v => v.Height == 1080);
        VideoOutput hevcRung = Assert.Single(hevcResult, v => v.Height == 1080);

        int expected = (int)Math.Round(h264Rung.BitrateKbps * 0.6);
        Assert.InRange(hevcRung.BitrateKbps, expected - 2, expected + 2);
    }

    [Fact]
    public void Generate_inherits_codec_preset_from_reference()
    {
        VideoOutput reference = Reference(VideoCodecType.H265, preset: "slow", crf: 22);

        IReadOnlyList<VideoOutput> result = _gen.Generate(
            reference,
            Source(1920, 1080),
            null,
            Sink()
        );

        Assert.NotEmpty(result);
        Assert.All(
            result,
            v =>
            {
                Assert.Equal(VideoCodecType.H265, v.Codec);
                Assert.Equal("slow", v.Preset);
                Assert.Equal(0, v.Crf); // CRF cleared for bitrate-ladder mode
            }
        );
    }

    [Fact]
    public void Generate_emits_complexity_unknown_decision_for_Auto()
    {
        ScopedDecisionLog sink = Sink();

        _gen.Generate(Reference(), Source(1920, 1080), null, sink, ComplexityHint.Auto);

        IReadOnlyList<DecisionLog> decisions = sink.Snapshot();
        Assert.Contains(decisions, d => d.Key == "plan.ladder_complexity_unknown");
    }

    [Fact]
    public void Generate_no_decisions_when_sink_is_NullDecisionLogSink()
    {
        // Smoke: should not throw even with the no-op sink.
        IReadOnlyList<VideoOutput> result = _gen.Generate(
            Reference(),
            Source(1920, 1080),
            null,
            NullDecisionLogSink.Instance
        );

        Assert.NotEmpty(result);
    }
}
