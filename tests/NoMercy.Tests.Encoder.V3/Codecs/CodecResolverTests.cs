namespace NoMercy.Tests.Encoder.V3.Codecs;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Hardware;

public class CodecResolverTests
{
    private readonly CodecRegistry _registry = new();

    [Fact]
    public void PreferHardware_WithNvidia_SelectsNvenc()
    {
        IHardwareCapabilities caps = MakeCaps(
            GpuVendor.Nvidia,
            [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );
        CodecResolver resolver = new(_registry);
        ResolvedCodec resolved = resolver.Resolve(VideoCodecType.H264, caps);
        resolved.FfmpegEncoderName.Should().Be("h264_nvenc");
        resolved.Device.Should().NotBeNull();
        resolved.Device!.Vendor.Should().Be(GpuVendor.Nvidia);
    }

    [Fact]
    public void PreferHardware_WithAmd_SelectsAmf()
    {
        IHardwareCapabilities caps = MakeCaps(
            GpuVendor.Amd,
            [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );
        CodecResolver resolver = new(_registry);
        ResolvedCodec resolved = resolver.Resolve(VideoCodecType.H265, caps);
        resolved.FfmpegEncoderName.Should().Be("hevc_amf");
    }

    [Fact]
    public void PreferHardware_NoGpu_FallsBackToSoftware()
    {
        IHardwareCapabilities caps = MakeCaps(null, []);
        CodecResolver resolver = new(_registry);
        ResolvedCodec resolved = resolver.Resolve(VideoCodecType.H264, caps);
        resolved.FfmpegEncoderName.Should().Be("libx264");
        resolved.Device.Should().BeNull();
    }

    [Fact]
    public void ForceSoftware_WithNvidia_StillSelectsSoftware()
    {
        IHardwareCapabilities caps = MakeCaps(GpuVendor.Nvidia, [VideoCodecType.H264]);
        CodecResolver resolver = new(_registry);
        ResolvedCodec resolved = resolver.Resolve(
            VideoCodecType.H264,
            caps,
            EncoderPreference.ForceSoftware
        );
        resolved.FfmpegEncoderName.Should().Be("libx264");
        resolved.Device.Should().BeNull();
    }

    [Fact]
    public void ForceHardware_NoGpu_Throws()
    {
        IHardwareCapabilities caps = MakeCaps(null, []);
        CodecResolver resolver = new(_registry);
        Action act = () =>
            resolver.Resolve(VideoCodecType.H264, caps, EncoderPreference.ForceHardware);
        act.Should().Throw<InvalidOperationException>().WithMessage("*hardware*");
    }

    [Fact]
    public void Vp9_WithNvidia_FallsBackToSoftware()
    {
        IHardwareCapabilities caps = MakeCaps(
            GpuVendor.Nvidia,
            [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );
        CodecResolver resolver = new(_registry);
        ResolvedCodec resolved = resolver.Resolve(VideoCodecType.Vp9, caps);
        resolved.FfmpegEncoderName.Should().Be("libvpx-vp9");
    }

    [Fact]
    public void Vp9_WithIntel_SelectsQsv()
    {
        IHardwareCapabilities caps = MakeCaps(
            GpuVendor.Intel,
            [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1, VideoCodecType.Vp9]
        );
        CodecResolver resolver = new(_registry);
        ResolvedCodec resolved = resolver.Resolve(VideoCodecType.Vp9, caps);
        resolved.FfmpegEncoderName.Should().Be("vp9_qsv");
    }

    [Fact]
    public void Av1_PrefersSvtAv1_ForSoftware()
    {
        IHardwareCapabilities caps = MakeCaps(null, []);
        CodecResolver resolver = new(_registry);
        ResolvedCodec resolved = resolver.Resolve(VideoCodecType.Av1, caps);
        resolved.FfmpegEncoderName.Should().Be("libsvtav1");
    }

    [Fact]
    public void DefaultRateControl_MatchesEncoderType()
    {
        IHardwareCapabilities noCaps = MakeCaps(null, []);
        CodecResolver resolver = new(_registry);
        ResolvedCodec sw = resolver.Resolve(VideoCodecType.H264, noCaps);
        sw.DefaultRateControl.Should().Be(RateControlMode.Crf);

        IHardwareCapabilities nvidiaCaps = MakeCaps(GpuVendor.Nvidia, [VideoCodecType.H264]);
        ResolvedCodec hw = resolver.Resolve(VideoCodecType.H264, nvidiaCaps);
        hw.DefaultRateControl.Should().Be(RateControlMode.Cq);
    }

    private static IHardwareCapabilities MakeCaps(GpuVendor? vendor, VideoCodecType[] codecs)
    {
        List<GpuDevice> gpus = [];
        if (vendor.HasValue)
            gpus.Add(new GpuDevice(vendor.Value, "Test GPU", 8192, 12, codecs));
        return new HardwareCapabilities(gpus, 8);
    }
}
