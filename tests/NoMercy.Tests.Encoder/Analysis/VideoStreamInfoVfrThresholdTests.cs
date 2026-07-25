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

namespace NoMercy.Tests.Encoder.Analysis;

/// <summary>
/// Pins the unified 1% VFR threshold on
/// <see cref="VideoStreamInfo.IsVariableFrameRate"/>.
///
/// Before unification AnalyzeStage carried a private duplicate of the
/// detection logic that used a 1% relative threshold while
/// <see cref="VideoStreamInfo"/> used a 1.0-fps absolute threshold. A 23.976
/// fps source whose real frame rate read 24.5 fps would:
///   • be silently ignored by ProfileRuleValidator (absolute diff 0.524 &lt; 1.0)
///   • but emit a VFR decision-log entry from AnalyzeStage (relative 2.2% &gt; 1%).
///
/// The two surfaces now share one threshold. These tests pin that contract.
/// </summary>
public class VideoStreamInfoVfrThresholdTests
{
    // ── Above the 1% threshold → VFR ────────────────────────────────────────

    [Theory]
    [InlineData([24.0, 24.5, true])] // ~2% spread, classic VFR
    [InlineData([30.0, 24.5, true])] // 18% spread
    [InlineData([60.0, 59.0, true])] // 1.67% spread (just over threshold)
    [InlineData([23.976, 25.0, true])] // 4.3% spread, common pulldown artefact
    public void Crosses_one_percent_threshold_flags_as_VFR(
        double realFps,
        double avgFps,
        bool expected
    )
    {
        VideoStreamInfo stream = MakeStream(realFps, avgFps);
        stream.IsVariableFrameRate.Should().Be(expected);
    }

    // ── At or below the 1% threshold → CFR ─────────────────────────────────

    [Theory]
    [InlineData([24.0, 24.0])] // identical
    [InlineData([60.0, 60.0])] // identical 60fps
    [InlineData([23.976, 23.97])] // sub-percent jitter
    [InlineData([60.0, 59.94])] // 0.1% spread — broadcast NTSC jitter, NOT VFR
    public void At_or_below_one_percent_threshold_treated_as_CFR(double realFps, double avgFps)
    {
        VideoStreamInfo stream = MakeStream(realFps, avgFps);
        stream.IsVariableFrameRate.Should().BeFalse();
    }

    // ── Null guard ──────────────────────────────────────────────────────────

    [Fact]
    public void Null_real_frame_rate_returns_false()
    {
        VideoStreamInfo stream = MakeStream(null, 24.0);
        stream.IsVariableFrameRate.Should().BeFalse();
    }

    [Fact]
    public void Null_average_frame_rate_returns_false()
    {
        VideoStreamInfo stream = MakeStream(24.0, null);
        stream.IsVariableFrameRate.Should().BeFalse();
    }

    [Fact]
    public void Both_null_returns_false()
    {
        VideoStreamInfo stream = MakeStream(null, null);
        stream.IsVariableFrameRate.Should().BeFalse();
    }

    // ── Zero / degenerate frame rate ────────────────────────────────────────

    [Fact]
    public void Zero_real_frame_rate_with_max_floor_avoids_divide_by_zero()
    {
        // The implementation divides by Math.Max(real, 1.0) — never zero.
        VideoStreamInfo stream = MakeStream(0, 0.005);
        // diff = 0.005, max(0, 1.0) = 1.0 → 0.5% → below threshold → CFR.
        stream.IsVariableFrameRate.Should().BeFalse();
    }

    [Fact]
    public void Symmetry_real_and_avg_swap_gives_same_result()
    {
        // Math.Abs makes it symmetric in the diff, but the denominator is
        // RealFrameRate (with floor). Swapping flips which is denominator.
        // Pin the current asymmetry so refactors are intentional.
        VideoStreamInfo highReal = MakeStream(30.0, 24.5);
        VideoStreamInfo highAvg = MakeStream(24.5, 30.0);

        highReal.IsVariableFrameRate.Should().BeTrue();
        highAvg.IsVariableFrameRate.Should().BeTrue();
    }

    // ── Aggregation via MediaInfo ───────────────────────────────────────────

    [Fact]
    public void MediaInfo_aggregates_any_VFR_stream_as_variable()
    {
        MediaInfo info = new(
            "/test.mkv",
            "matroska",
            TimeSpan.FromHours(1),
            8000,
            0,
            [
                MakeStream(24.0, 24.0), // CFR
                MakeStream(30.0, 24.5), // VFR
            ],
            [],
            [],
            []
        );

        info.IsVariableFrameRate.Should().BeTrue();
    }

    [Fact]
    public void MediaInfo_all_CFR_streams_yields_false()
    {
        MediaInfo info = new(
            "/test.mkv",
            "matroska",
            TimeSpan.FromHours(1),
            8000,
            0,
            [MakeStream(24.0, 24.0), MakeStream(60.0, 60.0)],
            [],
            [],
            []
        );

        info.IsVariableFrameRate.Should().BeFalse();
    }

    [Fact]
    public void MediaInfo_with_no_video_streams_yields_false()
    {
        MediaInfo info = new(
            "/audio.mp3",
            "mp3",
            TimeSpan.FromMinutes(5),
            192,
            0,
            [],
            [],
            [],
            []
        );

        info.IsVariableFrameRate.Should().BeFalse();
    }

    // ── Helper ──────────────────────────────────────────────────────────────

    private static VideoStreamInfo MakeStream(double? real, double? avg) =>
        new(
            0,
            "h264",
            1920,
            1080,
            avg ?? real ?? 24.0,
            8,
            "yuv420p",
            "bt709",
            "bt709",
            "bt709",
            true,
            5000,
            avg,
            real
        );
}
