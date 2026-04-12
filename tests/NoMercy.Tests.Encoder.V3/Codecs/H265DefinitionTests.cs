namespace NoMercy.Tests.Encoder.V3.Codecs;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Codecs.Definitions;
using NoMercy.Encoder.V3.Hardware;

public class H265DefinitionTests
{
    private readonly H265Definition _definition = new();

    [Fact]
    public void CodecType_IsH265()
    {
        _definition.CodecType.Should().Be(VideoCodecType.H265);
    }

    [Fact]
    public void Has_Exactly6_Encoders()
    {
        _definition.Encoders.Should().HaveCount(6);
    }

    [Fact]
    public void Libx265_HasCorrectFields()
    {
        EncoderInfo sw = _definition.Encoders.Single(e => e.FfmpegName == "libx265");

        sw.RequiredVendor.Should().BeNull();
        sw.Presets.Should().HaveCount(10);
        sw.Presets.Should().Contain("ultrafast");
        sw.Presets.Should().Contain("veryslow");
        sw.Presets.Should().Contain("placebo");
        sw.Profiles.Should().Contain("main");
        sw.Profiles.Should().Contain("main10");
        sw.Profiles.Should().Contain("main12");
        sw.Profiles.Should().Contain("main422-10");
        sw.Profiles.Should().Contain("main444-10");
        sw.QualityRange.Min.Should().Be(0);
        sw.QualityRange.Max.Should().Be(51);
        sw.QualityRange.Default.Should().Be(28);
        sw.SupportedRateControl.Should().Contain(RateControlMode.Crf);
        sw.Supports10Bit.Should().BeTrue();
        sw.SupportsHdr.Should().BeTrue();
        sw.PixelFormat10Bit.Should().Be("yuv420p10le");
        sw.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }

    [Fact]
    public void HevcNvenc_HasCorrectFields()
    {
        EncoderInfo nvenc = _definition.Encoders.Single(e => e.FfmpegName == "hevc_nvenc");

        nvenc.RequiredVendor.Should().Be(GpuVendor.Nvidia);
        nvenc.Presets.Should().BeEquivalentTo(["p1", "p2", "p3", "p4", "p5", "p6", "p7"]);
        nvenc.Profiles.Should().Contain("main");
        nvenc.Profiles.Should().Contain("main10");
        nvenc.Profiles.Should().Contain("rext");
        nvenc.QualityRange.Min.Should().Be(0);
        nvenc.QualityRange.Max.Should().Be(51);
        nvenc.SupportedRateControl.Should().Contain(RateControlMode.Cq);
        nvenc.Supports10Bit.Should().BeTrue();
        nvenc.SupportsHdr.Should().BeTrue();
        nvenc.MaxConcurrentSessions.Should().Be(12);
    }

    [Fact]
    public void HevcAmf_HasCorrectFields()
    {
        EncoderInfo amf = _definition.Encoders.Single(e => e.FfmpegName == "hevc_amf");

        amf.RequiredVendor.Should().Be(GpuVendor.Amd);
        amf.Presets.Should().BeEquivalentTo(["speed", "balanced", "quality"]);
        amf.Profiles.Should().Contain("main");
        amf.Profiles.Should().Contain("main10");
        amf.QualityRange.Min.Should().Be(0);
        amf.QualityRange.Max.Should().Be(51);
        amf.Supports10Bit.Should().BeTrue();
        amf.SupportsHdr.Should().BeTrue();
        amf.MaxConcurrentSessions.Should().Be(int.MaxValue);
        amf.VendorSpecificFlags.Should().ContainKey("-usage");
    }

    [Fact]
    public void HevcQsv_HasCorrectFields()
    {
        EncoderInfo qsv = _definition.Encoders.Single(e => e.FfmpegName == "hevc_qsv");

        qsv.RequiredVendor.Should().Be(GpuVendor.Intel);
        qsv.Presets.Should()
            .BeEquivalentTo(["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"]);
        qsv.Profiles.Should().Contain("main");
        qsv.Profiles.Should().Contain("main10");
        qsv.Profiles.Should().Contain("mainsp");
        qsv.Profiles.Should().Contain("rext");
        qsv.Profiles.Should().Contain("scc");
        qsv.QualityRange.Min.Should().Be(1);
        qsv.QualityRange.Max.Should().Be(51);
        qsv.SupportedRateControl.Should().Contain(RateControlMode.Icq);
        qsv.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }

    [Fact]
    public void HevcVaapi_HasCorrectFields()
    {
        EncoderInfo vaapi = _definition.Encoders.Single(e => e.FfmpegName == "hevc_vaapi");

        vaapi.RequiredVendor.Should().Be(GpuVendor.Intel);
        vaapi.Presets.Should().BeEmpty();
        vaapi.Profiles.Should().Contain("main");
        vaapi.Profiles.Should().Contain("main10");
        vaapi.Supports10Bit.Should().BeTrue();
        vaapi.SupportsHdr.Should().BeTrue();
    }

    [Fact]
    public void HevcVideoToolbox_HasCorrectFields()
    {
        EncoderInfo vtb = _definition.Encoders.Single(e => e.FfmpegName == "hevc_videotoolbox");

        vtb.RequiredVendor.Should().Be(GpuVendor.Apple);
        vtb.Presets.Should().BeEmpty();
        // HEVC VTB profiles are numeric: "1" = Main, "2" = Main10
        vtb.Profiles.Should().BeEquivalentTo(["1", "2"]);
        vtb.QualityRange.Min.Should().Be(0);
        vtb.QualityRange.Max.Should().Be(100);
        vtb.SupportedRateControl.Should().Contain(RateControlMode.QualityLevel);
        // hevc_videotoolbox REQUIRES -tag:v hvc1
        vtb.VendorSpecificFlags.Should().ContainKey("-tag:v");
        vtb.VendorSpecificFlags["-tag:v"].Should().Be("hvc1");
    }
}
