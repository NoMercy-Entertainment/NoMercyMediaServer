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
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Codecs;

public class CodecResolverTests
{
    private readonly CodecRegistry _registry = new();

    [Fact]
    public void PreferHardware_WithNvidia_SelectsNvenc()
    {
        IHardwareCapabilities caps = MakeCaps(
            vendor: GpuVendor.Nvidia,
            codecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );
        CodecResolver resolver = new(registry: _registry);
        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H264, hardware: caps);
        resolved.FfmpegEncoderName.Should().Be(expected: "h264_nvenc");
        resolved.Device.Should().NotBeNull();
        resolved.Device!.Vendor.Should().Be(expected: GpuVendor.Nvidia);
    }

    [Fact]
    public void PreferHardware_WithAmd_SelectsAmf()
    {
        IHardwareCapabilities caps = MakeCaps(
            vendor: GpuVendor.Amd,
            codecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );
        CodecResolver resolver = new(registry: _registry);
        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H265, hardware: caps);
        resolved.FfmpegEncoderName.Should().Be(expected: "hevc_amf");
    }

    [Fact]
    public void PreferHardware_NoGpu_FallsBackToSoftware()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);
        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H264, hardware: caps);
        resolved.FfmpegEncoderName.Should().Be(expected: "libx264");
        resolved.Device.Should().BeNull();
    }

    [Fact]
    public void ForceSoftware_WithNvidia_StillSelectsSoftware()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);
        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: EncoderPreference.ForceSoftware
        );
        resolved.FfmpegEncoderName.Should().Be(expected: "libx264");
        resolved.Device.Should().BeNull();
    }

    [Fact]
    public void ForceHardware_NoGpu_Throws()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);
        Action act = () =>
            resolver.Resolve(codec: VideoCodecType.H264, hardware: caps, preference: EncoderPreference.ForceHardware);
        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*hardware*");
    }

    [Fact]
    public void Vp9_WithNvidia_FallsBackToSoftware()
    {
        IHardwareCapabilities caps = MakeCaps(
            vendor: GpuVendor.Nvidia,
            codecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );
        CodecResolver resolver = new(registry: _registry);
        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Vp9, hardware: caps);
        resolved.FfmpegEncoderName.Should().Be(expected: "libvpx-vp9");
    }

    [Fact]
    public void Vp9_WithIntel_SelectsQsv()
    {
        IHardwareCapabilities caps = MakeCaps(
            vendor: GpuVendor.Intel,
            codecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1, VideoCodecType.Vp9]
        );
        CodecResolver resolver = new(registry: _registry);
        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Vp9, hardware: caps);
        resolved.FfmpegEncoderName.Should().Be(expected: "vp9_qsv");
    }

    [Fact]
    public void Av1_PrefersSvtAv1_ForSoftware()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);
        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Av1, hardware: caps);
        resolved.FfmpegEncoderName.Should().Be(expected: "libsvtav1");
    }

    [Fact]
    public void DefaultRateControl_MatchesEncoderType()
    {
        IHardwareCapabilities noCaps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);
        ResolvedCodec sw = resolver.Resolve(codec: VideoCodecType.H264, hardware: noCaps);
        sw.DefaultRateControl.Should().Be(expected: RateControlMode.Crf);

        IHardwareCapabilities nvidiaCaps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        ResolvedCodec hw = resolver.Resolve(codec: VideoCodecType.H264, hardware: nvidiaCaps);
        hw.DefaultRateControl.Should().Be(expected: RateControlMode.Cq);
    }

    private static IHardwareCapabilities MakeCaps(GpuVendor? vendor, VideoCodecType[] codecs)
    {
        List<GpuDevice> gpus = [];
        if (vendor.HasValue)
            gpus.Add(item: new(Vendor: vendor.Value, Name: "Test GPU", VramMb: 8192, MaxEncoderSessions: 12, SupportedCodecs: codecs));
        return new HardwareCapabilities(Gpus: gpus, CpuCores: 8);
    }
}
