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
        new(EncoderHandle: handle, Codec: VideoCodecType.H264);

    private static CodecHint HevcHint(string handle = "libx265") =>
        new(EncoderHandle: handle, Codec: VideoCodecType.H265);

    private static CodecHint Av1Hint(string handle = "libsvtav1") =>
        new(EncoderHandle: handle, Codec: VideoCodecType.Av1);

    // ─── LinearQualityScaler ─────────────────────────────────────────────────

    [Fact]
    public void LinearQualityScaler_translates_proportionally()
    {
        LinearQualityScaler scaler = new();
        // 25/51 of 100 ≈ 49 (Math.Round(49.02) = 49)
        scaler.Translate(sourceCrf: 25, sourceMax: 51, targetMax: 100, hint: H264Hint()).Should().Be(expected: 49);
    }

    [Fact]
    public void LinearQualityScaler_boundary_zero()
    {
        LinearQualityScaler scaler = new();
        scaler.Translate(sourceCrf: 0, sourceMax: 51, targetMax: 100, hint: H264Hint()).Should().Be(expected: 0);
    }

    [Fact]
    public void LinearQualityScaler_boundary_max()
    {
        LinearQualityScaler scaler = new();
        scaler.Translate(sourceCrf: 51, sourceMax: 51, targetMax: 100, hint: H264Hint()).Should().Be(expected: 100);
    }

    [Fact]
    public void LinearQualityScaler_boundary_mid()
    {
        LinearQualityScaler scaler = new();
        // 26/51 * 100 = 50.98 → 51
        int result = scaler.Translate(sourceCrf: 26, sourceMax: 51, targetMax: 100, hint: H264Hint());
        result.Should().BeInRange(minimumValue: 50, maximumValue: 52);
    }

    [Fact]
    public void LinearQualityScaler_clamps_to_targetMax()
    {
        LinearQualityScaler scaler = new();
        // sourceCrf > sourceMax — clamped to targetMax
        scaler.Translate(sourceCrf: 100, sourceMax: 51, targetMax: 100, hint: H264Hint()).Should().Be(expected: 100);
    }

    [Fact]
    public void LinearQualityScaler_supports_any_handle()
    {
        LinearQualityScaler scaler = new();
        scaler.Supports(encoderHandle: "unknown_encoder").Should().BeTrue();
        scaler.Supports(encoderHandle: "h264_nvenc").Should().BeTrue();
        scaler.Supports(encoderHandle: "libx264").Should().BeTrue();
    }

    // ─── NvencQualityScaler ───────────────────────────────────────────────────

    [Fact]
    public void NvencQualityScaler_passes_through_h264_crf_unchanged()
    {
        NvencQualityScaler scaler = new();
        // H.264 source is 0–51, NVENC CQ is also 0–51 → 1:1 passthrough
        scaler.Translate(sourceCrf: 23, sourceMax: 51, targetMax: 51, hint: H264Hint(handle: "h264_nvenc")).Should().Be(expected: 23);
    }

    [Fact]
    public void NvencQualityScaler_passes_through_hevc_crf_unchanged()
    {
        NvencQualityScaler scaler = new();
        scaler.Translate(sourceCrf: 28, sourceMax: 51, targetMax: 51, hint: HevcHint(handle: "hevc_nvenc")).Should().Be(expected: 28);
    }

    [Fact]
    public void NvencQualityScaler_scales_av1_crf_to_51_range()
    {
        NvencQualityScaler scaler = new();
        // AV1 source: 0–63 → NVENC AV1 CQ: 0–51
        // 35/63 * 51 ≈ 28.3 → 28
        int result = scaler.Translate(sourceCrf: 35, sourceMax: 63, targetMax: 51, hint: Av1Hint(handle: "av1_nvenc"));
        result.Should().Be(expected: 28);
    }

    [Fact]
    public void NvencQualityScaler_boundary_zero_passthrough()
    {
        NvencQualityScaler scaler = new();
        scaler.Translate(sourceCrf: 0, sourceMax: 51, targetMax: 51, hint: H264Hint(handle: "h264_nvenc")).Should().Be(expected: 0);
    }

    [Fact]
    public void NvencQualityScaler_boundary_max_passthrough()
    {
        NvencQualityScaler scaler = new();
        scaler.Translate(sourceCrf: 51, sourceMax: 51, targetMax: 51, hint: H264Hint(handle: "h264_nvenc")).Should().Be(expected: 51);
    }

    [Theory]
    [InlineData(data: "h264_nvenc")]
    [InlineData(data: "hevc_nvenc")]
    [InlineData(data: "av1_nvenc")]
    public void NvencQualityScaler_supports_h264_hevc_av1_nvenc_handles(string handle)
    {
        NvencQualityScaler scaler = new();
        scaler.Supports(encoderHandle: handle).Should().BeTrue();
    }

    [Fact]
    public void NvencQualityScaler_does_not_support_libx264()
    {
        NvencQualityScaler scaler = new();
        scaler.Supports(encoderHandle: "libx264").Should().BeFalse();
    }

    // ─── QsvQualityScaler ─────────────────────────────────────────────────────

    [Fact]
    public void QsvQualityScaler_clamps_zero_to_one()
    {
        QsvQualityScaler scaler = new();
        // CRF 0 → would be 0/51*51 = 0 → off-by-one guard clamps to 1
        scaler.Translate(sourceCrf: 0, sourceMax: 51, targetMax: 51, hint: H264Hint(handle: "h264_qsv")).Should().Be(expected: 1);
    }

    [Fact]
    public void QsvQualityScaler_mid_value_stays_in_range()
    {
        QsvQualityScaler scaler = new();
        // 23/51 * 51 = 23 → within [1..51]
        scaler.Translate(sourceCrf: 23, sourceMax: 51, targetMax: 51, hint: H264Hint(handle: "h264_qsv")).Should().Be(expected: 23);
    }

    [Fact]
    public void QsvQualityScaler_boundary_max()
    {
        QsvQualityScaler scaler = new();
        scaler.Translate(sourceCrf: 51, sourceMax: 51, targetMax: 51, hint: H264Hint(handle: "h264_qsv")).Should().Be(expected: 51);
    }

    [Fact]
    public void QsvQualityScaler_boundary_one()
    {
        QsvQualityScaler scaler = new();
        // 1/51 * 51 = 1.0 → 1 (the QSV minimum, not clamped away)
        scaler.Translate(sourceCrf: 1, sourceMax: 51, targetMax: 51, hint: H264Hint(handle: "h264_qsv")).Should().Be(expected: 1);
    }

    [Theory]
    [InlineData(data: "h264_qsv")]
    [InlineData(data: "hevc_qsv")]
    [InlineData(data: "av1_qsv")]
    public void QsvQualityScaler_supports_qsv_handles(string handle)
    {
        QsvQualityScaler scaler = new();
        scaler.Supports(encoderHandle: handle).Should().BeTrue();
    }

    [Fact]
    public void QsvQualityScaler_does_not_support_nvenc()
    {
        QsvQualityScaler scaler = new();
        scaler.Supports(encoderHandle: "h264_nvenc").Should().BeFalse();
    }

    // ─── VideoToolboxQualityScaler ────────────────────────────────────────────

    [Fact]
    public void VideoToolboxQualityScaler_inverts_scale()
    {
        VideoToolboxQualityScaler scaler = new();
        // CRF 23/51 → lower CRF = higher quality → higher q:v
        // linear: 23/51 * 100 ≈ 45 → inverted: 100-45 = 55
        int result = scaler.Translate(sourceCrf: 23, sourceMax: 51, targetMax: 100, hint: H264Hint(handle: "h264_videotoolbox"));
        result.Should().BeInRange(minimumValue: 53, maximumValue: 57); // ≈55 ± rounding
    }

    [Fact]
    public void VideoToolboxQualityScaler_crf_zero_maps_to_100()
    {
        VideoToolboxQualityScaler scaler = new();
        // CRF 0 (lossless) → q:v 100 (best quality)
        scaler.Translate(sourceCrf: 0, sourceMax: 51, targetMax: 100, hint: H264Hint(handle: "h264_videotoolbox")).Should().Be(expected: 100);
    }

    [Fact]
    public void VideoToolboxQualityScaler_crf_max_maps_to_zero()
    {
        VideoToolboxQualityScaler scaler = new();
        // CRF 51 (worst quality) → q:v 0
        scaler.Translate(sourceCrf: 51, sourceMax: 51, targetMax: 100, hint: H264Hint(handle: "h264_videotoolbox")).Should().Be(expected: 0);
    }

    [Fact]
    public void VideoToolboxQualityScaler_boundary_mid()
    {
        VideoToolboxQualityScaler scaler = new();
        // CRF ~half → q:v ~half (inverted is symmetric around 50)
        int result = scaler.Translate(sourceCrf: 26, sourceMax: 51, targetMax: 100, hint: H264Hint(handle: "h264_videotoolbox"));
        result.Should().BeInRange(minimumValue: 47, maximumValue: 53);
    }

    [Theory]
    [InlineData(data: "h264_videotoolbox")]
    [InlineData(data: "hevc_videotoolbox")]
    public void VideoToolboxQualityScaler_supports_videotoolbox_handles(string handle)
    {
        VideoToolboxQualityScaler scaler = new();
        scaler.Supports(encoderHandle: handle).Should().BeTrue();
    }

    [Fact]
    public void VideoToolboxQualityScaler_does_not_support_nvenc()
    {
        VideoToolboxQualityScaler scaler = new();
        scaler.Supports(encoderHandle: "h264_nvenc").Should().BeFalse();
    }

    // ─── AmfAv1QualityScaler ─────────────────────────────────────────────────

    [Fact]
    public void AmfAv1QualityScaler_scales_to_255()
    {
        AmfAv1QualityScaler scaler = new();
        // 30/63 * 255 ≈ 121.4 → 121
        scaler.Translate(sourceCrf: 30, sourceMax: 63, targetMax: 255, hint: Av1Hint(handle: "av1_amf")).Should().Be(expected: 121);
    }

    [Fact]
    public void AmfAv1QualityScaler_boundary_zero()
    {
        AmfAv1QualityScaler scaler = new();
        scaler.Translate(sourceCrf: 0, sourceMax: 63, targetMax: 255, hint: Av1Hint(handle: "av1_amf")).Should().Be(expected: 0);
    }

    [Fact]
    public void AmfAv1QualityScaler_boundary_max()
    {
        AmfAv1QualityScaler scaler = new();
        scaler.Translate(sourceCrf: 63, sourceMax: 63, targetMax: 255, hint: Av1Hint(handle: "av1_amf")).Should().Be(expected: 255);
    }

    [Fact]
    public void AmfAv1QualityScaler_boundary_mid()
    {
        AmfAv1QualityScaler scaler = new();
        // 31/63 * 255 ≈ 125.5 → 126
        int result = scaler.Translate(sourceCrf: 31, sourceMax: 63, targetMax: 255, hint: Av1Hint(handle: "av1_amf"));
        result.Should().BeInRange(minimumValue: 124, maximumValue: 128);
    }

    [Fact]
    public void AmfAv1QualityScaler_supports_av1_amf()
    {
        AmfAv1QualityScaler scaler = new();
        scaler.Supports(encoderHandle: "av1_amf").Should().BeTrue();
    }

    [Fact]
    public void AmfAv1QualityScaler_does_not_support_h264_amf()
    {
        AmfAv1QualityScaler scaler = new();
        scaler.Supports(encoderHandle: "h264_amf").Should().BeFalse();
    }

    // ─── SvtAv1QualityScaler ─────────────────────────────────────────────────

    [Fact]
    public void SvtAv1QualityScaler_passes_through_av1_crf_unchanged()
    {
        SvtAv1QualityScaler scaler = new();
        scaler.Translate(sourceCrf: 30, sourceMax: 63, targetMax: 63, hint: Av1Hint()).Should().Be(expected: 30);
    }

    [Fact]
    public void SvtAv1QualityScaler_boundary_zero()
    {
        SvtAv1QualityScaler scaler = new();
        scaler.Translate(sourceCrf: 0, sourceMax: 63, targetMax: 63, hint: Av1Hint()).Should().Be(expected: 0);
    }

    [Fact]
    public void SvtAv1QualityScaler_boundary_max()
    {
        SvtAv1QualityScaler scaler = new();
        scaler.Translate(sourceCrf: 63, sourceMax: 63, targetMax: 63, hint: Av1Hint()).Should().Be(expected: 63);
    }

    [Fact]
    public void SvtAv1QualityScaler_boundary_mid()
    {
        SvtAv1QualityScaler scaler = new();
        scaler.Translate(sourceCrf: 35, sourceMax: 63, targetMax: 63, hint: Av1Hint()).Should().Be(expected: 35);
    }

    [Fact]
    public void SvtAv1QualityScaler_supports_libsvtav1()
    {
        SvtAv1QualityScaler scaler = new();
        scaler.Supports(encoderHandle: "libsvtav1").Should().BeTrue();
    }

    [Fact]
    public void SvtAv1QualityScaler_does_not_support_libaom()
    {
        SvtAv1QualityScaler scaler = new();
        scaler.Supports(encoderHandle: "libaom-av1").Should().BeFalse();
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
        QualityScalerResolver resolver = new(scalers: scalers);

        IQualityScaler result = resolver.For(encoderHandle: "h264_nvenc");
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
        QualityScalerResolver resolver = new(scalers: scalers);

        resolver.For(encoderHandle: "hevc_qsv").Should().BeOfType<QsvQualityScaler>();
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
        QualityScalerResolver resolver = new(scalers: scalers);

        IQualityScaler result = resolver.For(encoderHandle: "unknown_encoder");
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
        QualityScalerResolver resolver = new(scalers: scalers);

        // libx264 uses CRF natively — linear passthrough is correct
        resolver.For(encoderHandle: "libx264").Should().BeOfType<LinearQualityScaler>();
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
        QualityScalerResolver resolver = new(scalers: scalers);

        resolver.For(encoderHandle: "hevc_videotoolbox").Should().BeOfType<VideoToolboxQualityScaler>();
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
        QualityScalerResolver resolver = new(scalers: scalers);

        resolver.For(encoderHandle: "libsvtav1").Should().BeOfType<SvtAv1QualityScaler>();
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
        QualityScalerResolver resolver = new(scalers: scalers);

        resolver.For(encoderHandle: "av1_amf").Should().BeOfType<AmfAv1QualityScaler>();
    }
}
