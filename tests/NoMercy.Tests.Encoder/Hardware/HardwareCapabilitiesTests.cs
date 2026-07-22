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

namespace NoMercy.Tests.Encoder.Hardware;

public class HardwareCapabilitiesTests
{
    [Fact]
    public void EmptyCapabilities_HasNoGpuEncoders()
    {
        HardwareCapabilities caps = new(Gpus: [], CpuCores: 4);
        caps.Gpus.Should().BeEmpty();
        caps.HasGpu.Should().BeFalse();
        caps.CpuCores.Should().Be(expected: 4);
    }

    [Fact]
    public void WithNvidiaGpu_HasGpuIsTrue()
    {
        GpuDevice gpu = new(
            Vendor: GpuVendor.Nvidia,
            Name: "GeForce RTX 4090",
            VramMb: 24576,
            MaxEncoderSessions: 12,
            SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );
        HardwareCapabilities caps = new(Gpus: [gpu], CpuCores: 16);
        caps.HasGpu.Should().BeTrue();
        caps.Gpus.Should().HaveCount(expected: 1);
        caps.Gpus[index: 0].Vendor.Should().Be(expected: GpuVendor.Nvidia);
    }

    [Fact]
    public void SupportsCodecOnGpu_True_WhenGpuHasCodec()
    {
        GpuDevice gpu = new(
            Vendor: GpuVendor.Nvidia,
            Name: "RTX 4090",
            VramMb: 24576,
            MaxEncoderSessions: 12,
            SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );
        HardwareCapabilities caps = new(Gpus: [gpu], CpuCores: 16);
        caps.SupportsHardwareEncoding(codec: VideoCodecType.H264).Should().BeTrue();
        caps.SupportsHardwareEncoding(codec: VideoCodecType.Av1).Should().BeTrue();
    }

    [Fact]
    public void SupportsCodecOnGpu_False_WhenNoGpuHasCodec()
    {
        GpuDevice gpu = new(
            Vendor: GpuVendor.Nvidia,
            Name: "GTX 1080",
            VramMb: 8192,
            MaxEncoderSessions: 12,
            SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265]
        );
        HardwareCapabilities caps = new(Gpus: [gpu], CpuCores: 8);
        caps.SupportsHardwareEncoding(codec: VideoCodecType.Av1).Should().BeFalse();
        caps.SupportsHardwareEncoding(codec: VideoCodecType.Vp9).Should().BeFalse();
    }

    [Fact]
    public void GetGpuForCodec_ReturnsCorrectGpu()
    {
        GpuDevice nvidia = new(
            Vendor: GpuVendor.Nvidia,
            Name: "RTX 4090",
            VramMb: 24576,
            MaxEncoderSessions: 12,
            SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );
        GpuDevice intel = new(
            Vendor: GpuVendor.Intel,
            Name: "Arc A770",
            VramMb: 16384,
            MaxEncoderSessions: int.MaxValue,
            SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1, VideoCodecType.Vp9]
        );
        HardwareCapabilities caps = new(Gpus: [nvidia, intel], CpuCores: 16);
        GpuDevice? vp9Gpu = caps.GetGpuForCodec(codec: VideoCodecType.Vp9);
        vp9Gpu.Should().NotBeNull();
        vp9Gpu!.Vendor.Should().Be(expected: GpuVendor.Intel);
    }

    [Fact]
    public void GetGpuForCodec_ReturnsNull_WhenNoGpuSupports()
    {
        HardwareCapabilities caps = new(Gpus: [], CpuCores: 4);
        GpuDevice? gpu = caps.GetGpuForCodec(codec: VideoCodecType.H264);
        gpu.Should().BeNull();
    }
}
