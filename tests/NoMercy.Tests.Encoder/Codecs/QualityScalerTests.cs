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

using NoMercy.Encoder.Codecs;

namespace NoMercy.Tests.Encoder.Codecs;

public class QualityScalerTests
{
    // ─── helpers ────────────────────────────────────────────────────────────

    private static CodecHint H264Hint(string handle = "libx264") =>
        new(handle, VideoCodecType.H264);

    private static CodecHint HevcHint(string handle = "libx265") =>
        new(handle, VideoCodecType.H265);

    private static CodecHint Av1Hint(string handle = "libsvtav1") =>
        new(handle, VideoCodecType.Av1);

    // ─── LinearQualityScaler ─────────────────────────────────────────────────

    [Fact]
    public void LinearQualityScaler_translates_proportionally()
    {
        LinearQualityScaler scaler = new();
        // 25/51 of 100 ≈ 49 (Math.Round(49.02) = 49)
        scaler.Translate(25, 51, 100, H264Hint()).Should().Be(49);
    }

    [Fact]
    public void LinearQualityScaler_boundary_zero()
    {
        LinearQualityScaler scaler = new();
        scaler.Translate(0, 51, 100, H264Hint()).Should().Be(0);
    }

    [Fact]
    public void LinearQualityScaler_boundary_max()
    {
        LinearQualityScaler scaler = new();
        scaler.Translate(51, 51, 100, H264Hint()).Should().Be(100);
    }

    [Fact]
    public void LinearQualityScaler_boundary_mid()
    {
        LinearQualityScaler scaler = new();
        // 26/51 * 100 = 50.98 → 51
        int result = scaler.Translate(26, 51, 100, H264Hint());
        result.Should().BeInRange(50, 52);
    }

    [Fact]
    public void LinearQualityScaler_clamps_to_targetMax()
    {
        LinearQualityScaler scaler = new();
        // sourceCrf > sourceMax — clamped to targetMax
        scaler.Translate(100, 51, 100, H264Hint()).Should().Be(100);
    }

    [Fact]
    public void LinearQualityScaler_supports_any_handle()
    {
        LinearQualityScaler scaler = new();
        scaler.Supports("unknown_encoder").Should().BeTrue();
        scaler.Supports("h264_nvenc").Should().BeTrue();
        scaler.Supports("libx264").Should().BeTrue();
    }

    // ─── NvencQualityScaler ───────────────────────────────────────────────────

    [Fact]
    public void NvencQualityScaler_passes_through_h264_crf_unchanged()
    {
        NvencQualityScaler scaler = new();
        // H.264 source is 0–51, NVENC CQ is also 0–51 → 1:1 passthrough
        scaler.Translate(23, 51, 51, H264Hint("h264_nvenc")).Should().Be(23);
    }

    [Fact]
    public void NvencQualityScaler_passes_through_hevc_crf_unchanged()
    {
        NvencQualityScaler scaler = new();
        scaler.Translate(28, 51, 51, HevcHint("hevc_nvenc")).Should().Be(28);
    }

    [Fact]
    public void NvencQualityScaler_scales_av1_crf_to_51_range()
    {
        NvencQualityScaler scaler = new();
        // AV1 source: 0–63 → NVENC AV1 CQ: 0–51
        // 35/63 * 51 ≈ 28.3 → 28
        int result = scaler.Translate(35, 63, 51, Av1Hint("av1_nvenc"));
        result.Should().Be(28);
    }

    [Fact]
    public void NvencQualityScaler_boundary_zero_passthrough()
    {
        NvencQualityScaler scaler = new();
        scaler.Translate(0, 51, 51, H264Hint("h264_nvenc")).Should().Be(0);
    }

    [Fact]
    public void NvencQualityScaler_boundary_max_passthrough()
    {
        NvencQualityScaler scaler = new();
        scaler.Translate(51, 51, 51, H264Hint("h264_nvenc")).Should().Be(51);
    }

    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    public void NvencQualityScaler_supports_h264_hevc_av1_nvenc_handles(string handle)
    {
        NvencQualityScaler scaler = new();
        scaler.Supports(handle).Should().BeTrue();
    }

    [Fact]
    public void NvencQualityScaler_does_not_support_libx264()
    {
        NvencQualityScaler scaler = new();
        scaler.Supports("libx264").Should().BeFalse();
    }

    // ─── QsvQualityScaler ─────────────────────────────────────────────────────

    [Fact]
    public void QsvQualityScaler_clamps_zero_to_one()
    {
        QsvQualityScaler scaler = new();
        // CRF 0 → would be 0/51*51 = 0 → off-by-one guard clamps to 1
        scaler.Translate(0, 51, 51, H264Hint("h264_qsv")).Should().Be(1);
    }

    [Fact]
    public void QsvQualityScaler_mid_value_stays_in_range()
    {
        QsvQualityScaler scaler = new();
        // 23/51 * 51 = 23 → within [1..51]
        scaler.Translate(23, 51, 51, H264Hint("h264_qsv")).Should().Be(23);
    }

    [Fact]
    public void QsvQualityScaler_boundary_max()
    {
        QsvQualityScaler scaler = new();
        scaler.Translate(51, 51, 51, H264Hint("h264_qsv")).Should().Be(51);
    }

    [Fact]
    public void QsvQualityScaler_boundary_one()
    {
        QsvQualityScaler scaler = new();
        // 1/51 * 51 = 1.0 → 1 (the QSV minimum, not clamped away)
        scaler.Translate(1, 51, 51, H264Hint("h264_qsv")).Should().Be(1);
    }

    [Theory]
    [InlineData("h264_qsv")]
    [InlineData("hevc_qsv")]
    [InlineData("av1_qsv")]
    public void QsvQualityScaler_supports_qsv_handles(string handle)
    {
        QsvQualityScaler scaler = new();
        scaler.Supports(handle).Should().BeTrue();
    }

    [Fact]
    public void QsvQualityScaler_does_not_support_nvenc()
    {
        QsvQualityScaler scaler = new();
        scaler.Supports("h264_nvenc").Should().BeFalse();
    }

    // ─── VideoToolboxQualityScaler ────────────────────────────────────────────

    [Fact]
    public void VideoToolboxQualityScaler_inverts_scale()
    {
        VideoToolboxQualityScaler scaler = new();
        // CRF 23/51 → lower CRF = higher quality → higher q:v
        // linear: 23/51 * 100 ≈ 45 → inverted: 100-45 = 55
        int result = scaler.Translate(23, 51, 100, H264Hint("h264_videotoolbox"));
        result.Should().BeInRange(53, 57); // ≈55 ± rounding
    }

    [Fact]
    public void VideoToolboxQualityScaler_crf_zero_maps_to_100()
    {
        VideoToolboxQualityScaler scaler = new();
        // CRF 0 (lossless) → q:v 100 (best quality)
        scaler.Translate(0, 51, 100, H264Hint("h264_videotoolbox")).Should().Be(100);
    }

    [Fact]
    public void VideoToolboxQualityScaler_crf_max_maps_to_zero()
    {
        VideoToolboxQualityScaler scaler = new();
        // CRF 51 (worst quality) → q:v 0
        scaler.Translate(51, 51, 100, H264Hint("h264_videotoolbox")).Should().Be(0);
    }

    [Fact]
    public void VideoToolboxQualityScaler_boundary_mid()
    {
        VideoToolboxQualityScaler scaler = new();
        // CRF ~half → q:v ~half (inverted is symmetric around 50)
        int result = scaler.Translate(26, 51, 100, H264Hint("h264_videotoolbox"));
        result.Should().BeInRange(47, 53);
    }

    [Theory]
    [InlineData("h264_videotoolbox")]
    [InlineData("hevc_videotoolbox")]
    public void VideoToolboxQualityScaler_supports_videotoolbox_handles(string handle)
    {
        VideoToolboxQualityScaler scaler = new();
        scaler.Supports(handle).Should().BeTrue();
    }

    [Fact]
    public void VideoToolboxQualityScaler_does_not_support_nvenc()
    {
        VideoToolboxQualityScaler scaler = new();
        scaler.Supports("h264_nvenc").Should().BeFalse();
    }

    // ─── AmfAv1QualityScaler ─────────────────────────────────────────────────

    [Fact]
    public void AmfAv1QualityScaler_scales_to_255()
    {
        AmfAv1QualityScaler scaler = new();
        // 30/63 * 255 ≈ 121.4 → 121
        scaler.Translate(30, 63, 255, Av1Hint("av1_amf")).Should().Be(121);
    }

    [Fact]
    public void AmfAv1QualityScaler_boundary_zero()
    {
        AmfAv1QualityScaler scaler = new();
        scaler.Translate(0, 63, 255, Av1Hint("av1_amf")).Should().Be(0);
    }

    [Fact]
    public void AmfAv1QualityScaler_boundary_max()
    {
        AmfAv1QualityScaler scaler = new();
        scaler.Translate(63, 63, 255, Av1Hint("av1_amf")).Should().Be(255);
    }

    [Fact]
    public void AmfAv1QualityScaler_boundary_mid()
    {
        AmfAv1QualityScaler scaler = new();
        // 31/63 * 255 ≈ 125.5 → 126
        int result = scaler.Translate(31, 63, 255, Av1Hint("av1_amf"));
        result.Should().BeInRange(124, 128);
    }

    [Fact]
    public void AmfAv1QualityScaler_supports_av1_amf()
    {
        AmfAv1QualityScaler scaler = new();
        scaler.Supports("av1_amf").Should().BeTrue();
    }

    [Fact]
    public void AmfAv1QualityScaler_does_not_support_h264_amf()
    {
        AmfAv1QualityScaler scaler = new();
        scaler.Supports("h264_amf").Should().BeFalse();
    }

    // ─── SvtAv1QualityScaler ─────────────────────────────────────────────────

    [Fact]
    public void SvtAv1QualityScaler_passes_through_av1_crf_unchanged()
    {
        SvtAv1QualityScaler scaler = new();
        scaler.Translate(30, 63, 63, Av1Hint()).Should().Be(30);
    }

    [Fact]
    public void SvtAv1QualityScaler_boundary_zero()
    {
        SvtAv1QualityScaler scaler = new();
        scaler.Translate(0, 63, 63, Av1Hint()).Should().Be(0);
    }

    [Fact]
    public void SvtAv1QualityScaler_boundary_max()
    {
        SvtAv1QualityScaler scaler = new();
        scaler.Translate(63, 63, 63, Av1Hint()).Should().Be(63);
    }

    [Fact]
    public void SvtAv1QualityScaler_boundary_mid()
    {
        SvtAv1QualityScaler scaler = new();
        scaler.Translate(35, 63, 63, Av1Hint()).Should().Be(35);
    }

    [Fact]
    public void SvtAv1QualityScaler_supports_libsvtav1()
    {
        SvtAv1QualityScaler scaler = new();
        scaler.Supports("libsvtav1").Should().BeTrue();
    }

    [Fact]
    public void SvtAv1QualityScaler_does_not_support_libaom()
    {
        SvtAv1QualityScaler scaler = new();
        scaler.Supports("libaom-av1").Should().BeFalse();
    }

    // ─── QualityScalerResolver ────────────────────────────────────────────────

    [Fact]
    public void Resolver_returns_specific_scaler_when_handle_matches()
    {
        IQualityScaler[] scalers =
        [
            new NvencQualityScaler(),
            new QsvQualityScaler(),
            new VideoToolboxQualityScaler(),
            new AmfAv1QualityScaler(),
            new SvtAv1QualityScaler(),
            new LinearQualityScaler(),
        ];
        QualityScalerResolver resolver = new(scalers);

        IQualityScaler result = resolver.For("h264_nvenc");
        result.Should().BeOfType<NvencQualityScaler>();
    }

    [Fact]
    public void Resolver_returns_qsv_scaler_for_hevc_qsv()
    {
        IQualityScaler[] scalers =
        [
            new NvencQualityScaler(),
            new QsvQualityScaler(),
            new VideoToolboxQualityScaler(),
            new AmfAv1QualityScaler(),
            new SvtAv1QualityScaler(),
            new LinearQualityScaler(),
        ];
        QualityScalerResolver resolver = new(scalers);

        resolver.For("hevc_qsv").Should().BeOfType<QsvQualityScaler>();
    }

    [Fact]
    public void Resolver_falls_back_to_LinearQualityScaler_when_no_match()
    {
        IQualityScaler[] scalers =
        [
            new NvencQualityScaler(),
            new QsvQualityScaler(),
            new VideoToolboxQualityScaler(),
            new AmfAv1QualityScaler(),
            new SvtAv1QualityScaler(),
            new LinearQualityScaler(),
        ];
        QualityScalerResolver resolver = new(scalers);

        IQualityScaler result = resolver.For("unknown_encoder");
        result.Should().BeOfType<LinearQualityScaler>();
    }

    [Fact]
    public void Resolver_falls_back_to_LinearQualityScaler_for_libx264()
    {
        IQualityScaler[] scalers =
        [
            new NvencQualityScaler(),
            new QsvQualityScaler(),
            new VideoToolboxQualityScaler(),
            new AmfAv1QualityScaler(),
            new SvtAv1QualityScaler(),
            new LinearQualityScaler(),
        ];
        QualityScalerResolver resolver = new(scalers);

        // libx264 uses CRF natively — linear passthrough is correct
        resolver.For("libx264").Should().BeOfType<LinearQualityScaler>();
    }

    [Fact]
    public void Resolver_returns_videotoolbox_scaler_for_hevc_videotoolbox()
    {
        IQualityScaler[] scalers =
        [
            new NvencQualityScaler(),
            new QsvQualityScaler(),
            new VideoToolboxQualityScaler(),
            new AmfAv1QualityScaler(),
            new SvtAv1QualityScaler(),
            new LinearQualityScaler(),
        ];
        QualityScalerResolver resolver = new(scalers);

        resolver.For("hevc_videotoolbox").Should().BeOfType<VideoToolboxQualityScaler>();
    }

    [Fact]
    public void Resolver_returns_svt_scaler_for_libsvtav1()
    {
        IQualityScaler[] scalers =
        [
            new NvencQualityScaler(),
            new QsvQualityScaler(),
            new VideoToolboxQualityScaler(),
            new AmfAv1QualityScaler(),
            new SvtAv1QualityScaler(),
            new LinearQualityScaler(),
        ];
        QualityScalerResolver resolver = new(scalers);

        resolver.For("libsvtav1").Should().BeOfType<SvtAv1QualityScaler>();
    }

    [Fact]
    public void Resolver_returns_amf_av1_scaler_for_av1_amf()
    {
        IQualityScaler[] scalers =
        [
            new NvencQualityScaler(),
            new QsvQualityScaler(),
            new VideoToolboxQualityScaler(),
            new AmfAv1QualityScaler(),
            new SvtAv1QualityScaler(),
            new LinearQualityScaler(),
        ];
        QualityScalerResolver resolver = new(scalers);

        resolver.For("av1_amf").Should().BeOfType<AmfAv1QualityScaler>();
    }
}
