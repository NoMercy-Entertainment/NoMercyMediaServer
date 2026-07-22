// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

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
            new(Width: 800, Height: 450, Codec: VideoCodecType.H264, BitrateKbps: 1000, MaxBitrateKbps: 1100, BufferSizeKbps: 1500, Framerate: 24),
            new(Width: 640, Height: 360, Codec: VideoCodecType.H264, BitrateKbps: 600, MaxBitrateKbps: 660, BufferSizeKbps: 900, Framerate: 24),
        ];

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            reference: Reference(),
            source: Source(width: 3840, height: 2160),
            userRungs: userRungs,
            decisions: log
        );

        outputs.Should().HaveCount(expected: 2);
        outputs[index: 0].Width.Should().Be(expected: 800);
        outputs[index: 0].BitrateKbps.Should().Be(expected: 1000);
        outputs[index: 1].Width.Should().Be(expected: 640);
        outputs[index: 1].BitrateKbps.Should().Be(expected: 600);
        // User-rungs path zeroes the CRF.
        outputs.Should().AllSatisfy(expected: o => o.Crf.Should().Be(expected: 0));
    }

    [Fact]
    public void Generate_UserSuppliedRungs_EmitsSuppliedDecision()
    {
        ScopedDecisionLog log = new();
        LadderRung[] userRungs = [new(Width: 640, Height: 360, Codec: VideoCodecType.H264, BitrateKbps: 800, MaxBitrateKbps: 880, BufferSizeKbps: 1200, Framerate: 24)];

        _ = _generator.Generate(reference: Reference(), source: Source(width: 1920, height: 1080), userRungs: userRungs, decisions: log);

        log.Snapshot().Should().Contain(predicate: d => d.Key == "plan.ladder_user_supplied");
    }

    [Fact]
    public void Generate_EmptyUserRungs_FallsBackToDefaultTable()
    {
        // Zero-length user list is treated as "not supplied" — fall back to table.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            reference: Reference(),
            source: Source(width: 1920, height: 1080),
            userRungs: [],
            decisions: log
        );

        outputs.Should().NotBeEmpty();
        // 1080p hits the table exactly; rungs are 1080p, 720p, 480p, 360p.
        outputs.Should().HaveCount(expected: 4);
    }

    // ── source upscaling guard ──────────────────────────────────────────────

    [Fact]
    public void Generate_RungsAboveSource_AreSkipped()
    {
        // 720p source — must NOT produce 1080p, 1440p, 2160p rungs.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            reference: Reference(),
            source: Source(width: 1280, height: 720),
            userRungs: null,
            decisions: log
        );

        outputs.Should().NotContain(predicate: o => o.Width > 1280 || (o.Height ?? 0) > 720);
        log.Snapshot().Should().Contain(predicate: d => d.Key == "plan.ladder_rung_skipped_above_source");
    }

    [Fact]
    public void Generate_SourceEqualsTopRung_IncludesIt()
    {
        // 4K source — top table rung (3840×2160) should be present, native not duplicated.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            reference: Reference(),
            source: Source(width: 3840, height: 2160),
            userRungs: null,
            decisions: log
        );

        outputs.Should().Contain(predicate: o => o.Width == 3840 && o.Height == 2160);
        outputs
            .Count(predicate: o => o is { Width: 3840, Height: 2160 })
            .Should()
            .Be(expected: 1, because: "native rung must not be duplicated when source matches the table exactly");
    }

    // ── codec scaling ───────────────────────────────────────────────────────

    [Fact]
    public void Generate_HevcReference_Bitrates60PercentOfH264()
    {
        // 1920×1080 H.264 entry = 5000 kbps; HEVC = round(5000 * 0.6) = 3000.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> hevcOutputs = _generator.Generate(
            reference: Reference(codec: VideoCodecType.H265),
            source: Source(width: 1920, height: 1080),
            userRungs: null,
            decisions: log
        );

        VideoOutput? rung1080 = hevcOutputs.FirstOrDefault(predicate: o => o.Width == 1920);
        rung1080.Should().NotBeNull();
        rung1080!.BitrateKbps.Should().Be(expected: 3000);
    }

    [Fact]
    public void Generate_Av1Reference_1080p_Is50PercentOfH264()
    {
        // H.264 table entry at 1080p = 5000 kbps; AV1 = round(5000 * 0.50) = 2500.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> av1Outputs = _generator.Generate(
            reference: Reference(codec: VideoCodecType.Av1),
            source: Source(width: 1920, height: 1080),
            userRungs: null,
            decisions: log
        );

        VideoOutput? rung1080 = av1Outputs.FirstOrDefault(predicate: o => o.Width == 1920);
        rung1080.Should().NotBeNull();
        rung1080!.BitrateKbps.Should().Be(expected: 2500);
    }

    [Fact]
    public void Generate_Vp9Reference_1080p_Is65PercentOfH264()
    {
        // H.264 table entry at 1080p = 5000 kbps; VP9 = round(5000 * 0.65) = 3250.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> vp9Outputs = _generator.Generate(
            reference: Reference(codec: VideoCodecType.Vp9),
            source: Source(width: 1920, height: 1080),
            userRungs: null,
            decisions: log
        );

        VideoOutput? rung1080 = vp9Outputs.FirstOrDefault(predicate: o => o.Width == 1920);
        rung1080.Should().NotBeNull();
        rung1080!.BitrateKbps.Should().Be(expected: 3250);
    }

    [Fact]
    public void Generate_Av1Reference_NoCodecDefaultDecisionEmitted()
    {
        // AV1 now has real bitrate columns — the "ladder_codec_default" fallback
        // notice must not appear.
        ScopedDecisionLog log = new();

        _ = _generator.Generate(
            reference: Reference(codec: VideoCodecType.Av1),
            source: Source(width: 1920, height: 1080),
            userRungs: null,
            decisions: log
        );

        log.Snapshot().Should().NotContain(predicate: d => d.Key == "analyze.ladder_codec_default");
    }

    [Fact]
    public void Generate_Vp9Reference_NoCodecDefaultDecisionEmitted()
    {
        ScopedDecisionLog log = new();

        _ = _generator.Generate(
            reference: Reference(codec: VideoCodecType.Vp9),
            source: Source(width: 1920, height: 1080),
            userRungs: null,
            decisions: log
        );

        log.Snapshot().Should().NotContain(predicate: d => d.Key == "analyze.ladder_codec_default");
    }

    [Fact]
    public void Generate_Av1Reference_AllRungs_SaneBitrates()
    {
        // Every AV1 rung must be positive and less than the H.264 rung.
        ScopedDecisionLog h264Log = new();
        ScopedDecisionLog av1Log = new();

        IReadOnlyList<VideoOutput> h264Outputs = _generator.Generate(
            reference: Reference(codec: VideoCodecType.H264),
            source: Source(width: 3840, height: 2160),
            userRungs: null,
            decisions: h264Log
        );

        IReadOnlyList<VideoOutput> av1Outputs = _generator.Generate(
            reference: Reference(codec: VideoCodecType.Av1),
            source: Source(width: 3840, height: 2160),
            userRungs: null,
            decisions: av1Log
        );

        av1Outputs.Should().HaveCount(expected: h264Outputs.Count);
        av1Outputs.Should().AllSatisfy(expected: rung => rung.BitrateKbps.Should().BePositive());

        for (int i = 0; i < av1Outputs.Count; i++)
        {
            av1Outputs[index: i].BitrateKbps.Should().BeLessThan(expected: h264Outputs[index: i].BitrateKbps);
        }
    }

    [Fact]
    public void Generate_Vp9Reference_AllRungs_SaneBitrates()
    {
        // Every VP9 rung must be positive and less than the H.264 rung.
        ScopedDecisionLog h264Log = new();
        ScopedDecisionLog vp9Log = new();

        IReadOnlyList<VideoOutput> h264Outputs = _generator.Generate(
            reference: Reference(codec: VideoCodecType.H264),
            source: Source(width: 3840, height: 2160),
            userRungs: null,
            decisions: h264Log
        );

        IReadOnlyList<VideoOutput> vp9Outputs = _generator.Generate(
            reference: Reference(codec: VideoCodecType.Vp9),
            source: Source(width: 3840, height: 2160),
            userRungs: null,
            decisions: vp9Log
        );

        vp9Outputs.Should().HaveCount(expected: h264Outputs.Count);
        vp9Outputs.Should().AllSatisfy(expected: rung => rung.BitrateKbps.Should().BePositive());

        for (int i = 0; i < vp9Outputs.Count; i++)
        {
            vp9Outputs[index: i].BitrateKbps.Should().BeLessThan(expected: h264Outputs[index: i].BitrateKbps);
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
            reference: Reference(codec: VideoCodecType.H264),
            source: Source(width: 3840, height: 2160),
            userRungs: null,
            decisions: h264Log
        );

        IReadOnlyList<VideoOutput> vp9Outputs = _generator.Generate(
            reference: Reference(codec: VideoCodecType.Vp9),
            source: Source(width: 3840, height: 2160),
            userRungs: null,
            decisions: vp9Log
        );

        IReadOnlyList<VideoOutput> av1Outputs = _generator.Generate(
            reference: Reference(codec: VideoCodecType.Av1),
            source: Source(width: 3840, height: 2160),
            userRungs: null,
            decisions: av1Log
        );

        for (int i = 0; i < h264Outputs.Count; i++)
        {
            av1Outputs[index: i].BitrateKbps.Should().BeLessThan(expected: vp9Outputs[index: i].BitrateKbps);
            vp9Outputs[index: i].BitrateKbps.Should().BeLessThan(expected: h264Outputs[index: i].BitrateKbps);
        }
    }

    [Fact]
    public void Generate_H264Ladder_UnchangedAfterCodecColumnAddition()
    {
        // Regression: adding AV1/VP9 columns must not alter H.264 output.
        // Snapshot the expected bitrates from the DefaultTable.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            reference: Reference(codec: VideoCodecType.H264),
            source: Source(width: 3840, height: 2160),
            userRungs: null,
            decisions: log
        );

        // All six rungs fit a 4K source; bitrates come directly from the H264 table.
        outputs.Should().HaveCount(expected: 6);
        outputs.Single(predicate: o => o.Width == 3840).BitrateKbps.Should().Be(expected: 15000);
        outputs.Single(predicate: o => o.Width == 2560).BitrateKbps.Should().Be(expected: 8000);
        outputs.Single(predicate: o => o.Width == 1920).BitrateKbps.Should().Be(expected: 5000);
        outputs.Single(predicate: o => o.Width == 1280).BitrateKbps.Should().Be(expected: 3000);
        outputs.Single(predicate: o => o.Width == 854).BitrateKbps.Should().Be(expected: 1500);
        outputs.Single(predicate: o => o.Width == 640).BitrateKbps.Should().Be(expected: 800);
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
                reference: Reference(codec: codec),
                source: Source(width: 3840, height: 2160),
                userRungs: null,
                decisions: log
            );

            // Outputs are ordered descending by resolution already; bitrates should follow.
            int[] bitrates = outputs.Select(selector: o => o.BitrateKbps).ToArray();
            for (int i = 1; i < bitrates.Length; i++)
            {
                bitrates[i]
                    .Should()
                    .BeLessThanOrEqualTo(
                        expected: bitrates[i - 1],
                        because: $"codec {codec}: rung {i} bitrate {bitrates[i]} must not exceed rung {i - 1} bitrate {bitrates[i - 1]}"
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
            reference: Reference(),
            source: Source(width: 1920, height: 1080),
            userRungs: null,
            decisions: log,
            complexity: ComplexityHint.Auto
        );

        log.Snapshot().Should().Contain(predicate: d => d.Key == "plan.ladder_complexity_unknown");
    }

    [Fact]
    public void Generate_AnimatedComplexity_DropsEveryOtherRung()
    {
        // 4K source → table has 4 fitting rungs (2160, 1440, 1080, 720, 480, 360).
        // Animated keeps even indices: 2160, 1080, 480.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            reference: Reference(),
            source: Source(width: 3840, height: 2160),
            userRungs: null,
            decisions: log,
            complexity: ComplexityHint.Animated
        );

        outputs.Select(selector: o => o.Height).Should().BeEquivalentTo(expectation: new int?[] { 2160, 1080, 480 });
        log.Snapshot().Should().Contain(predicate: d => d.Key == "plan.ladder_animated_thinned");
    }

    [Fact]
    public void Generate_GrainyComplexity_KeepsAllRungs()
    {
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            reference: Reference(),
            source: Source(width: 3840, height: 2160),
            userRungs: null,
            decisions: log,
            complexity: ComplexityHint.Grainy
        );

        // All six default rungs fit a 4K source.
        outputs.Should().HaveCount(expected: 6);
        log.Snapshot().Should().Contain(predicate: d => d.Key == "plan.ladder_grainy_full");
    }

    [Fact]
    public void Generate_LiveActionComplexity_KeepsAllRungs()
    {
        // LiveAction → no thinning, no extra decision specific to live-action.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            reference: Reference(),
            source: Source(width: 3840, height: 2160),
            userRungs: null,
            decisions: log,
            complexity: ComplexityHint.LiveAction
        );

        outputs.Should().HaveCount(expected: 6);
    }

    // ── native (non-table) source resolution ────────────────────────────────

    [Fact]
    public void Generate_NonTableSource_PrependsNativeRung()
    {
        // 1440p is not in the default table → native rung must be added at top.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            reference: Reference(),
            source: Source(width: 2560, height: 1440),
            userRungs: null,
            decisions: log,
            complexity: ComplexityHint.LiveAction
        );

        outputs[index: 0].Width.Should().Be(expected: 2560);
        outputs[index: 0].Height.Should().Be(expected: 1440);
        // Below the table 1080 rung (5000 kbps) and above the next 720 rung —
        // interpolated value lands between.
        outputs[index: 0].BitrateKbps.Should().BeGreaterThan(expected: 5000);
    }

    [Fact]
    public void Generate_SourceAboveLargestTableEntry_UsesTopBitrate()
    {
        // 8K source exceeds the 3840x2160 cap → native rung uses 3840x2160 bitrate.
        ScopedDecisionLog log = new();

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            reference: Reference(),
            source: Source(width: 7680, height: 4320),
            userRungs: null,
            decisions: log,
            complexity: ComplexityHint.LiveAction
        );

        outputs[index: 0].Width.Should().Be(expected: 7680);
        outputs[index: 0].BitrateKbps.Should().Be(expected: 15000); // top table entry bitrate
    }

    [Fact]
    public void Generate_OutputsAreClonesOfReference_OnlyResolutionAndBitrateDiffer()
    {
        // Reference codec / profile / level / preset must be preserved across
        // every generated rung — only width/height/bitrate change.
        ScopedDecisionLog log = new();
        VideoOutput reference = Reference() with { Preset = "slow", Level = "4.1" };

        IReadOnlyList<VideoOutput> outputs = _generator.Generate(
            reference: reference,
            source: Source(width: 1920, height: 1080),
            userRungs: null,
            decisions: log
        );

        outputs
            .Should()
            .AllSatisfy(expected: o =>
            {
                o.Preset.Should().Be(expected: "slow");
                o.Level.Should().Be(expected: "4.1");
                o.Codec.Should().Be(expected: VideoCodecType.H264);
                o.Crf.Should().Be(expected: 0);
            });
    }
}
