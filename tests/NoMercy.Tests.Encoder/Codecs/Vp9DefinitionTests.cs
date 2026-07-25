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

public class Vp9DefinitionTests
{
    private readonly Vp9Definition _definition = new();

    [Fact]
    public void CodecType_IsVp9()
    {
        _definition.CodecType.Should().Be(VideoCodecType.Vp9);
    }

    [Fact]
    public void Has_Exactly3_Encoders()
    {
        _definition.Encoders.Should().HaveCount(3);
    }

    [Fact]
    public void NoPhantomHardwareEncoders_Exist()
    {
        // vp9_nvenc, vp9_amf, vp9_videotoolbox do NOT exist
        _definition.Encoders.Should().NotContain(e => e.FfmpegName == "vp9_nvenc");
        _definition.Encoders.Should().NotContain(e => e.FfmpegName == "vp9_amf");
        _definition.Encoders.Should().NotContain(e => e.FfmpegName == "vp9_videotoolbox");
        // VP9 hardware encoding is Intel-only
        _definition.Encoders.Should().NotContain(e => e.RequiredVendor == GpuVendor.Nvidia);
        _definition.Encoders.Should().NotContain(e => e.RequiredVendor == GpuVendor.Amd);
        _definition.Encoders.Should().NotContain(e => e.RequiredVendor == GpuVendor.Apple);
    }

    [Fact]
    public void LibvpxVp9_HasCorrectFields()
    {
        EncoderInfo sw = _definition.Encoders.Single(e => e.FfmpegName == "libvpx-vp9");

        sw.RequiredVendor.Should().BeNull();
        sw.Presets.Should().BeEmpty();
        // Numeric profile ids — the values ffmpeg's -profile accepts for VP9.
        sw.Profiles.Should().Contain("0");
        sw.Profiles.Should().Contain("1");
        sw.Profiles.Should().Contain("2");
        sw.Profiles.Should().Contain("3");
        sw.QualityRange.Min.Should().Be(0);
        sw.QualityRange.Max.Should().Be(63);
        sw.SupportedRateControl.Should().Contain(RateControlMode.Crf);
        sw.Supports10Bit.Should().BeTrue();
        sw.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Vp9Qsv_IsIntelOnly()
    {
        EncoderInfo qsv = _definition.Encoders.Single(e => e.FfmpegName == "vp9_qsv");

        qsv.RequiredVendor.Should().Be(GpuVendor.Intel);
        qsv.Presets.Should()
            .BeEquivalentTo(["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"]);
        qsv.QualityRange.Min.Should().Be(1);
        qsv.QualityRange.Max.Should().Be(51);
        qsv.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Vp9Vaapi_IsIntelOnly_NoPresets()
    {
        EncoderInfo vaapi = _definition.Encoders.Single(e => e.FfmpegName == "vp9_vaapi");

        vaapi.RequiredVendor.Should().Be(GpuVendor.Intel);
        vaapi.Presets.Should().BeEmpty();
        vaapi.Profiles.Should().Contain("0");
        vaapi.Profiles.Should().Contain("1");
        vaapi.Profiles.Should().Contain("2");
        vaapi.Profiles.Should().Contain("3");
        vaapi.QualityRange.Min.Should().Be(0);
        vaapi.QualityRange.Max.Should().Be(255);
        vaapi.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }
}
