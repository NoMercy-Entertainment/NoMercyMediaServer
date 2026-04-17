namespace NoMercy.Tests.Encoder.Codecs;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Profiles;

/// <summary>
/// Every H264 encoder family uses different flags for the same profile-level
/// CRF value. Getting the mapping wrong means silently-wrong bitrate on one
/// vendor while the others look fine — the kind of regression that only shows
/// up after a user re-encodes their whole library on new hardware. These
/// tests pin the mapping down per family.
/// </summary>
public class EncoderArgumentResolverTests
{
    private static readonly CodecRegistry Registry = new();

    // ──────────────────────────────────────────────────────────────────────────
    // ResolveQuality — CRF → vendor-specific flag
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolveQuality_Software_EmitsCrfDirectly()
    {
        ResolvedCodec resolved = ResolveH264("libx264", null, RateControlMode.Crf);
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(22, resolved, flags);

        crf.Should().Be(22, "libx264 accepts -crf directly");
        flags.Should().NotContainKey("-cq");
        flags.Should().NotContainKey("-qp");
        flags.Should().NotContainKey("-global_quality");
    }

    [Fact]
    public void ResolveQuality_Nvenc_MapsToVbrCq()
    {
        ResolvedCodec resolved = ResolveH264("h264_nvenc", GpuVendor.Nvidia, RateControlMode.Cq);
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(22, resolved, flags);

        crf.Should().Be(0, "NVENC doesn't accept -crf");
        flags["-rc"].Should().Be("vbr");
        flags["-cq"].Should().Be("22");
    }

    [Fact]
    public void ResolveQuality_Qsv_MapsToGlobalQuality()
    {
        ResolvedCodec resolved = ResolveH264("h264_qsv", GpuVendor.Intel, RateControlMode.Icq);
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(22, resolved, flags);

        crf.Should().Be(0);
        flags["-global_quality"].Should().Be("22", "Intel QSV uses ICQ via -global_quality");
        flags.Should().NotContainKey("-rc");
    }

    [Fact]
    public void ResolveQuality_Amf_MapsToCqpQp()
    {
        ResolvedCodec resolved = ResolveH264("h264_amf", GpuVendor.Amd, RateControlMode.Cqp);
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(22, resolved, flags);

        crf.Should().Be(0);
        flags["-rc"].Should().Be("cqp");
        flags["-qp"].Should().Be("22");
    }

    [Fact]
    public void ResolveQuality_Vaapi_MapsToCqpQp()
    {
        ResolvedCodec resolved = ResolveH264("h264_vaapi", GpuVendor.Intel, RateControlMode.Cqp);
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(22, resolved, flags);

        crf.Should().Be(0);
        flags["-rc"].Should().Be("cqp");
        flags["-qp"].Should().Be("22");
    }

    [Fact]
    public void ResolveQuality_VideoToolbox_MapsToQ_ScaledIntoPercentRange()
    {
        // VideoToolbox quality is 0-100 (higher = better). The profile's CRF
        // is H264 reference 0-51. Scaled: round(50/51*100)=98.
        // Without scaling, passing 50 raw would be mid-range quality instead
        // of near-lossless as the profile intended.
        ResolvedCodec resolved = ResolveH264(
            "h264_videotoolbox",
            GpuVendor.Apple,
            RateControlMode.QualityLevel
        );
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(50, resolved, flags);

        crf.Should().Be(0);
        flags["-q:v"]
            .Should()
            .Be("98", "50/51 of the 0-100 VideoToolbox range = 98 (near-max quality)");
        flags.Should().NotContainKey("-rc");
    }

    [Fact]
    public void ResolveQuality_ZeroCrf_IsNoOp()
    {
        // 0 = "not configured by profile" — resolver must not emit anything.
        ResolvedCodec nvenc = ResolveH264("h264_nvenc", GpuVendor.Nvidia, RateControlMode.Cq);
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(0, nvenc, flags);

        crf.Should().Be(0);
        flags.Should().BeEmpty();
    }

    [Fact]
    public void ResolveQuality_PreservesPreExistingVendorFlags()
    {
        // PlanStage seeds extraFlags with encoder.VendorSpecificFlags BEFORE calling
        // ResolveQuality. The resolver must add to the dict, not replace it —
        // otherwise HEVC videotoolbox loses its required -tag:v hvc1 and Apple
        // clients stop decoding, and AMF loses -usage transcoding and switches
        // to the wrong ratecontrol profile.
        ResolvedCodec amf = ResolveH264("h264_amf", GpuVendor.Amd, RateControlMode.Cqp);
        Dictionary<string, string> flags = new(amf.EncoderInfo.VendorSpecificFlags);
        flags["-usage"].Should().Be("transcoding");

        EncoderArgumentResolver.ResolveQuality(25, amf, flags);

        flags["-usage"].Should().Be("transcoding", "vendor flags must survive ResolveQuality");
        flags["-rc"].Should().Be("cqp");
        flags["-qp"].Should().Be("25");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HEVC-specific — videotoolbox -tag:v hvc1 is mandatory for Apple playback
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HevcVideoToolbox_HasMandatoryHvc1Tag()
    {
        EncoderInfo vt = GetEncoder(VideoCodecType.H265, "hevc_videotoolbox");
        vt.VendorSpecificFlags.Should()
            .ContainKey("-tag:v", "HEVC in MP4 without hvc1 tag plays as video/octet on Apple");
        vt.VendorSpecificFlags["-tag:v"].Should().Be("hvc1");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ResolvePreset — per encoder family
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolvePreset_Software_UsesProfilePresetWhenSupported()
    {
        EncoderInfo libx264 = GetH264Encoder("libx264");
        string? preset = EncoderArgumentResolver.ResolvePreset("slow", libx264);
        preset.Should().Be("slow");
    }

    [Fact]
    public void ResolvePreset_Nvenc_UnsupportedPresetFallsBackToMiddle()
    {
        EncoderInfo nvenc = GetH264Encoder("h264_nvenc");
        // "slow" isn't in NVENC's p1..p7 set — resolver must substitute.
        string? preset = EncoderArgumentResolver.ResolvePreset("slow", nvenc);
        preset.Should().Be("p4", "NVENC's 7-preset middle is p4");
    }

    [Fact]
    public void ResolvePreset_Vaapi_ReturnsNull()
    {
        // VAAPI has no preset concept — the driver doesn't accept -preset.
        EncoderInfo vaapi = GetH264Encoder("h264_vaapi");
        vaapi.Presets.Should().BeEmpty();

        string? preset = EncoderArgumentResolver.ResolvePreset("medium", vaapi);
        preset.Should().BeNull();
    }

    [Fact]
    public void ResolvePreset_VideoToolbox_ReturnsNull()
    {
        EncoderInfo vt = GetH264Encoder("h264_videotoolbox");
        vt.Presets.Should().BeEmpty();
        EncoderArgumentResolver.ResolvePreset("medium", vt).Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ResolveProfile — fallback behavior
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolveProfile_UnsupportedProfile_FallsBackToFirst()
    {
        EncoderInfo nvenc = GetH264Encoder("h264_nvenc");
        // high10 isn't in NVENC's profile set — driver rejects it, so we fall back.
        string? profile = EncoderArgumentResolver.ResolveProfile("high10", nvenc);
        profile.Should().Be("baseline", "NVENC's first-declared profile is the safe fallback");
    }

    [Fact]
    public void ResolveProfile_SupportedProfile_PassedThrough()
    {
        EncoderInfo libx264 = GetH264Encoder("libx264");
        EncoderArgumentResolver.ResolveProfile("high10", libx264).Should().Be("high10");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ResolveDimensions — no-upscale, even height
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolveDimensions_DoesNotUpscaleBeyondSource()
    {
        VideoOutput profile = MakeVideoOutput(width: 1920);
        (int w, int h) = EncoderArgumentResolver.ResolveDimensions(profile, 1280, 720);
        w.Should().Be(1280, "upscaling is off by design");
    }

    [Fact]
    public void ResolveDimensions_ForcesEvenHeight()
    {
        VideoOutput profile = MakeVideoOutput(width: 853); // 853x480 → 853*(480/1920) = 213.25
        (int w, int h) = EncoderArgumentResolver.ResolveDimensions(profile, 1920, 480);
        (h % 2).Should().Be(0, "h.264/h.265 require even dimensions");
    }

    [Fact]
    public void ResolveDimensions_UsesExplicitHeightWhenProvided()
    {
        VideoOutput profile = MakeVideoOutput(width: 1280, height: 720);
        (int w, int h) = EncoderArgumentResolver.ResolveDimensions(profile, 1920, 1080);
        w.Should().Be(1280);
        h.Should().Be(720);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static EncoderInfo GetH264Encoder(string ffmpegName) =>
        GetEncoder(VideoCodecType.H264, ffmpegName);

    private static EncoderInfo GetEncoder(VideoCodecType codec, string ffmpegName)
    {
        foreach ((VideoCodecType c, EncoderInfo encoder) in Registry.EnumerateVideoEncoders())
        {
            if (c == codec && encoder.FfmpegName == ffmpegName)
                return encoder;
        }
        throw new InvalidOperationException($"Encoder {ffmpegName} not registered for {codec}");
    }

    private static ResolvedCodec ResolveH264(
        string ffmpegName,
        GpuVendor? vendor,
        RateControlMode defaultRateControl
    )
    {
        EncoderInfo encoder = GetH264Encoder(ffmpegName);
        GpuDevice? device = vendor is null
            ? null
            : new GpuDevice(
                Vendor: vendor.Value,
                Name: $"Test {vendor.Value}",
                VramMb: 16_384,
                MaxEncoderSessions: 12,
                SupportedCodecs: [VideoCodecType.H264]
            );
        return new ResolvedCodec(ffmpegName, encoder, device, defaultRateControl);
    }

    private static VideoOutput MakeVideoOutput(int width, int? height = null) =>
        new(
            Codec: VideoCodecType.H264,
            Width: width,
            Height: height,
            BitrateKbps: 4000,
            Crf: 22,
            Preset: "medium",
            Profile: "high",
            Level: null,
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
}
