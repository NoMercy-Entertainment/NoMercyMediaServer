namespace NoMercy.Tests.Encoder.V3.Codecs;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Codecs.Definitions;
using NoMercy.Encoder.V3.Hardware;

public class Av1DefinitionTests
{
    private readonly Av1Definition _definition = new();

    [Fact]
    public void CodecType_IsAv1()
    {
        _definition.CodecType.Should().Be(VideoCodecType.Av1);
    }

    [Fact]
    public void Has_Exactly7_Encoders()
    {
        _definition.Encoders.Should().HaveCount(7);
    }

    [Fact]
    public void NoAppleEncoder_Exists()
    {
        // Apple decodes AV1 but does NOT encode it
        _definition.Encoders.Should().NotContain(e => e.FfmpegName == "av1_videotoolbox");
        _definition.Encoders.Should().NotContain(e => e.RequiredVendor == GpuVendor.Apple);
    }

    [Fact]
    public void Libsvtav1_HasCorrectFields()
    {
        EncoderInfo sw = _definition.Encoders.Single(e => e.FfmpegName == "libsvtav1");

        sw.RequiredVendor.Should().BeNull();
        // 14 presets: "0" through "13"
        sw.Presets.Should().HaveCount(14);
        sw.Presets.Should().Contain("0");
        sw.Presets.Should().Contain("13");
        sw.QualityRange.Min.Should().Be(0);
        sw.QualityRange.Max.Should().Be(63);
        sw.QualityRange.Default.Should().Be(35);
        sw.SupportedRateControl.Should().Contain(RateControlMode.Crf);
        sw.Supports10Bit.Should().BeTrue();
        sw.SupportsHdr.Should().BeTrue();
        sw.PixelFormat10Bit.Should().Be("yuv420p10le");
        sw.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }

    [Fact]
    public void LibaomAv1_HasCorrectFields()
    {
        EncoderInfo aom = _definition.Encoders.Single(e => e.FfmpegName == "libaom-av1");

        aom.RequiredVendor.Should().BeNull();
        // 9 presets: "0" through "8" (cpu-used values)
        aom.Presets.Should().HaveCount(9);
        aom.Presets.Should().Contain("0");
        aom.Presets.Should().Contain("8");
        aom.QualityRange.Min.Should().Be(0);
        aom.QualityRange.Max.Should().Be(63);
        aom.SupportedRateControl.Should().Contain(RateControlMode.Crf);
        aom.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Librav1e_HasCorrectFields()
    {
        EncoderInfo rav1e = _definition.Encoders.Single(e => e.FfmpegName == "librav1e");

        rav1e.RequiredVendor.Should().BeNull();
        // 11 presets: "0" through "10"
        rav1e.Presets.Should().HaveCount(11);
        rav1e.Presets.Should().Contain("0");
        rav1e.Presets.Should().Contain("10");
        // librav1e QP range is 0-255
        rav1e.QualityRange.Min.Should().Be(0);
        rav1e.QualityRange.Max.Should().Be(255);
        rav1e.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Av1Nvenc_HasCorrectFields()
    {
        EncoderInfo nvenc = _definition.Encoders.Single(e => e.FfmpegName == "av1_nvenc");

        nvenc.RequiredVendor.Should().Be(GpuVendor.Nvidia);
        nvenc.Presets.Should().BeEquivalentTo(["p1", "p2", "p3", "p4", "p5", "p6", "p7"]);
        // AV1 NVENC: main profile only
        nvenc.Profiles.Should().ContainSingle(p => p == "main");
        nvenc.QualityRange.Min.Should().Be(0);
        nvenc.QualityRange.Max.Should().Be(51);
        nvenc.SupportedRateControl.Should().Contain(RateControlMode.Cq);
        nvenc.Supports10Bit.Should().BeTrue();
        nvenc.SupportsHdr.Should().BeTrue();
        nvenc.MaxConcurrentSessions.Should().Be(12);
    }

    [Fact]
    public void Av1Amf_HasCorrectFields()
    {
        EncoderInfo amf = _definition.Encoders.Single(e => e.FfmpegName == "av1_amf");

        amf.RequiredVendor.Should().Be(GpuVendor.Amd);
        amf.Presets.Should().HaveCount(4);
        amf.Presets.Should().Contain("speed");
        amf.Presets.Should().Contain("balanced");
        amf.Presets.Should().Contain("quality");
        amf.Presets.Should().Contain("high_quality");
        amf.Profiles.Should().Contain("main");
        // AMD AV1 QP range is 0-255 (NOT 0-51!)
        amf.QualityRange.Min.Should().Be(0);
        amf.QualityRange.Max.Should().Be(255);
        amf.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Av1Qsv_HasCorrectFields()
    {
        EncoderInfo qsv = _definition.Encoders.Single(e => e.FfmpegName == "av1_qsv");

        qsv.RequiredVendor.Should().Be(GpuVendor.Intel);
        qsv.Presets.Should()
            .BeEquivalentTo(["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"]);
        qsv.Profiles.Should().Contain("main");
        qsv.QualityRange.Min.Should().Be(1);
        qsv.QualityRange.Max.Should().Be(51);
        qsv.SupportedRateControl.Should().Contain(RateControlMode.Icq);
        qsv.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Av1Vaapi_HasCorrectFields()
    {
        EncoderInfo vaapi = _definition.Encoders.Single(e => e.FfmpegName == "av1_vaapi");

        vaapi.RequiredVendor.Should().Be(GpuVendor.Intel);
        vaapi.Presets.Should().BeEmpty();
        vaapi.Profiles.Should().Contain("main");
        // av1_vaapi QP range is 0-255
        vaapi.QualityRange.Min.Should().Be(0);
        vaapi.QualityRange.Max.Should().Be(255);
        vaapi.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }
}
