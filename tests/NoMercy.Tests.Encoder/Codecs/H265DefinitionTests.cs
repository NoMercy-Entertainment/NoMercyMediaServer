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

public class H265DefinitionTests
{
    private readonly H265Definition _definition = new();

    [Fact]
    public void CodecType_IsH265()
    {
        _definition.CodecType.Should().Be(expected: VideoCodecType.H265);
    }

    [Fact]
    public void Has_Exactly6_Encoders()
    {
        _definition.Encoders.Should().HaveCount(expected: 6);
    }

    [Fact]
    public void Libx265_HasCorrectFields()
    {
        EncoderInfo sw = _definition.Encoders.Single(predicate: e => e.FfmpegName == "libx265");

        sw.RequiredVendor.Should().BeNull();
        sw.Presets.Should().HaveCount(expected: 10);
        sw.Presets.Should().Contain(expected: "ultrafast");
        sw.Presets.Should().Contain(expected: "veryslow");
        sw.Presets.Should().Contain(expected: "placebo");
        sw.Profiles.Should().Contain(expected: "main");
        sw.Profiles.Should().Contain(expected: "main10");
        sw.Profiles.Should().Contain(expected: "main12");
        sw.Profiles.Should().Contain(expected: "main422-10");
        sw.Profiles.Should().Contain(expected: "main444-10");
        sw.QualityRange.Min.Should().Be(expected: 0);
        sw.QualityRange.Max.Should().Be(expected: 51);
        sw.QualityRange.Default.Should().Be(expected: 28);
        sw.SupportedRateControl.Should().Contain(expected: RateControlMode.Crf);
        sw.Supports10Bit.Should().BeTrue();
        sw.SupportsHdr.Should().BeTrue();
        sw.PixelFormat10Bit.Should().Be(expected: "yuv420p10le");
        sw.MaxConcurrentSessions.Should().Be(expected: int.MaxValue);
    }

    [Fact]
    public void HevcNvenc_HasCorrectFields()
    {
        EncoderInfo nvenc = _definition.Encoders.Single(predicate: e => e.FfmpegName == "hevc_nvenc");

        nvenc.RequiredVendor.Should().Be(expected: GpuVendor.Nvidia);
        nvenc.Presets.Should().BeEquivalentTo(expectation: ["p1", "p2", "p3", "p4", "p5", "p6", "p7"]);
        nvenc.Profiles.Should().Contain(expected: "main");
        nvenc.Profiles.Should().Contain(expected: "main10");
        nvenc.Profiles.Should().Contain(expected: "rext");
        nvenc.QualityRange.Min.Should().Be(expected: 0);
        nvenc.QualityRange.Max.Should().Be(expected: 51);
        nvenc.SupportedRateControl.Should().Contain(expected: RateControlMode.Cq);
        nvenc.Supports10Bit.Should().BeTrue();
        nvenc.SupportsHdr.Should().BeTrue();
        nvenc.MaxConcurrentSessions.Should().Be(expected: 12);
    }

    [Fact]
    public void HevcAmf_HasCorrectFields()
    {
        EncoderInfo amf = _definition.Encoders.Single(predicate: e => e.FfmpegName == "hevc_amf");

        amf.RequiredVendor.Should().Be(expected: GpuVendor.Amd);
        amf.Presets.Should().BeEquivalentTo(expectation: ["speed", "balanced", "quality"]);
        amf.Profiles.Should().Contain(expected: "main");
        amf.Profiles.Should().Contain(expected: "main10");
        amf.QualityRange.Min.Should().Be(expected: 0);
        amf.QualityRange.Max.Should().Be(expected: 51);
        amf.Supports10Bit.Should().BeTrue();
        amf.SupportsHdr.Should().BeTrue();
        amf.MaxConcurrentSessions.Should().Be(expected: int.MaxValue);
        amf.VendorSpecificFlags.Should().ContainKey(expected: "-usage");
    }

    [Fact]
    public void HevcQsv_HasCorrectFields()
    {
        EncoderInfo qsv = _definition.Encoders.Single(predicate: e => e.FfmpegName == "hevc_qsv");

        qsv.RequiredVendor.Should().Be(expected: GpuVendor.Intel);
        qsv.Presets.Should()
            .BeEquivalentTo(expectation: ["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"]);
        qsv.Profiles.Should().Contain(expected: "main");
        qsv.Profiles.Should().Contain(expected: "main10");
        qsv.Profiles.Should().Contain(expected: "mainsp");
        qsv.Profiles.Should().Contain(expected: "rext");
        qsv.Profiles.Should().Contain(expected: "scc");
        qsv.QualityRange.Min.Should().Be(expected: 1);
        qsv.QualityRange.Max.Should().Be(expected: 51);
        qsv.SupportedRateControl.Should().Contain(expected: RateControlMode.Icq);
        qsv.MaxConcurrentSessions.Should().Be(expected: int.MaxValue);
    }

    [Fact]
    public void HevcVaapi_HasCorrectFields()
    {
        EncoderInfo vaapi = _definition.Encoders.Single(predicate: e => e.FfmpegName == "hevc_vaapi");

        vaapi.RequiredVendor.Should().Be(expected: GpuVendor.Intel);
        vaapi.Presets.Should().BeEmpty();
        vaapi.Profiles.Should().Contain(expected: "main");
        vaapi.Profiles.Should().Contain(expected: "main10");
        vaapi.Supports10Bit.Should().BeTrue();
        vaapi.SupportsHdr.Should().BeTrue();
    }

    [Fact]
    public void HevcVideoToolbox_HasCorrectFields()
    {
        EncoderInfo vtb = _definition.Encoders.Single(predicate: e => e.FfmpegName == "hevc_videotoolbox");

        vtb.RequiredVendor.Should().Be(expected: GpuVendor.Apple);
        vtb.Presets.Should().BeEmpty();
        // HEVC VTB profiles are numeric: "1" = Main, "2" = Main10
        vtb.Profiles.Should().BeEquivalentTo(expectation: ["1", "2"]);
        vtb.QualityRange.Min.Should().Be(expected: 0);
        vtb.QualityRange.Max.Should().Be(expected: 100);
        vtb.SupportedRateControl.Should().Contain(expected: RateControlMode.QualityLevel);
        // hevc_videotoolbox REQUIRES -tag:v hvc1
        vtb.VendorSpecificFlags.Should().ContainKey(expected: "-tag:v");
        vtb.VendorSpecificFlags[key: "-tag:v"].Should().Be(expected: "hvc1");
    }
}
