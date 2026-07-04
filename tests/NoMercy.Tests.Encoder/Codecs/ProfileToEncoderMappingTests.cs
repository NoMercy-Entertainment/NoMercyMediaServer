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
using NoMercy.Encoder.Codecs.Definitions;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Codecs;

/// <summary>
/// Tests that codec definitions map to the correct encoder capabilities per codec type.
/// Asserts preset/profile/level availability, rate control modes, and quality ranges.
/// Each codec definition must supply the correct sets of presets and profiles for
/// each supported encoder (software and hardware variants).
/// </summary>
public class ProfileToEncoderMappingTests
{
    private readonly CodecRegistry _registry = new();

    // ── H.264 codec definition ──────────────────────────────────────────────────

    [Fact]
    public void H264Definition_Contains_Six_Encoders()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        def.Encoders.Should().HaveCount(6);
    }

    [Fact]
    public void H264_Software_Libx264_Is_First_Entry()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo sw = def.Encoders[0];

        sw.FfmpegName.Should().Be("libx264");
        sw.RequiredVendor.Should().BeNull();
    }

    [Fact]
    public void H264_Libx264_Has_10_Presets()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo sw = def.Encoders[0];

        sw.Presets.Should().HaveCount(10);
        sw.Presets.Should()
            .Equal(
                "ultrafast",
                "superfast",
                "veryfast",
                "faster",
                "fast",
                "medium",
                "slow",
                "slower",
                "veryslow",
                "placebo"
            );
    }

    [Fact]
    public void H264_Libx264_Has_Six_Profiles()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo sw = def.Encoders[0];

        sw.Profiles.Should().Contain("baseline");
        sw.Profiles.Should().Contain("main");
        sw.Profiles.Should().Contain("high");
        sw.Profiles.Should().Contain("high10");
        sw.Profiles.Should().Contain("high422");
        sw.Profiles.Should().Contain("high444p");
    }

    [Fact]
    public void H264_Libx264_Quality_Range_Is_0_51()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo sw = def.Encoders[0];

        sw.QualityRange.Min.Should().Be(0);
        sw.QualityRange.Max.Should().Be(51);
        sw.QualityRange.Default.Should().Be(23);
    }

    [Fact]
    public void H264_Nvenc_Has_Seven_Presets()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo nvenc = def.Encoders.FirstOrDefault(e => e.FfmpegName == "h264_nvenc");

        nvenc.Should().NotBeNull();
        nvenc!.Presets.Should().HaveCount(7);
        nvenc.Presets.Should().Equal("p1", "p2", "p3", "p4", "p5", "p6", "p7");
    }

    [Fact]
    public void H264_Nvenc_Does_Not_Have_High10_Profile()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo nvenc = def.Encoders.FirstOrDefault(e => e.FfmpegName == "h264_nvenc");

        nvenc!.Profiles.Should().NotContain("high10");
        nvenc.Profiles.Should().Equal("baseline", "main", "high");
    }

    [Fact]
    public void H264_Nvenc_Supports_Cq_Cqp_Cbr_Vbr()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo nvenc = def.Encoders.FirstOrDefault(e => e.FfmpegName == "h264_nvenc");

        nvenc!.SupportedRateControl.Should().Contain(RateControlMode.Cq);
        nvenc.SupportedRateControl.Should().Contain(RateControlMode.Cqp);
        nvenc.SupportedRateControl.Should().Contain(RateControlMode.Cbr);
        nvenc.SupportedRateControl.Should().Contain(RateControlMode.Vbr);
        nvenc.SupportedRateControl.Should().NotContain(RateControlMode.Crf);
    }

    [Fact]
    public void H264_Amf_Has_Three_Presets()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo amf = def.Encoders.FirstOrDefault(e => e.FfmpegName == "h264_amf");

        amf!.Presets.Should().HaveCount(3);
        amf.Presets.Should().Equal("speed", "balanced", "quality");
    }

    [Fact]
    public void H264_Amf_Supports_Qvbr_And_Hqvbr()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo amf = def.Encoders.FirstOrDefault(e => e.FfmpegName == "h264_amf");

        amf!.SupportedRateControl.Should().Contain(RateControlMode.Qvbr);
        amf.SupportedRateControl.Should().Contain(RateControlMode.Hqvbr);
        amf.SupportedRateControl.Should().Contain(RateControlMode.Hqcbr);
    }

    [Fact]
    public void H264_Amf_Has_VendorSpecificFlag_Usage()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo amf = def.Encoders.FirstOrDefault(e => e.FfmpegName == "h264_amf");

        amf!.VendorSpecificFlags.Should().ContainKey("-usage");
        amf.VendorSpecificFlags["-usage"].Should().Be("transcoding");
    }

    [Fact]
    public void H264_Qsv_Has_Seven_Presets_No_Ultrafast()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo qsv = def.Encoders.FirstOrDefault(e => e.FfmpegName == "h264_qsv");

        qsv!.Presets.Should().HaveCount(7);
        qsv.Presets.Should().NotContain("ultrafast");
        qsv.Presets.Should().NotContain("placebo");
    }

    [Fact]
    public void H264_Qsv_Quality_Range_Min_Is_One()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo qsv = def.Encoders.FirstOrDefault(e => e.FfmpegName == "h264_qsv");

        qsv!.QualityRange.Min.Should().Be(1);
        qsv.QualityRange.Max.Should().Be(51);
    }

    [Fact]
    public void H264_Qsv_Supports_Icq()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo qsv = def.Encoders.FirstOrDefault(e => e.FfmpegName == "h264_qsv");

        qsv!.SupportedRateControl.Should().Contain(RateControlMode.Icq);
    }

    [Fact]
    public void H264_Vaapi_Has_No_Presets()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo vaapi = def.Encoders.FirstOrDefault(e => e.FfmpegName == "h264_vaapi");

        vaapi!.Presets.Should().BeEmpty();
    }

    [Fact]
    public void H264_VideoToolbox_Quality_Range_Is_0_100()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo vt = def.Encoders.FirstOrDefault(e => e.FfmpegName == "h264_videotoolbox");

        vt!.QualityRange.Min.Should().Be(0);
        vt.QualityRange.Max.Should().Be(100);
        vt.QualityRange.Default.Should().Be(50);
    }

    [Fact]
    public void H264_VideoToolbox_Has_Numeric_Profiles()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo vt = def.Encoders.FirstOrDefault(e => e.FfmpegName == "h264_videotoolbox");

        vt!.Profiles.Should().Equal("66", "77", "100");
    }

    [Fact]
    public void H264_VideoToolbox_Supports_QualityLevel_And_Cbr()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H264);
        EncoderInfo vt = def.Encoders.FirstOrDefault(e => e.FfmpegName == "h264_videotoolbox");

        vt!.SupportedRateControl.Should().Contain(RateControlMode.QualityLevel);
        vt.SupportedRateControl.Should().Contain(RateControlMode.Cbr);
        vt.SupportedRateControl.Should().HaveCount(2);
    }

    // ── H.265 / HEVC codec definition ────────────────────────────────────────────

    [Fact]
    public void H265Definition_Contains_Six_Encoders()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H265);
        def.Encoders.Should().HaveCount(6);
    }

    [Fact]
    public void H265_Libx265_Supports10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H265);
        EncoderInfo libx265 = def.Encoders[0];

        libx265.FfmpegName.Should().Be("libx265");
        libx265.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void H265_Libx265_Supports_Hdr()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H265);
        EncoderInfo libx265 = def.Encoders[0];

        libx265.SupportsHdr.Should().BeTrue();
    }

    [Fact]
    public void H265_HevcNvenc_Supports10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H265);
        EncoderInfo nvenc = def.Encoders.FirstOrDefault(e => e.FfmpegName == "hevc_nvenc");

        nvenc!.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void H265_HevcAmf_Supports10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H265);
        EncoderInfo amf = def.Encoders.FirstOrDefault(e => e.FfmpegName == "hevc_amf");

        amf!.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void H265_HevcQsv_Supports10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H265);
        EncoderInfo qsv = def.Encoders.FirstOrDefault(e => e.FfmpegName == "hevc_qsv");

        qsv!.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void H265_HevcVideoToolbox_Has_Numeric_Profiles()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.H265);
        EncoderInfo vt = def.Encoders.FirstOrDefault(e => e.FfmpegName == "hevc_videotoolbox");

        vt!.Profiles.Should().HaveCount(2);
        vt.Profiles.Should().Contain("1");
        vt.Profiles.Should().Contain("2");
    }

    // ── VP9 codec definition ────────────────────────────────────────────────────

    [Fact]
    public void Vp9Definition_Contains_Multiple_Encoders()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Vp9);
        def.Encoders.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Vp9_Libvpx_Is_Software_Fallback()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Vp9);
        EncoderInfo sw = def.Encoders[0];

        sw.FfmpegName.Should().Be("libvpx-vp9");
        sw.RequiredVendor.Should().BeNull();
    }

    [Fact]
    public void Vp9_QsvIsTheOnlyHardwareOption()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Vp9);
        EncoderInfo hw = def.Encoders.FirstOrDefault(e => e.FfmpegName == "vp9_qsv");

        hw!.RequiredVendor.Should().Be(GpuVendor.Intel);
    }

    [Fact]
    public void Vp9_Libvpx_Quality_Range_Is_0_63()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Vp9);
        EncoderInfo sw = def.Encoders[0];

        sw.QualityRange.Min.Should().Be(0);
        sw.QualityRange.Max.Should().Be(63);
    }

    [Fact]
    public void Vp9_Libvpx_Supports_Crf()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Vp9);
        EncoderInfo sw = def.Encoders[0];

        sw.SupportedRateControl.Should().Contain(RateControlMode.Crf);
    }

    // ── AV1 codec definition ────────────────────────────────────────────────────

    [Fact]
    public void Av1Definition_Contains_Seven_Encoders()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Av1);
        def.Encoders.Should().HaveCount(7);
    }

    [Fact]
    public void Av1_Libsvtav1_Quality_Range_Is_0_63()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Av1);
        EncoderInfo sw = def.Encoders[0];

        sw.FfmpegName.Should().Be("libsvtav1");
        sw.QualityRange.Min.Should().Be(0);
        sw.QualityRange.Max.Should().Be(63);
        sw.QualityRange.Default.Should().Be(35);
    }

    [Fact]
    public void Av1_Libsvtav1_Supports10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Av1);
        EncoderInfo sw = def.Encoders[0];

        sw.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void Av1_Av1Nvenc_Quality_Range_Is_0_51()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Av1);
        EncoderInfo nvenc = def.Encoders.FirstOrDefault(e => e.FfmpegName == "av1_nvenc");

        nvenc!.QualityRange.Min.Should().Be(0);
        nvenc.QualityRange.Max.Should().Be(51);
    }

    [Fact]
    public void Av1_Av1Nvenc_Supports10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Av1);
        EncoderInfo nvenc = def.Encoders.FirstOrDefault(e => e.FfmpegName == "av1_nvenc");

        nvenc!.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void Av1_Av1Amf_Quality_Range_Is_0_255()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Av1);
        EncoderInfo amf = def.Encoders.FirstOrDefault(e => e.FfmpegName == "av1_amf");

        amf!.QualityRange.Min.Should().Be(0);
        amf.QualityRange.Max.Should().Be(255);
    }

    [Fact]
    public void Av1_Av1Qsv_Does_Not_Support10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Av1);
        EncoderInfo qsv = def.Encoders.FirstOrDefault(e => e.FfmpegName == "av1_qsv");

        qsv!.Supports10Bit.Should().BeFalse();
    }

    // ── Copy codec (stream remux) ───────────────────────────────────────────────

    [Fact]
    public void CopyVideoDefinition_Has_Single_Synthetic_Entry()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Copy);
        def.Encoders.Should().HaveCount(1);
    }

    [Fact]
    public void CopyVideoDefinition_Handle_Is_Copy()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(VideoCodecType.Copy);
        def.Encoders[0].FfmpegName.Should().Be("copy");
    }

    // ── CodecRegistry enumeration ───────────────────────────────────────────────

    [Fact]
    public void EnumerateVideoEncoders_Does_Not_Include_Copy()
    {
        List<(VideoCodecType, EncoderInfo)> encoders = _registry.EnumerateVideoEncoders().ToList();

        encoders.Should().NotContain(x => x.Item2.FfmpegName == "copy");
    }

    [Fact]
    public void EnumerateVideoEncoders_Includes_All_Real_Codecs()
    {
        List<(VideoCodecType, EncoderInfo)> encoders = _registry.EnumerateVideoEncoders().ToList();

        var codecs = encoders.Select(x => x.Item1).Distinct().ToList();

        codecs.Should().Contain(VideoCodecType.H264);
        codecs.Should().Contain(VideoCodecType.H265);
        codecs.Should().Contain(VideoCodecType.Av1);
        codecs.Should().Contain(VideoCodecType.Vp9);
    }

    [Fact]
    public void GetVideoEncoderByName_Returns_Encoder_By_Handle()
    {
        EncoderInfo? encoder = _registry.GetVideoEncoderByName("libx264");

        encoder.Should().NotBeNull();
        encoder!.FfmpegName.Should().Be("libx264");
    }

    [Fact]
    public void GetVideoEncoderByName_Returns_Null_For_Unknown()
    {
        EncoderInfo? encoder = _registry.GetVideoEncoderByName("unknown_encoder");

        encoder.Should().BeNull();
    }

    // ── Hardware vendor classification ──────────────────────────────────────────

    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    public void CodecRegistry_IsHardware_Recognizes_Nvenc(string handle)
    {
        CodecRegistry.IsHardware(handle).Should().BeTrue();
    }

    [Theory]
    [InlineData("h264_qsv")]
    [InlineData("hevc_qsv")]
    [InlineData("av1_qsv")]
    public void CodecRegistry_IsHardware_Recognizes_Qsv(string handle)
    {
        CodecRegistry.IsHardware(handle).Should().BeTrue();
    }

    [Theory]
    [InlineData("h264_amf")]
    [InlineData("hevc_amf")]
    [InlineData("av1_amf")]
    public void CodecRegistry_IsHardware_Recognizes_Amf(string handle)
    {
        CodecRegistry.IsHardware(handle).Should().BeTrue();
    }

    [Theory]
    [InlineData("h264_vaapi")]
    [InlineData("hevc_vaapi")]
    public void CodecRegistry_IsHardware_Recognizes_Vaapi(string handle)
    {
        CodecRegistry.IsHardware(handle).Should().BeTrue();
    }

    [Theory]
    [InlineData("h264_videotoolbox")]
    [InlineData("hevc_videotoolbox")]
    public void CodecRegistry_IsHardware_Recognizes_VideoToolbox(string handle)
    {
        CodecRegistry.IsHardware(handle).Should().BeTrue();
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libsvtav1")]
    [InlineData("libvpx-vp9")]
    public void CodecRegistry_IsHardware_Returns_False_For_Software(string handle)
    {
        CodecRegistry.IsHardware(handle).Should().BeFalse();
    }

    [Fact]
    public void CodecRegistry_IsHardware_Case_Insensitive()
    {
        CodecRegistry.IsHardware("H264_NVENC").Should().BeTrue();
        CodecRegistry.IsHardware("H264_QSV").Should().BeTrue();
        CodecRegistry.IsHardware("H264_AMF").Should().BeTrue();
    }
}
