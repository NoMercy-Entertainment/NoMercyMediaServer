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

public class Av1DefinitionTests
{
    private readonly Av1Definition _definition = new();

    [Fact]
    public void CodecType_IsAv1()
    {
        _definition.CodecType.Should().Be(expected: VideoCodecType.Av1);
    }

    [Fact]
    public void Has_Exactly7_Encoders()
    {
        _definition.Encoders.Should().HaveCount(expected: 7);
    }

    [Fact]
    public void NoAppleEncoder_Exists()
    {
        // Apple decodes AV1 but does NOT encode it
        _definition.Encoders.Should().NotContain(predicate: e => e.FfmpegName == "av1_videotoolbox");
        _definition.Encoders.Should().NotContain(predicate: e => e.RequiredVendor == GpuVendor.Apple);
    }

    [Fact]
    public void Libsvtav1_HasCorrectFields()
    {
        EncoderInfo sw = _definition.Encoders.Single(predicate: e => e.FfmpegName == "libsvtav1");

        sw.RequiredVendor.Should().BeNull();
        // 14 presets: "0" through "13"
        sw.Presets.Should().HaveCount(expected: 14);
        sw.Presets.Should().Contain(expected: "0");
        sw.Presets.Should().Contain(expected: "13");
        sw.QualityRange.Min.Should().Be(expected: 0);
        sw.QualityRange.Max.Should().Be(expected: 63);
        sw.QualityRange.Default.Should().Be(expected: 35);
        sw.SupportedRateControl.Should().Contain(expected: RateControlMode.Crf);
        sw.Supports10Bit.Should().BeTrue();
        sw.SupportsHdr.Should().BeTrue();
        sw.PixelFormat10Bit.Should().Be(expected: "yuv420p10le");
        sw.MaxConcurrentSessions.Should().Be(expected: int.MaxValue);
    }

    [Fact]
    public void LibaomAv1_HasCorrectFields()
    {
        EncoderInfo aom = _definition.Encoders.Single(predicate: e => e.FfmpegName == "libaom-av1");

        aom.RequiredVendor.Should().BeNull();
        // 9 presets: "0" through "8" (cpu-used values)
        aom.Presets.Should().HaveCount(expected: 9);
        aom.Presets.Should().Contain(expected: "0");
        aom.Presets.Should().Contain(expected: "8");
        aom.QualityRange.Min.Should().Be(expected: 0);
        aom.QualityRange.Max.Should().Be(expected: 63);
        aom.SupportedRateControl.Should().Contain(expected: RateControlMode.Crf);
        aom.MaxConcurrentSessions.Should().Be(expected: int.MaxValue);
    }

    [Fact]
    public void Librav1e_HasCorrectFields()
    {
        EncoderInfo rav1e = _definition.Encoders.Single(predicate: e => e.FfmpegName == "librav1e");

        rav1e.RequiredVendor.Should().BeNull();
        // 11 presets: "0" through "10"
        rav1e.Presets.Should().HaveCount(expected: 11);
        rav1e.Presets.Should().Contain(expected: "0");
        rav1e.Presets.Should().Contain(expected: "10");
        // librav1e QP range is 0-255
        rav1e.QualityRange.Min.Should().Be(expected: 0);
        rav1e.QualityRange.Max.Should().Be(expected: 255);
        rav1e.MaxConcurrentSessions.Should().Be(expected: int.MaxValue);
    }

    [Fact]
    public void Av1Nvenc_HasCorrectFields()
    {
        EncoderInfo nvenc = _definition.Encoders.Single(predicate: e => e.FfmpegName == "av1_nvenc");

        nvenc.RequiredVendor.Should().Be(expected: GpuVendor.Nvidia);
        nvenc.Presets.Should().BeEquivalentTo(expectation: ["p1", "p2", "p3", "p4", "p5", "p6", "p7"]);
        // AV1 NVENC: main profile only
        nvenc.Profiles.Should().ContainSingle(predicate: p => p == "main");
        nvenc.QualityRange.Min.Should().Be(expected: 0);
        nvenc.QualityRange.Max.Should().Be(expected: 51);
        nvenc.SupportedRateControl.Should().Contain(expected: RateControlMode.Cq);
        nvenc.Supports10Bit.Should().BeTrue();
        nvenc.SupportsHdr.Should().BeTrue();
        nvenc.MaxConcurrentSessions.Should().Be(expected: 12);
    }

    [Fact]
    public void Av1Amf_HasCorrectFields()
    {
        EncoderInfo amf = _definition.Encoders.Single(predicate: e => e.FfmpegName == "av1_amf");

        amf.RequiredVendor.Should().Be(expected: GpuVendor.Amd);
        amf.Presets.Should().HaveCount(expected: 4);
        amf.Presets.Should().Contain(expected: "speed");
        amf.Presets.Should().Contain(expected: "balanced");
        amf.Presets.Should().Contain(expected: "quality");
        amf.Presets.Should().Contain(expected: "high_quality");
        amf.Profiles.Should().Contain(expected: "main");
        // AMD AV1 QP range is 0-255 (NOT 0-51!)
        amf.QualityRange.Min.Should().Be(expected: 0);
        amf.QualityRange.Max.Should().Be(expected: 255);
        amf.MaxConcurrentSessions.Should().Be(expected: int.MaxValue);
    }

    [Fact]
    public void Av1Qsv_HasCorrectFields()
    {
        EncoderInfo qsv = _definition.Encoders.Single(predicate: e => e.FfmpegName == "av1_qsv");

        qsv.RequiredVendor.Should().Be(expected: GpuVendor.Intel);
        qsv.Presets.Should()
            .BeEquivalentTo(expectation: ["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"]);
        qsv.Profiles.Should().Contain(expected: "main");
        qsv.QualityRange.Min.Should().Be(expected: 1);
        qsv.QualityRange.Max.Should().Be(expected: 51);
        qsv.SupportedRateControl.Should().Contain(expected: RateControlMode.Icq);
        qsv.MaxConcurrentSessions.Should().Be(expected: int.MaxValue);
    }

    [Fact]
    public void Av1Vaapi_HasCorrectFields()
    {
        EncoderInfo vaapi = _definition.Encoders.Single(predicate: e => e.FfmpegName == "av1_vaapi");

        vaapi.RequiredVendor.Should().Be(expected: GpuVendor.Intel);
        vaapi.Presets.Should().BeEmpty();
        vaapi.Profiles.Should().Contain(expected: "main");
        // av1_vaapi QP range is 0-255
        vaapi.QualityRange.Min.Should().Be(expected: 0);
        vaapi.QualityRange.Max.Should().Be(expected: 255);
        vaapi.MaxConcurrentSessions.Should().Be(expected: int.MaxValue);
    }
}
