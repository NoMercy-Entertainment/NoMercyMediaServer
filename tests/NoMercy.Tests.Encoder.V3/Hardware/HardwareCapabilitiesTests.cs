namespace NoMercy.Tests.Encoder.V3.Hardware;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Hardware;

public class HardwareCapabilitiesTests
{
    [Fact]
    public void EmptyCapabilities_HasNoGpuEncoders()
    {
        HardwareCapabilities caps = new(Gpus: [], CpuCores: 4);
        caps.Gpus.Should().BeEmpty();
        caps.HasGpu.Should().BeFalse();
        caps.CpuCores.Should().Be(4);
    }

    [Fact]
    public void WithNvidiaGpu_HasGpuIsTrue()
    {
        GpuDevice gpu = new(
            GpuVendor.Nvidia,
            "GeForce RTX 4090",
            24576,
            12,
            [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );
        HardwareCapabilities caps = new(Gpus: [gpu], CpuCores: 16);
        caps.HasGpu.Should().BeTrue();
        caps.Gpus.Should().HaveCount(1);
        caps.Gpus[0].Vendor.Should().Be(GpuVendor.Nvidia);
    }

    [Fact]
    public void SupportsCodecOnGpu_True_WhenGpuHasCodec()
    {
        GpuDevice gpu = new(
            GpuVendor.Nvidia,
            "RTX 4090",
            24576,
            12,
            [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );
        HardwareCapabilities caps = new(Gpus: [gpu], CpuCores: 16);
        caps.SupportsHardwareEncoding(VideoCodecType.H264).Should().BeTrue();
        caps.SupportsHardwareEncoding(VideoCodecType.Av1).Should().BeTrue();
    }

    [Fact]
    public void SupportsCodecOnGpu_False_WhenNoGpuHasCodec()
    {
        GpuDevice gpu = new(
            GpuVendor.Nvidia,
            "GTX 1080",
            8192,
            12,
            [VideoCodecType.H264, VideoCodecType.H265]
        );
        HardwareCapabilities caps = new(Gpus: [gpu], CpuCores: 8);
        caps.SupportsHardwareEncoding(VideoCodecType.Av1).Should().BeFalse();
        caps.SupportsHardwareEncoding(VideoCodecType.Vp9).Should().BeFalse();
    }

    [Fact]
    public void GetGpuForCodec_ReturnsCorrectGpu()
    {
        GpuDevice nvidia = new(
            GpuVendor.Nvidia,
            "RTX 4090",
            24576,
            12,
            [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );
        GpuDevice intel = new(
            GpuVendor.Intel,
            "Arc A770",
            16384,
            int.MaxValue,
            [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1, VideoCodecType.Vp9]
        );
        HardwareCapabilities caps = new(Gpus: [nvidia, intel], CpuCores: 16);
        GpuDevice? vp9Gpu = caps.GetGpuForCodec(VideoCodecType.Vp9);
        vp9Gpu.Should().NotBeNull();
        vp9Gpu!.Vendor.Should().Be(GpuVendor.Intel);
    }

    [Fact]
    public void GetGpuForCodec_ReturnsNull_WhenNoGpuSupports()
    {
        HardwareCapabilities caps = new(Gpus: [], CpuCores: 4);
        GpuDevice? gpu = caps.GetGpuForCodec(VideoCodecType.H264);
        gpu.Should().BeNull();
    }
}
