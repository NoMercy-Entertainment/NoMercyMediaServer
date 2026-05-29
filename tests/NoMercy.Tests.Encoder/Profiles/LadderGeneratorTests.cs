using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// LadderGenerator builds the ABR variant set from a reference VideoOutput
/// and source. The product behaviour pinned here:
///   - User rungs (when non-empty) bypass the default table entirely.
///   - Rungs above source resolution are dropped (no upscaling, ever).
///   - HEVC bitrate is always 60% of H.264 at the same rung.
///   - VP9 bitrate is always 65% of H.264 at the same rung.
///   - AV1 bitrate is always 50% of H.264 at the same rung.
///   - Animated thinning drops every-other rung.
///   - Non-table source resolutions get a native rung at the top, with
///     bitrate interpolated between table neighbours.
/// </summary>
public class LadderGeneratorTests
{
    private readonly LadderGenerator _generator = new();

    // ── fixtures ────────────────────────────────────────────────────────────

    private static VideoOutput Reference(VideoCodecType codec = VideoCodecType.H264) =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: codec,
            Width: 1920,
            Height: 1080,
            RateControl: NoMercy.Encoder.Profiles.RateControlMode.Cbr,
            Crf: 0,
            BitrateKbps: 0,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: "medium",
            CodecProfile: CodecProfile.Main,
            Level: "4.0",
            Tune: null,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            KeyframeIntervalSeconds: 2,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: "v/{label}",
            PlaylistNameTemplate: "v/{label}/p"
        );

    private static VideoStreamInfo Source(int width, int height) =>
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
            BitRateKbps: 8000
        );

    // ── user-supplied rungs ────────────────────────────────────────────────

    [Fact]
    public void Generate_UserSuppliedRungs_BypassDefaultTable()
    {
        // Caller passing rungs takes total control; the default table is skipped.
        ScopedDecisionLog log = new();
        LadderRung[] userRungs =
        [
            new(800, 450, VideoCodecType.H264, 1000, 1100, 1500, 24),
            new(640, 360, VideoCodecType.H264, 600, 660, 900, 24),
        ];

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            Reference(),
            Source(3840, 2160),
            userRungs,
            log
        );

        outputs.Should().HaveCount(2);
        outputs[0].Width.Should().Be(800);
        outputs[0].BitrateKbps.Should().Be(1000);
        outputs[1].Width.Should().Be(640);
        outputs[1].BitrateKbps.Should().Be(600);
        // User-rungs path zeroes the CRF.
        outputs.Should().AllSatisfy(o => o.Crf.Should().Be(0));
    }

    [Fact]
    public void Generate_UserSuppliedRungs_EmitsSuppliedDecision()
    {
        ScopedDecisionLog log = new();
        LadderRung[] userRungs = [new(640, 360, VideoCodecType.H264, 800, 880, 1200, 24)];

        _ = _generator.Generate(Reference(), Source(1920, 1080), userRungs, log);

        log.Snapshot().Should().Contain(d => d.Key == "plan.ladder_user_supplied");
    }

    [Fact]
    public void Generate_EmptyUserRungs_FallsBackToDefaultTable()
    {
        // Zero-length user list is treated as "not supplied" — fall back to table.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            Reference(),
            Source(1920, 1080),
            userRungs: [],
            log
        );

        outputs.Should().NotBeEmpty();
        // 1080p hits the table exactly; rungs are 1080p, 720p, 480p, 360p.
        outputs.Should().HaveCount(4);
    }

    // ── source upscaling guard ──────────────────────────────────────────────

    [Fact]
    public void Generate_RungsAboveSource_AreSkipped()
    {
        // 720p source — must NOT produce 1080p, 1440p, 2160p rungs.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            Reference(),
            Source(1280, 720),
            userRungs: null,
            log
        );

        outputs.Should().NotContain(o => o.Width > 1280 || (o.Height ?? 0) > 720);
        log.Snapshot().Should().Contain(d => d.Key == "plan.ladder_rung_skipped_above_source");
    }

    [Fact]
    public void Generate_SourceEqualsTopRung_IncludesIt()
    {
        // 4K source — top table rung (3840×2160) should be present, native not duplicated.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            Reference(),
            Source(3840, 2160),
            userRungs: null,
            log
        );

        outputs.Should().Contain(o => o.Width == 3840 && o.Height == 2160);
        outputs
            .Count(o => o.Width == 3840 && o.Height == 2160)
            .Should()
            .Be(1, "native rung must not be duplicated when source matches the table exactly");
    }

    // ── codec scaling ───────────────────────────────────────────────────────

    [Fact]
    public void Generate_HevcReference_Bitrates60PercentOfH264()
    {
        // 1920×1080 H.264 entry = 5000 kbps; HEVC = round(5000 * 0.6) = 3000.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> hevcOutputs = _generator.Generate(
            Reference(VideoCodecType.H265),
            Source(1920, 1080),
            userRungs: null,
            log
        );

        VideoOutput? rung1080 = hevcOutputs.FirstOrDefault(o => o.Width == 1920);
        rung1080.Should().NotBeNull();
        rung1080!.BitrateKbps.Should().Be(3000);
    }

    [Fact]
    public void Generate_Av1Reference_1080p_Is50PercentOfH264()
    {
        // H.264 table entry at 1080p = 5000 kbps; AV1 = round(5000 * 0.50) = 2500.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> av1Outputs = _generator.Generate(
            Reference(VideoCodecType.Av1),
            Source(1920, 1080),
            userRungs: null,
            log
        );

        VideoOutput? rung1080 = av1Outputs.FirstOrDefault(o => o.Width == 1920);
        rung1080.Should().NotBeNull();
        rung1080!.BitrateKbps.Should().Be(2500);
    }

    [Fact]
    public void Generate_Vp9Reference_1080p_Is65PercentOfH264()
    {
        // H.264 table entry at 1080p = 5000 kbps; VP9 = round(5000 * 0.65) = 3250.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> vp9Outputs = _generator.Generate(
            Reference(VideoCodecType.Vp9),
            Source(1920, 1080),
            userRungs: null,
            log
        );

        VideoOutput? rung1080 = vp9Outputs.FirstOrDefault(o => o.Width == 1920);
        rung1080.Should().NotBeNull();
        rung1080!.BitrateKbps.Should().Be(3250);
    }

    [Fact]
    public void Generate_Av1Reference_NoCodecDefaultDecisionEmitted()
    {
        // AV1 now has real bitrate columns — the "ladder_codec_default" fallback
        // notice must not appear.
        ScopedDecisionLog log = new();

        _ = _generator.Generate(
            Reference(VideoCodecType.Av1),
            Source(1920, 1080),
            userRungs: null,
            log
        );

        log.Snapshot().Should().NotContain(d => d.Key == "analyze.ladder_codec_default");
    }

    [Fact]
    public void Generate_Vp9Reference_NoCodecDefaultDecisionEmitted()
    {
        ScopedDecisionLog log = new();

        _ = _generator.Generate(
            Reference(VideoCodecType.Vp9),
            Source(1920, 1080),
            userRungs: null,
            log
        );

        log.Snapshot().Should().NotContain(d => d.Key == "analyze.ladder_codec_default");
    }

    [Fact]
    public void Generate_Av1Reference_AllRungs_SaneBitrates()
    {
        // Every AV1 rung must be positive and less than the H.264 rung.
        ScopedDecisionLog h264Log = new();
        ScopedDecisionLog av1Log = new();

        IReadOnlyList<VideoOutput> h264Outputs = _generator.Generate(
            Reference(VideoCodecType.H264),
            Source(3840, 2160),
            userRungs: null,
            h264Log
        );

        IReadOnlyList<VideoOutput> av1Outputs = _generator.Generate(
            Reference(VideoCodecType.Av1),
            Source(3840, 2160),
            userRungs: null,
            av1Log
        );

        av1Outputs.Should().HaveCount(h264Outputs.Count);
        av1Outputs.Should().AllSatisfy(rung => rung.BitrateKbps.Should().BePositive());

        for (int i = 0; i < av1Outputs.Count; i++)
        {
            av1Outputs[i].BitrateKbps.Should().BeLessThan(h264Outputs[i].BitrateKbps);
        }
    }

    [Fact]
    public void Generate_Vp9Reference_AllRungs_SaneBitrates()
    {
        // Every VP9 rung must be positive and less than the H.264 rung.
        ScopedDecisionLog h264Log = new();
        ScopedDecisionLog vp9Log = new();

        IReadOnlyList<VideoOutput> h264Outputs = _generator.Generate(
            Reference(VideoCodecType.H264),
            Source(3840, 2160),
            userRungs: null,
            h264Log
        );

        IReadOnlyList<VideoOutput> vp9Outputs = _generator.Generate(
            Reference(VideoCodecType.Vp9),
            Source(3840, 2160),
            userRungs: null,
            vp9Log
        );

        vp9Outputs.Should().HaveCount(h264Outputs.Count);
        vp9Outputs.Should().AllSatisfy(rung => rung.BitrateKbps.Should().BePositive());

        for (int i = 0; i < vp9Outputs.Count; i++)
        {
            vp9Outputs[i].BitrateKbps.Should().BeLessThan(h264Outputs[i].BitrateKbps);
        }
    }

    [Fact]
    public void Generate_Av1LessThanVp9_Vp9LessThanH264_AllRungs()
    {
        // Efficiency ordering must hold at every rung: AV1 < VP9 < H.264.
        ScopedDecisionLog h264Log = new();
        ScopedDecisionLog vp9Log = new();
        ScopedDecisionLog av1Log = new();

        IReadOnlyList<VideoOutput> h264Outputs = _generator.Generate(
            Reference(VideoCodecType.H264),
            Source(3840, 2160),
            userRungs: null,
            h264Log
        );

        IReadOnlyList<VideoOutput> vp9Outputs = _generator.Generate(
            Reference(VideoCodecType.Vp9),
            Source(3840, 2160),
            userRungs: null,
            vp9Log
        );

        IReadOnlyList<VideoOutput> av1Outputs = _generator.Generate(
            Reference(VideoCodecType.Av1),
            Source(3840, 2160),
            userRungs: null,
            av1Log
        );

        for (int i = 0; i < h264Outputs.Count; i++)
        {
            av1Outputs[i].BitrateKbps.Should().BeLessThan(vp9Outputs[i].BitrateKbps);
            vp9Outputs[i].BitrateKbps.Should().BeLessThan(h264Outputs[i].BitrateKbps);
        }
    }

    [Fact]
    public void Generate_H264Ladder_UnchangedAfterCodecColumnAddition()
    {
        // Regression: adding AV1/VP9 columns must not alter H.264 output.
        // Snapshot the expected bitrates from the DefaultTable.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            Reference(VideoCodecType.H264),
            Source(3840, 2160),
            userRungs: null,
            log
        );

        // All six rungs fit a 4K source; bitrates come directly from the H264 table.
        outputs.Should().HaveCount(6);
        outputs.Single(o => o.Width == 3840).BitrateKbps.Should().Be(15000);
        outputs.Single(o => o.Width == 2560).BitrateKbps.Should().Be(8000);
        outputs.Single(o => o.Width == 1920).BitrateKbps.Should().Be(5000);
        outputs.Single(o => o.Width == 1280).BitrateKbps.Should().Be(3000);
        outputs.Single(o => o.Width == 854).BitrateKbps.Should().Be(1500);
        outputs.Single(o => o.Width == 640).BitrateKbps.Should().Be(800);
    }

    [Fact]
    public void Generate_AllCodecs_LadderIsMonotonic()
    {
        // Bitrates must be monotonically descending as resolution decreases.
        VideoCodecType[] codecs =
        [
            VideoCodecType.H264,
            VideoCodecType.H265,
            VideoCodecType.Vp9,
            VideoCodecType.Av1,
        ];

        foreach (VideoCodecType codec in codecs)
        {
            ScopedDecisionLog log = new();
            IReadOnlyList<VideoOutput> outputs = _generator.Generate(
                Reference(codec),
                Source(3840, 2160),
                userRungs: null,
                log
            );

            // Outputs are ordered descending by resolution already; bitrates should follow.
            int[] bitrates = outputs.Select(o => o.BitrateKbps).ToArray();
            for (int i = 1; i < bitrates.Length; i++)
            {
                bitrates[i]
                    .Should()
                    .BeLessThanOrEqualTo(
                        bitrates[i - 1],
                        $"codec {codec}: rung {i} bitrate {bitrates[i]} must not exceed rung {i - 1} bitrate {bitrates[i - 1]}"
                    );
            }
        }
    }

    // ── complexity-aware thinning ───────────────────────────────────────────

    [Fact]
    public void Generate_AutoComplexity_EmitsUnknownDecision()
    {
        // Auto defaults to live-action but emits a decision so the user
        // knows the complexity probe didn't run.
        ScopedDecisionLog log = new();

        _ = _generator.Generate(
            Reference(),
            Source(1920, 1080),
            userRungs: null,
            log,
            complexity: ComplexityHint.Auto
        );

        log.Snapshot().Should().Contain(d => d.Key == "plan.ladder_complexity_unknown");
    }

    [Fact]
    public void Generate_AnimatedComplexity_DropsEveryOtherRung()
    {
        // 4K source → table has 4 fitting rungs (2160, 1440, 1080, 720, 480, 360).
        // Animated keeps even indices: 2160, 1080, 480.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            Reference(),
            Source(3840, 2160),
            userRungs: null,
            log,
            complexity: ComplexityHint.Animated
        );

        outputs.Select(o => o.Height).Should().BeEquivalentTo(new int?[] { 2160, 1080, 480 });
        log.Snapshot().Should().Contain(d => d.Key == "plan.ladder_animated_thinned");
    }

    [Fact]
    public void Generate_GrainyComplexity_KeepsAllRungs()
    {
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            Reference(),
            Source(3840, 2160),
            userRungs: null,
            log,
            complexity: ComplexityHint.Grainy
        );

        // All six default rungs fit a 4K source.
        outputs.Should().HaveCount(6);
        log.Snapshot().Should().Contain(d => d.Key == "plan.ladder_grainy_full");
    }

    [Fact]
    public void Generate_LiveActionComplexity_KeepsAllRungs()
    {
        // LiveAction → no thinning, no extra decision specific to live-action.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            Reference(),
            Source(3840, 2160),
            userRungs: null,
            log,
            complexity: ComplexityHint.LiveAction
        );

        outputs.Should().HaveCount(6);
    }

    // ── native (non-table) source resolution ────────────────────────────────

    [Fact]
    public void Generate_NonTableSource_PrependsNativeRung()
    {
        // 1440p is not in the default table → native rung must be added at top.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            Reference(),
            Source(2560, 1440),
            userRungs: null,
            log,
            complexity: ComplexityHint.LiveAction
        );

        outputs[0].Width.Should().Be(2560);
        outputs[0].Height.Should().Be(1440);
        // Below the table 1080 rung (5000 kbps) and above the next 720 rung —
        // interpolated value lands between.
        outputs[0].BitrateKbps.Should().BeGreaterThan(5000);
    }

    [Fact]
    public void Generate_SourceAboveLargestTableEntry_UsesTopBitrate()
    {
        // 8K source exceeds the 3840x2160 cap → native rung uses 3840x2160 bitrate.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            Reference(),
            Source(7680, 4320),
            userRungs: null,
            log,
            complexity: ComplexityHint.LiveAction
        );

        outputs[0].Width.Should().Be(7680);
        outputs[0].BitrateKbps.Should().Be(15000); // top table entry bitrate
    }

    [Fact]
    public void Generate_OutputsAreClonesOfReference_OnlyResolutionAndBitrateDiffer()
    {
        // Reference codec / profile / level / preset must be preserved across
        // every generated rung — only width/height/bitrate change.
        ScopedDecisionLog log = new();
        VideoOutput reference = Reference() with { Preset = "slow", Level = "4.1" };

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            reference,
            Source(1920, 1080),
            userRungs: null,
            log
        );

        outputs
            .Should()
            .AllSatisfy(o =>
            {
                o.Preset.Should().Be("slow");
                o.Level.Should().Be("4.1");
                o.Codec.Should().Be(VideoCodecType.H264);
                o.Crf.Should().Be(0);
            });
    }
}
