namespace NoMercy.Tests.Encoder.V3.Codecs;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Codecs.Definitions;
using NoMercy.Encoder.V3.Hardware;

public class H264DefinitionTests
{
    private readonly H264Definition _definition = new();

    [Fact]
    public void CodecType_IsH264()
    {
        _definition.CodecType.Should().Be(VideoCodecType.H264);
    }

    [Fact]
    public void Has_Exactly6_Encoders()
    {
        _definition.Encoders.Should().HaveCount(6);
    }

    [Fact]
    public void SoftwareEncoder_IsLibx264()
    {
        EncoderInfo sw = _definition.Encoders.Single(e => e.FfmpegName == "libx264");
        sw.RequiredVendor.Should().BeNull();
        sw.Presets.Should().Contain("ultrafast");
        sw.Presets.Should().Contain("veryslow");
        sw.Presets.Should().Contain("placebo");
        sw.Presets.Should().HaveCount(10);
        sw.Profiles.Should().Contain("baseline");
        sw.Profiles.Should().Contain("main");
        sw.Profiles.Should().Contain("high");
        sw.Profiles.Should().Contain("high10");
        sw.Profiles.Should().Contain("high422");
        sw.Profiles.Should().Contain("high444p");
        sw.QualityRange.Min.Should().Be(0);
        sw.QualityRange.Max.Should().Be(51);
        sw.QualityRange.Default.Should().Be(23);
        sw.SupportedRateControl.Should().Contain(RateControlMode.Crf);
        sw.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Nvenc_HasCorrectPresets()
    {
        EncoderInfo nvenc = _definition.Encoders.Single(e => e.FfmpegName == "h264_nvenc");
        nvenc.RequiredVendor.Should().Be(GpuVendor.Nvidia);
        nvenc.Presets.Should().BeEquivalentTo(["p1", "p2", "p3", "p4", "p5", "p6", "p7"]);
        nvenc.Profiles.Should().Contain("baseline");
        nvenc.Profiles.Should().Contain("main");
        nvenc.Profiles.Should().Contain("high");
        nvenc.QualityRange.Min.Should().Be(0);
        nvenc.QualityRange.Max.Should().Be(51);
        nvenc.MaxConcurrentSessions.Should().Be(12);
        nvenc.SupportedRateControl.Should().Contain(RateControlMode.Cq);
        nvenc.SupportedRateControl.Should().Contain(RateControlMode.Vbr);
        nvenc.Supports10Bit.Should().BeFalse();
    }

    [Fact]
    public void Amf_HasCorrectFields()
    {
        EncoderInfo amf = _definition.Encoders.Single(e => e.FfmpegName == "h264_amf");
        amf.RequiredVendor.Should().Be(GpuVendor.Amd);
        amf.Presets.Should().BeEquivalentTo(["speed", "balanced", "quality"]);
        amf.Profiles.Should().Contain("constrained_baseline");
        amf.Profiles.Should().Contain("constrained_high");
        amf.MaxConcurrentSessions.Should().Be(int.MaxValue);
        amf.Supports10Bit.Should().BeFalse();
    }

    [Fact]
    public void Qsv_HasCorrectPresets()
    {
        EncoderInfo qsv = _definition.Encoders.Single(e => e.FfmpegName == "h264_qsv");
        qsv.RequiredVendor.Should().Be(GpuVendor.Intel);
        qsv.Presets.Should()
            .BeEquivalentTo(["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"]);
        qsv.Profiles.Should().BeEquivalentTo(["baseline", "main", "high"]);
        qsv.QualityRange.Min.Should().Be(1);
        qsv.QualityRange.Max.Should().Be(51);
        qsv.SupportedRateControl.Should().Contain(RateControlMode.Icq);
        qsv.MaxConcurrentSessions.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Vaapi_HasNoPresets()
    {
        EncoderInfo vaapi = _definition.Encoders.Single(e => e.FfmpegName == "h264_vaapi");
        vaapi.RequiredVendor.Should().Be(GpuVendor.Intel);
        vaapi.Presets.Should().BeEmpty();
        vaapi.Profiles.Should().BeEquivalentTo(["constrained_baseline", "main", "high"]);
    }

    [Fact]
    public void VideoToolbox_HasNumericProfiles()
    {
        EncoderInfo vtb = _definition.Encoders.Single(e => e.FfmpegName == "h264_videotoolbox");
        vtb.RequiredVendor.Should().Be(GpuVendor.Apple);
        vtb.Presets.Should().BeEmpty();
        vtb.Profiles.Should().BeEquivalentTo(["66", "77", "100"]);
        vtb.QualityRange.Min.Should().Be(0);
        vtb.QualityRange.Max.Should().Be(100);
        vtb.SupportedRateControl.Should().Contain(RateControlMode.QualityLevel);
        vtb.VendorSpecificFlags.Should().BeEmpty();
    }

    [Theory]
    [InlineData("vp9_nvenc")]
    [InlineData("vp9_amf")]
    [InlineData("vp9_videotoolbox")]
    [InlineData("av1_videotoolbox")]
    public void PhantomCodecs_DoNotExist(string phantomName)
    {
        _definition.Encoders.Should().NotContain(e => e.FfmpegName == phantomName);
    }
}
