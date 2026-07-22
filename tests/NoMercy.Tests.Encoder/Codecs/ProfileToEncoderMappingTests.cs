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
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        def.Encoders.Should().HaveCount(expected: 6);
    }

    [Fact]
    public void H264_Software_Libx264_Is_First_Entry()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo sw = def.Encoders[0];

        sw.FfmpegName.Should().Be(expected: "libx264");
        sw.RequiredVendor.Should().BeNull();
    }

    [Fact]
    public void H264_Libx264_Has_10_Presets()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo sw = def.Encoders[0];

        sw.Presets.Should().HaveCount(expected: 10);
        sw.Presets.Should()
            .Equal(expected: ["ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow", "placebo"]
            );
    }

    [Fact]
    public void H264_Libx264_Has_Six_Profiles()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo sw = def.Encoders[0];

        sw.Profiles.Should().Contain(expected: "baseline");
        sw.Profiles.Should().Contain(expected: "main");
        sw.Profiles.Should().Contain(expected: "high");
        sw.Profiles.Should().Contain(expected: "high10");
        sw.Profiles.Should().Contain(expected: "high422");
        sw.Profiles.Should().Contain(expected: "high444p");
    }

    [Fact]
    public void H264_Libx264_Quality_Range_Is_0_51()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo sw = def.Encoders[0];

        sw.QualityRange.Min.Should().Be(expected: 0);
        sw.QualityRange.Max.Should().Be(expected: 51);
        sw.QualityRange.Default.Should().Be(expected: 23);
    }

    [Fact]
    public void H264_Nvenc_Has_Seven_Presets()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo nvenc = def.Encoders.First(predicate: e => e.FfmpegName == "h264_nvenc");

        nvenc.Should().NotBeNull();
        nvenc!.Presets.Should().HaveCount(expected: 7);
        nvenc.Presets.Should().Equal(expected: ["p1", "p2", "p3", "p4", "p5", "p6", "p7"]);
    }

    [Fact]
    public void H264_Nvenc_Does_Not_Have_High10_Profile()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo nvenc = def.Encoders.First(predicate: e => e.FfmpegName == "h264_nvenc");

        nvenc!.Profiles.Should().NotContain(unexpected: "high10");
        nvenc.Profiles.Should().Equal(expected: ["baseline", "main", "high"]);
    }

    [Fact]
    public void H264_Nvenc_Supports_Cq_Cqp_Cbr_Vbr()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo nvenc = def.Encoders.First(predicate: e => e.FfmpegName == "h264_nvenc");

        nvenc!.SupportedRateControl.Should().Contain(expected: RateControlMode.Cq);
        nvenc.SupportedRateControl.Should().Contain(expected: RateControlMode.Cqp);
        nvenc.SupportedRateControl.Should().Contain(expected: RateControlMode.Cbr);
        nvenc.SupportedRateControl.Should().Contain(expected: RateControlMode.Vbr);
        nvenc.SupportedRateControl.Should().NotContain(unexpected: RateControlMode.Crf);
    }

    [Fact]
    public void H264_Amf_Has_Three_Presets()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo amf = def.Encoders.First(predicate: e => e.FfmpegName == "h264_amf");

        amf!.Presets.Should().HaveCount(expected: 3);
        amf.Presets.Should().Equal(expected: ["speed", "balanced", "quality"]);
    }

    [Fact]
    public void H264_Amf_Supports_Qvbr_And_Hqvbr()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo amf = def.Encoders.First(predicate: e => e.FfmpegName == "h264_amf");

        amf!.SupportedRateControl.Should().Contain(expected: RateControlMode.Qvbr);
        amf.SupportedRateControl.Should().Contain(expected: RateControlMode.Hqvbr);
        amf.SupportedRateControl.Should().Contain(expected: RateControlMode.Hqcbr);
    }

    [Fact]
    public void H264_Amf_Has_VendorSpecificFlag_Usage()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo amf = def.Encoders.First(predicate: e => e.FfmpegName == "h264_amf");

        amf!.VendorSpecificFlags.Should().ContainKey(expected: "-usage");
        amf.VendorSpecificFlags[key: "-usage"].Should().Be(expected: "transcoding");
    }

    [Fact]
    public void H264_Qsv_Has_Seven_Presets_No_Ultrafast()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo qsv = def.Encoders.First(predicate: e => e.FfmpegName == "h264_qsv");

        qsv!.Presets.Should().HaveCount(expected: 7);
        qsv.Presets.Should().NotContain(unexpected: "ultrafast");
        qsv.Presets.Should().NotContain(unexpected: "placebo");
    }

    [Fact]
    public void H264_Qsv_Quality_Range_Min_Is_One()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo qsv = def.Encoders.First(predicate: e => e.FfmpegName == "h264_qsv");

        qsv!.QualityRange.Min.Should().Be(expected: 1);
        qsv.QualityRange.Max.Should().Be(expected: 51);
    }

    [Fact]
    public void H264_Qsv_Supports_Icq()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo qsv = def.Encoders.First(predicate: e => e.FfmpegName == "h264_qsv");

        qsv!.SupportedRateControl.Should().Contain(expected: RateControlMode.Icq);
    }

    [Fact]
    public void H264_Vaapi_Has_No_Presets()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo vaapi = def.Encoders.First(predicate: e => e.FfmpegName == "h264_vaapi");

        vaapi!.Presets.Should().BeEmpty();
    }

    [Fact]
    public void H264_VideoToolbox_Quality_Range_Is_0_100()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo vt = def.Encoders.First(predicate: e => e.FfmpegName == "h264_videotoolbox");

        vt!.QualityRange.Min.Should().Be(expected: 0);
        vt.QualityRange.Max.Should().Be(expected: 100);
        vt.QualityRange.Default.Should().Be(expected: 50);
    }

    [Fact]
    public void H264_VideoToolbox_Has_Numeric_Profiles()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo vt = def.Encoders.First(predicate: e => e.FfmpegName == "h264_videotoolbox");

        vt!.Profiles.Should().Equal(expected: ["66", "77", "100"]);
    }

    [Fact]
    public void H264_VideoToolbox_Supports_QualityLevel_And_Cbr()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H264);
        EncoderInfo vt = def.Encoders.First(predicate: e => e.FfmpegName == "h264_videotoolbox");

        vt!.SupportedRateControl.Should().Contain(expected: RateControlMode.QualityLevel);
        vt.SupportedRateControl.Should().Contain(expected: RateControlMode.Cbr);
        vt.SupportedRateControl.Should().HaveCount(expected: 2);
    }

    // ── H.265 / HEVC codec definition ────────────────────────────────────────────

    [Fact]
    public void H265Definition_Contains_Six_Encoders()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H265);
        def.Encoders.Should().HaveCount(expected: 6);
    }

    [Fact]
    public void H265_Libx265_Supports10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H265);
        EncoderInfo libx265 = def.Encoders[0];

        libx265.FfmpegName.Should().Be(expected: "libx265");
        libx265.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void H265_Libx265_Supports_Hdr()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H265);
        EncoderInfo libx265 = def.Encoders[0];

        libx265.SupportsHdr.Should().BeTrue();
    }

    [Fact]
    public void H265_HevcNvenc_Supports10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H265);
        EncoderInfo nvenc = def.Encoders.First(predicate: e => e.FfmpegName == "hevc_nvenc");

        nvenc!.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void H265_HevcAmf_Supports10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H265);
        EncoderInfo amf = def.Encoders.First(predicate: e => e.FfmpegName == "hevc_amf");

        amf!.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void H265_HevcQsv_Supports10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H265);
        EncoderInfo qsv = def.Encoders.First(predicate: e => e.FfmpegName == "hevc_qsv");

        qsv!.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void H265_HevcVideoToolbox_Has_Numeric_Profiles()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.H265);
        EncoderInfo vt = def.Encoders.First(predicate: e => e.FfmpegName == "hevc_videotoolbox");

        vt!.Profiles.Should().HaveCount(expected: 2);
        vt.Profiles.Should().Contain(expected: "1");
        vt.Profiles.Should().Contain(expected: "2");
    }

    // ── VP9 codec definition ────────────────────────────────────────────────────

    [Fact]
    public void Vp9Definition_Contains_Multiple_Encoders()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Vp9);
        def.Encoders.Should().HaveCountGreaterThanOrEqualTo(expected: 2);
    }

    [Fact]
    public void Vp9_Libvpx_Is_Software_Fallback()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Vp9);
        EncoderInfo sw = def.Encoders[0];

        sw.FfmpegName.Should().Be(expected: "libvpx-vp9");
        sw.RequiredVendor.Should().BeNull();
    }

    [Fact]
    public void Vp9_QsvIsTheOnlyHardwareOption()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Vp9);
        EncoderInfo hw = def.Encoders.First(predicate: e => e.FfmpegName == "vp9_qsv");

        hw!.RequiredVendor.Should().Be(expected: GpuVendor.Intel);
    }

    [Fact]
    public void Vp9_Libvpx_Quality_Range_Is_0_63()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Vp9);
        EncoderInfo sw = def.Encoders[0];

        sw.QualityRange.Min.Should().Be(expected: 0);
        sw.QualityRange.Max.Should().Be(expected: 63);
    }

    [Fact]
    public void Vp9_Libvpx_Supports_Crf()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Vp9);
        EncoderInfo sw = def.Encoders[0];

        sw.SupportedRateControl.Should().Contain(expected: RateControlMode.Crf);
    }

    // ── AV1 codec definition ────────────────────────────────────────────────────

    [Fact]
    public void Av1Definition_Contains_Seven_Encoders()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Av1);
        def.Encoders.Should().HaveCount(expected: 7);
    }

    [Fact]
    public void Av1_Libsvtav1_Quality_Range_Is_0_63()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Av1);
        EncoderInfo sw = def.Encoders[0];

        sw.FfmpegName.Should().Be(expected: "libsvtav1");
        sw.QualityRange.Min.Should().Be(expected: 0);
        sw.QualityRange.Max.Should().Be(expected: 63);
        sw.QualityRange.Default.Should().Be(expected: 35);
    }

    [Fact]
    public void Av1_Libsvtav1_Supports10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Av1);
        EncoderInfo sw = def.Encoders[0];

        sw.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void Av1_Av1Nvenc_Quality_Range_Is_0_51()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Av1);
        EncoderInfo nvenc = def.Encoders.First(predicate: e => e.FfmpegName == "av1_nvenc");

        nvenc!.QualityRange.Min.Should().Be(expected: 0);
        nvenc.QualityRange.Max.Should().Be(expected: 51);
    }

    [Fact]
    public void Av1_Av1Nvenc_Supports10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Av1);
        EncoderInfo nvenc = def.Encoders.First(predicate: e => e.FfmpegName == "av1_nvenc");

        nvenc!.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void Av1_Av1Amf_Quality_Range_Is_0_255()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Av1);
        EncoderInfo amf = def.Encoders.First(predicate: e => e.FfmpegName == "av1_amf");

        amf!.QualityRange.Min.Should().Be(expected: 0);
        amf.QualityRange.Max.Should().Be(expected: 255);
    }

    [Fact]
    public void Av1_Av1Qsv_Does_Not_Support10Bit()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Av1);
        EncoderInfo qsv = def.Encoders.First(predicate: e => e.FfmpegName == "av1_qsv");

        qsv!.Supports10Bit.Should().BeFalse();
    }

    // ── Copy codec (stream remux) ───────────────────────────────────────────────

    [Fact]
    public void CopyVideoDefinition_Has_Single_Synthetic_Entry()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Copy);
        def.Encoders.Should().HaveCount(expected: 1);
    }

    [Fact]
    public void CopyVideoDefinition_Handle_Is_Copy()
    {
        ICodecDefinition def = _registry.GetVideoDefinition(codecType: VideoCodecType.Copy);
        def.Encoders[0].FfmpegName.Should().Be(expected: "copy");
    }

    // ── CodecRegistry enumeration ───────────────────────────────────────────────

    [Fact]
    public void EnumerateVideoEncoders_Does_Not_Include_Copy()
    {
        List<(VideoCodecType, EncoderInfo)> encoders = _registry.EnumerateVideoEncoders().ToList();

        encoders.Should().NotContain(predicate: x => x.Item2.FfmpegName == "copy");
    }

    [Fact]
    public void EnumerateVideoEncoders_Includes_All_Real_Codecs()
    {
        List<(VideoCodecType, EncoderInfo)> encoders = _registry.EnumerateVideoEncoders().ToList();

        var codecs = encoders.Select(selector: x => x.Item1).Distinct().ToList();

        codecs.Should().Contain(expected: VideoCodecType.H264);
        codecs.Should().Contain(expected: VideoCodecType.H265);
        codecs.Should().Contain(expected: VideoCodecType.Av1);
        codecs.Should().Contain(expected: VideoCodecType.Vp9);
    }

    [Fact]
    public void GetVideoEncoderByName_Returns_Encoder_By_Handle()
    {
        EncoderInfo? encoder = _registry.GetVideoEncoderByName(ffmpegName: "libx264");

        encoder.Should().NotBeNull();
        encoder!.FfmpegName.Should().Be(expected: "libx264");
    }

    [Fact]
    public void GetVideoEncoderByName_Returns_Null_For_Unknown()
    {
        EncoderInfo? encoder = _registry.GetVideoEncoderByName(ffmpegName: "unknown_encoder");

        encoder.Should().BeNull();
    }

    // ── Hardware vendor classification ──────────────────────────────────────────

    [Theory]
    [InlineData(data: "h264_nvenc")]
    [InlineData(data: "hevc_nvenc")]
    [InlineData(data: "av1_nvenc")]
    public void CodecRegistry_IsHardware_Recognizes_Nvenc(string handle)
    {
        CodecRegistry.IsHardware(ffmpegEncoderName: handle).Should().BeTrue();
    }

    [Theory]
    [InlineData(data: "h264_qsv")]
    [InlineData(data: "hevc_qsv")]
    [InlineData(data: "av1_qsv")]
    public void CodecRegistry_IsHardware_Recognizes_Qsv(string handle)
    {
        CodecRegistry.IsHardware(ffmpegEncoderName: handle).Should().BeTrue();
    }

    [Theory]
    [InlineData(data: "h264_amf")]
    [InlineData(data: "hevc_amf")]
    [InlineData(data: "av1_amf")]
    public void CodecRegistry_IsHardware_Recognizes_Amf(string handle)
    {
        CodecRegistry.IsHardware(ffmpegEncoderName: handle).Should().BeTrue();
    }

    [Theory]
    [InlineData(data: "h264_vaapi")]
    [InlineData(data: "hevc_vaapi")]
    public void CodecRegistry_IsHardware_Recognizes_Vaapi(string handle)
    {
        CodecRegistry.IsHardware(ffmpegEncoderName: handle).Should().BeTrue();
    }

    [Theory]
    [InlineData(data: "h264_videotoolbox")]
    [InlineData(data: "hevc_videotoolbox")]
    public void CodecRegistry_IsHardware_Recognizes_VideoToolbox(string handle)
    {
        CodecRegistry.IsHardware(ffmpegEncoderName: handle).Should().BeTrue();
    }

    [Theory]
    [InlineData(data: "libx264")]
    [InlineData(data: "libx265")]
    [InlineData(data: "libsvtav1")]
    [InlineData(data: "libvpx-vp9")]
    public void CodecRegistry_IsHardware_Returns_False_For_Software(string handle)
    {
        CodecRegistry.IsHardware(ffmpegEncoderName: handle).Should().BeFalse();
    }

    [Fact]
    public void CodecRegistry_IsHardware_Case_Insensitive()
    {
        CodecRegistry.IsHardware(ffmpegEncoderName: "H264_NVENC").Should().BeTrue();
        CodecRegistry.IsHardware(ffmpegEncoderName: "H264_QSV").Should().BeTrue();
        CodecRegistry.IsHardware(ffmpegEncoderName: "H264_AMF").Should().BeTrue();
    }
}
