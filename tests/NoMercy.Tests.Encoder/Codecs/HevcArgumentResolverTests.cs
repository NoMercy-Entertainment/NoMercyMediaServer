namespace NoMercy.Tests.Encoder.Codecs;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;

/// <summary>
/// HEVC-specific resolver behavior. Most of the CRF→flag mapping is
/// identical to H264 (same rate-control modes per vendor), but HEVC has
/// two concerns H264 doesn't:
///
///   1) VideoToolbox requires -tag:v hvc1 or Apple clients play the file
///      as "video/octet-stream" — the vendor-flag must survive through
///      ResolveQuality.
///   2) HEVC 10-bit support is broad across families (libx265, hevc_nvenc,
///      hevc_amf, hevc_qsv, hevc_vaapi all support main10) — only
///      hevc_videotoolbox does NOT, so the PlanStage downgrade path still
///      matters here.
/// </summary>
public class HevcArgumentResolverTests
{
    private static readonly CodecRegistry Registry = new();

    [Fact]
    public void LibX265_Supports10BitAndHdr_ViaMain10()
    {
        EncoderInfo libx265 = Get("libx265");
        libx265.Supports10Bit.Should().BeTrue();
        libx265.SupportsHdr.Should().BeTrue();
        libx265.Profiles.Should().Contain("main10");
        libx265.PixelFormat10Bit.Should().Be("yuv420p10le");
    }

    [Fact]
    public void HevcNvenc_MapsCrfToVbrCq()
    {
        ResolvedCodec nvenc = Resolve("hevc_nvenc", GpuVendor.Nvidia, RateControlMode.Cq);
        Dictionary<string, string> flags = [];

        EncoderArgumentResolver.ResolveQuality(28, nvenc, flags);

        flags["-rc"].Should().Be("vbr");
        flags["-cq"].Should().Be("28", "hevc default CRF is 28, not 23 like H264");
    }

    [Fact]
    public void HevcAmf_CarriesUsageTranscoding()
    {
        // AMF REQUIRES -usage transcoding or the encoder picks ultra-low-latency
        // mode by default → pixellated output at any reasonable bitrate.
        EncoderInfo amf = Get("hevc_amf");
        amf.VendorSpecificFlags.Should().ContainKey("-usage");
        amf.VendorSpecificFlags["-usage"].Should().Be("transcoding");
    }

    [Fact]
    public void HevcQsv_QualityStartsAt1_NotZero()
    {
        // Intel QSV's HEVC encoder rejects -global_quality 0 (interpreted as
        // "default"). Minimum is 1. Getting this wrong means silent fallback.
        EncoderInfo qsv = Get("hevc_qsv");
        qsv.QualityRange.Min.Should().Be(1);
    }

    [Fact]
    public void HevcVaapi_HasNoPresetConcept()
    {
        // VAAPI doesn't accept -preset. ResolvePreset must return null.
        EncoderInfo vaapi = Get("hevc_vaapi");
        vaapi.Presets.Should().BeEmpty();
        EncoderArgumentResolver.ResolvePreset("medium", vaapi).Should().BeNull();
    }

    [Fact]
    public void HevcVideoToolbox_UsesNumericProfiles()
    {
        // Apple's HEVC encoder doesn't accept "main"/"main10" — it takes
        // integer profile IDs. "1" = Main, "2" = Main10. Profile mapping from
        // a library-style profile string must fall back to the first
        // declared profile, which is "1" (= Main).
        EncoderInfo vt = Get("hevc_videotoolbox");
        vt.Profiles.Should().BeEquivalentTo(["1", "2"]);
        EncoderArgumentResolver.ResolveProfile("main", vt).Should().Be("1");
    }

    [Fact]
    public void HevcVideoToolbox_EmitsHvc1Tag()
    {
        EncoderInfo vt = Get("hevc_videotoolbox");
        vt.VendorSpecificFlags["-tag:v"]
            .Should()
            .Be("hvc1", "Apple clients reject HEVC MP4 without hvc1 branding");
    }

    [Fact]
    public void HevcVideoToolbox_DoesNotSupport10Bit()
    {
        // hevc_videotoolbox has Supports10Bit=false so the PlanStage downgrade
        // guard kicks in for TenBit=true profiles on Apple Silicon / Intel Mac.
        EncoderInfo vt = Get("hevc_videotoolbox");
        vt.Supports10Bit.Should().BeFalse();
    }

    private static EncoderInfo Get(string ffmpegName)
    {
        foreach ((VideoCodecType c, EncoderInfo encoder) in Registry.EnumerateVideoEncoders())
        {
            if (c == VideoCodecType.H265 && encoder.FfmpegName == ffmpegName)
                return encoder;
        }
        throw new InvalidOperationException($"HEVC encoder {ffmpegName} not registered");
    }

    private static ResolvedCodec Resolve(
        string ffmpegName,
        GpuVendor? vendor,
        RateControlMode defaultRateControl
    )
    {
        EncoderInfo encoder = Get(ffmpegName);
        GpuDevice? device = vendor is null
            ? null
            : new GpuDevice(
                Vendor: vendor.Value,
                Name: $"Test {vendor.Value}",
                VramMb: 16_384,
                MaxEncoderSessions: 12,
                SupportedCodecs: [VideoCodecType.H265]
            );
        return new(ffmpegName, encoder, device, defaultRateControl);
    }
}
