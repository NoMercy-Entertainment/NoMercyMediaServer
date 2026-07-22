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

public class H264DefinitionTests
{
    private readonly H264Definition _definition = new();

    [Fact]
    public void CodecType_IsH264()
    {
        _definition.CodecType.Should().Be(expected: VideoCodecType.H264);
    }

    [Fact]
    public void Has_Exactly6_Encoders()
    {
        _definition.Encoders.Should().HaveCount(expected: 6);
    }

    [Fact]
    public void SoftwareEncoder_IsLibx264()
    {
        EncoderInfo sw = _definition.Encoders.Single(predicate: e => e.FfmpegName == "libx264");
        sw.RequiredVendor.Should().BeNull();
        sw.Presets.Should().Contain(expected: "ultrafast");
        sw.Presets.Should().Contain(expected: "veryslow");
        sw.Presets.Should().Contain(expected: "placebo");
        sw.Presets.Should().HaveCount(expected: 10);
        sw.Profiles.Should().Contain(expected: "baseline");
        sw.Profiles.Should().Contain(expected: "main");
        sw.Profiles.Should().Contain(expected: "high");
        sw.Profiles.Should().Contain(expected: "high10");
        sw.Profiles.Should().Contain(expected: "high422");
        sw.Profiles.Should().Contain(expected: "high444p");
        sw.QualityRange.Min.Should().Be(expected: 0);
        sw.QualityRange.Max.Should().Be(expected: 51);
        sw.QualityRange.Default.Should().Be(expected: 23);
        sw.SupportedRateControl.Should().Contain(expected: RateControlMode.Crf);
        sw.MaxConcurrentSessions.Should().Be(expected: int.MaxValue);
    }

    [Fact]
    public void Nvenc_HasCorrectPresets()
    {
        EncoderInfo nvenc = _definition.Encoders.Single(predicate: e => e.FfmpegName == "h264_nvenc");
        nvenc.RequiredVendor.Should().Be(expected: GpuVendor.Nvidia);
        nvenc.Presets.Should().BeEquivalentTo(expectation: ["p1", "p2", "p3", "p4", "p5", "p6", "p7"]);
        nvenc.Profiles.Should().Contain(expected: "baseline");
        nvenc.Profiles.Should().Contain(expected: "main");
        nvenc.Profiles.Should().Contain(expected: "high");
        nvenc.QualityRange.Min.Should().Be(expected: 0);
        nvenc.QualityRange.Max.Should().Be(expected: 51);
        nvenc.MaxConcurrentSessions.Should().Be(expected: 12);
        nvenc.SupportedRateControl.Should().Contain(expected: RateControlMode.Cq);
        nvenc.SupportedRateControl.Should().Contain(expected: RateControlMode.Vbr);
        nvenc.Supports10Bit.Should().BeFalse();
    }

    [Fact]
    public void Amf_HasCorrectFields()
    {
        EncoderInfo amf = _definition.Encoders.Single(predicate: e => e.FfmpegName == "h264_amf");
        amf.RequiredVendor.Should().Be(expected: GpuVendor.Amd);
        amf.Presets.Should().BeEquivalentTo(expectation: ["speed", "balanced", "quality"]);
        amf.Profiles.Should().Contain(expected: "constrained_baseline");
        amf.Profiles.Should().Contain(expected: "constrained_high");
        amf.MaxConcurrentSessions.Should().Be(expected: int.MaxValue);
        amf.Supports10Bit.Should().BeFalse();
    }

    [Fact]
    public void Qsv_HasCorrectPresets()
    {
        EncoderInfo qsv = _definition.Encoders.Single(predicate: e => e.FfmpegName == "h264_qsv");
        qsv.RequiredVendor.Should().Be(expected: GpuVendor.Intel);
        qsv.Presets.Should()
            .BeEquivalentTo(expectation: ["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"]);
        qsv.Profiles.Should().BeEquivalentTo(expectation: ["baseline", "main", "high"]);
        qsv.QualityRange.Min.Should().Be(expected: 1);
        qsv.QualityRange.Max.Should().Be(expected: 51);
        qsv.SupportedRateControl.Should().Contain(expected: RateControlMode.Icq);
        qsv.MaxConcurrentSessions.Should().Be(expected: int.MaxValue);
    }

    [Fact]
    public void Vaapi_HasNoPresets()
    {
        EncoderInfo vaapi = _definition.Encoders.Single(predicate: e => e.FfmpegName == "h264_vaapi");
        vaapi.RequiredVendor.Should().Be(expected: GpuVendor.Intel);
        vaapi.Presets.Should().BeEmpty();
        vaapi.Profiles.Should().BeEquivalentTo(expectation: ["constrained_baseline", "main", "high"]);
    }

    [Fact]
    public void VideoToolbox_HasNumericProfiles()
    {
        EncoderInfo vtb = _definition.Encoders.Single(predicate: e => e.FfmpegName == "h264_videotoolbox");
        vtb.RequiredVendor.Should().Be(expected: GpuVendor.Apple);
        vtb.Presets.Should().BeEmpty();
        vtb.Profiles.Should().BeEquivalentTo(expectation: ["66", "77", "100"]);
        vtb.QualityRange.Min.Should().Be(expected: 0);
        vtb.QualityRange.Max.Should().Be(expected: 100);
        vtb.SupportedRateControl.Should().Contain(expected: RateControlMode.QualityLevel);
        vtb.VendorSpecificFlags.Should().BeEmpty();
    }

    [Theory]
    [InlineData(data: "vp9_nvenc")]
    [InlineData(data: "vp9_amf")]
    [InlineData(data: "vp9_videotoolbox")]
    [InlineData(data: "av1_videotoolbox")]
    public void PhantomCodecs_DoNotExist(string phantomName)
    {
        _definition.Encoders.Should().NotContain(predicate: e => e.FfmpegName == phantomName);
    }
}
