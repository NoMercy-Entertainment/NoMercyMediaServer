namespace NoMercy.Tests.Encoder.V3.Codecs;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Codecs.Definitions;
using NoMercy.Encoder.V3.Hardware;

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
        sw.Profiles.Should().Contain("profile0");
        sw.Profiles.Should().Contain("profile1");
        sw.Profiles.Should().Contain("profile2");
        sw.Profiles.Should().Contain("profile3");
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
        vaapi.Profiles.Should().Contain("profile0");
        vaapi.Profiles.Should().Contain("profile1");
        vaapi.Profiles.Should().Contain("profile2");
        vaapi.Profiles.Should().Contain("profile3");
        vaapi.QualityRange.Min.Should().Be(0);
        vaapi.QualityRange.Max.Should().Be(255);
        vaapi.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }
}
