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
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Hdr;

/// <summary>
/// Unit tests for <see cref="DolbyVisionGate.Resolve"/>.
///
/// The gate is pure static logic — every test exercises a distinct branch
/// in the four-condition evaluation without any I/O or DI wiring.
/// </summary>
public class DolbyVisionGateTests
{
    // ---------------------------------------------------------------------------
    // Fixture helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Minimal DV source fixture — profile 8, level 6, RPU present, no EL,
    /// HDR10 base-layer compatible (the most common streaming DV variant).
    /// </summary>
    private static DolbyVisionInfo DvSource(int profile = 8, int level = 6) =>
        new(
            profile,
            level,
            true,
            false,
            DvBlCompatibility.Hdr10
        );

    private static ScopedDecisionLog NewLog() => new();

    // ---------------------------------------------------------------------------
    // No DV in source
    // ---------------------------------------------------------------------------

    [Fact]
    public void Resolve_no_DV_in_source_returns_not_preserved_silently()
    {
        ScopedDecisionLog log = NewLog();

        DolbyVisionDecision result = DolbyVisionGate.Resolve(
            null,
            VideoCodecType.H265,
            10,
            OutputFormat.Mp4,
            HdrPolicies.PassthroughWhenPossible,
            true,
            log
        );

        result.Preserved.Should().BeFalse();
        result.ExtraFlags.Should().BeEmpty();
        // No decision emitted — nothing to log when DV simply isn't present.
        log.Snapshot().Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // HdrPolicy.AlwaysTonemap forces strip
    // ---------------------------------------------------------------------------

    [Fact]
    public void Resolve_strips_when_HdrPolicy_is_AlwaysTonemap()
    {
        ScopedDecisionLog log = NewLog();

        DolbyVisionDecision result = DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.H265,
            10,
            OutputFormat.Mp4,
            HdrPolicies.AlwaysTonemap,
            true,
            log
        );

        result.Preserved.Should().BeFalse();
        result.Reason.Should().Contain("AlwaysTonemap");
        log.Snapshot().Should().ContainSingle(d => d.Key == "plan.dv_stripped");
    }

    // ---------------------------------------------------------------------------
    // Codec gate
    // ---------------------------------------------------------------------------

    [Fact]
    public void Resolve_strips_when_codec_is_H264()
    {
        ScopedDecisionLog log = NewLog();

        DolbyVisionDecision result = DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.H264,
            10,
            OutputFormat.Mp4,
            HdrPolicies.PassthroughWhenPossible,
            true,
            log
        );

        result.Preserved.Should().BeFalse();
        result.Reason.Should().Contain("codec").And.Contain("H264");
        log.Snapshot().Should().ContainSingle(d => d.Key == "plan.dv_stripped");
    }

    [Fact]
    public void Resolve_strips_when_codec_is_VP9()
    {
        ScopedDecisionLog log = NewLog();

        DolbyVisionDecision result = DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.Vp9,
            10,
            OutputFormat.Mp4,
            HdrPolicies.PassthroughWhenPossible,
            true,
            log
        );

        result.Preserved.Should().BeFalse();
        result.Reason.Should().Contain("Vp9");
        log.Snapshot().Should().ContainSingle(d => d.Key == "plan.dv_stripped");
    }

    // ---------------------------------------------------------------------------
    // Bit-depth gate
    // ---------------------------------------------------------------------------

    [Fact]
    public void Resolve_strips_when_bit_depth_below_10()
    {
        ScopedDecisionLog log = NewLog();

        DolbyVisionDecision result = DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.H265,
            8,
            OutputFormat.Mp4,
            HdrPolicies.PassthroughWhenPossible,
            true,
            log
        );

        result.Preserved.Should().BeFalse();
        result.Reason.Should().Contain("bit_depth").And.Contain("8");
        log.Snapshot().Should().ContainSingle(d => d.Key == "plan.dv_stripped");
    }

    // ---------------------------------------------------------------------------
    // Container gate
    // ---------------------------------------------------------------------------

    [Fact]
    public void Resolve_strips_when_container_is_DASH()
    {
        ScopedDecisionLog log = NewLog();

        DolbyVisionDecision result = DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.H265,
            10,
            OutputFormat.Dash,
            HdrPolicies.PassthroughWhenPossible,
            true,
            log
        );

        result.Preserved.Should().BeFalse();
        result.Reason.Should().Contain("Dash");
        log.Snapshot().Should().ContainSingle(d => d.Key == "plan.dv_stripped");
    }

    // ---------------------------------------------------------------------------
    // Preservation — MP4
    // ---------------------------------------------------------------------------

    [Fact]
    public void Resolve_preserves_when_HEVC_10bit_MP4_passthrough()
    {
        ScopedDecisionLog log = NewLog();

        DolbyVisionDecision result = DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.H265,
            10,
            OutputFormat.Mp4,
            HdrPolicies.PassthroughWhenPossible,
            true,
            log
        );

        result.Preserved.Should().BeTrue();
        result.ExtraFlags.Should().ContainKey("-tag:v").WhoseValue.Should().Be("dvh1");
        result.ExtraFlags.Should().ContainKey("-brand").WhoseValue.Should().Be("iso6");
        log.Snapshot().Should().ContainSingle(d => d.Key == "plan.dv_preserved");
    }

    [Fact]
    public void Resolve_extra_flags_for_MP4_includes_dvh1_and_iso6_brand()
    {
        DolbyVisionDecision result = DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.H265,
            10,
            OutputFormat.Mp4,
            HdrPolicies.PassthroughWhenPossible,
            true,
            NewLog()
        );

        result.ExtraFlags["-tag:v"].Should().Be("dvh1");
        result.ExtraFlags["-brand"].Should().Be("iso6");
    }

    // ---------------------------------------------------------------------------
    // Preservation — MKV
    // ---------------------------------------------------------------------------

    [Fact]
    public void Resolve_preserves_when_AV1_10bit_MKV_passthrough()
    {
        ScopedDecisionLog log = NewLog();

        DolbyVisionDecision result = DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.Av1,
            10,
            OutputFormat.Mkv,
            HdrPolicies.PassthroughWhenPossible,
            true,
            log
        );

        result.Preserved.Should().BeTrue();
        // MKV carries DV natively — only disposition flag, no codec tag.
        result.ExtraFlags.Should().ContainKey("-disposition:v");
        result.ExtraFlags.Should().NotContainKey("-tag:v");
        log.Snapshot().Should().ContainSingle(d => d.Key == "plan.dv_preserved");
    }

    // ---------------------------------------------------------------------------
    // Preservation — HLS fmp4
    // ---------------------------------------------------------------------------

    [Fact]
    public void Resolve_preserves_with_HLS_fmp4_when_HEVC_10bit_HLS_passthrough()
    {
        ScopedDecisionLog log = NewLog();

        DolbyVisionDecision result = DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.H265,
            10,
            OutputFormat.Hls,
            HdrPolicies.PassthroughWhenPossible,
            true,
            log,
            true
        );

        result.Preserved.Should().BeTrue();
        result.ExtraFlags.Should().ContainKey("-hls_segment_type").WhoseValue.Should().Be("fmp4");
        log.Snapshot().Should().ContainSingle(d => d.Key == "plan.dv_preserved");
    }

    [Fact]
    public void Resolve_strips_when_HLS_segment_type_is_mpegts()
    {
        ScopedDecisionLog log = NewLog();

        DolbyVisionDecision result = DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.H265,
            10,
            OutputFormat.Hls,
            HdrPolicies.PassthroughWhenPossible,
            true,
            log,
            false
        );

        result.Preserved.Should().BeFalse();
        result.Reason.Should().Contain("mpegts");
        log.Snapshot().Should().ContainSingle(d => d.Key == "plan.dv_stripped");
    }

    // ---------------------------------------------------------------------------
    // Re-encode strips the RPU — the reported corrupt-playback regression
    // ---------------------------------------------------------------------------

    [Fact]
    public void Resolve_strips_when_video_is_re_encoded_even_if_all_else_passes()
    {
        // The exact shape of the Punisher bug: a DV profile-8.6 source encoded
        // to HEVC 10-bit HLS fmp4 — every codec/depth/container condition passes
        // — but the rung crops + re-encodes with nvenc, which drops the RPU. The
        // gate must NOT tag it dvh1; doing so produced a file claiming Dolby
        // Vision with no RPU / DV config box, which players decode as corrupt.
        ScopedDecisionLog log = NewLog();

        DolbyVisionDecision result = DolbyVisionGate.Resolve(
            DvSource(8, 6),
            VideoCodecType.H265,
            10,
            OutputFormat.Hls,
            HdrPolicies.PassthroughWhenPossible,
            false,
            log,
            true
        );

        result.Preserved.Should().BeFalse();
        result.Reason.Should().Contain("re-encoded");
        result.ExtraFlags.Should().NotContainKey("-tag:v");
        log.Snapshot().Should().ContainSingle(d => d.Key == "plan.dv_stripped");
    }

    // ---------------------------------------------------------------------------
    // AlwaysPreserve still goes through gate conditions
    // ---------------------------------------------------------------------------

    [Fact]
    public void Resolve_preserves_when_HdrPolicy_is_AlwaysPreserve()
    {
        ScopedDecisionLog log = NewLog();

        DolbyVisionDecision result = DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.H265,
            10,
            OutputFormat.Mp4,
            HdrPolicies.AlwaysPreserve,
            true,
            log
        );

        result.Preserved.Should().BeTrue();
        log.Snapshot().Should().ContainSingle(d => d.Key == "plan.dv_preserved");
    }

    // ---------------------------------------------------------------------------
    // Decision log reason string coverage
    // ---------------------------------------------------------------------------

    [Fact]
    public void Resolve_decision_log_includes_reason_string_for_every_strip_branch()
    {
        // AlwaysTonemap branch
        ScopedDecisionLog log1 = NewLog();
        DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.H265,
            10,
            OutputFormat.Mp4,
            HdrPolicies.AlwaysTonemap,
            true,
            log1
        );
        log1.Snapshot()[0].Message.Should().NotBeNullOrWhiteSpace();

        // Codec branch
        ScopedDecisionLog log2 = NewLog();
        DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.H264,
            10,
            OutputFormat.Mp4,
            HdrPolicies.PassthroughWhenPossible,
            true,
            log2
        );
        log2.Snapshot()[0].Message.Should().Contain("H264");

        // Bit-depth branch
        ScopedDecisionLog log3 = NewLog();
        DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.H265,
            8,
            OutputFormat.Mp4,
            HdrPolicies.PassthroughWhenPossible,
            true,
            log3
        );
        log3.Snapshot()[0].Message.Should().Contain("8");

        // Container branch
        ScopedDecisionLog log4 = NewLog();
        DolbyVisionGate.Resolve(
            DvSource(),
            VideoCodecType.H265,
            10,
            OutputFormat.Dash,
            HdrPolicies.PassthroughWhenPossible,
            true,
            log4
        );
        log4.Snapshot()[0].Message.Should().Contain("Dash");
    }
}
