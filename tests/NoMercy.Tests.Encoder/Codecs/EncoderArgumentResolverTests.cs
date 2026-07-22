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
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;
using VideoOutput = NoMercy.Encoder.Profiles.VideoOutput;

namespace NoMercy.Tests.Encoder.Codecs;

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
        ResolvedCodec resolved = ResolveH264(ffmpegName: "libx264", vendor: null, defaultRateControl: RateControlMode.Crf);
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(profileCrf: 22, resolved: resolved, extraFlags: flags);

        crf.Should().Be(expected: 22, because: "libx264 accepts -crf directly");
        flags.Should().NotContainKey(unexpected: "-cq");
        flags.Should().NotContainKey(unexpected: "-qp");
        flags.Should().NotContainKey(unexpected: "-global_quality");
    }

    [Fact]
    public void ResolveQuality_Nvenc_MapsToVbrCq()
    {
        ResolvedCodec resolved = ResolveH264(ffmpegName: "h264_nvenc", vendor: GpuVendor.Nvidia, defaultRateControl: RateControlMode.Cq);
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(profileCrf: 22, resolved: resolved, extraFlags: flags);

        crf.Should().Be(expected: 0, because: "NVENC doesn't accept -crf");
        flags[key: "-rc"].Should().Be(expected: "vbr");
        flags[key: "-cq"].Should().Be(expected: "22");
    }

    [Fact]
    public void ResolveQuality_Qsv_MapsToGlobalQuality()
    {
        ResolvedCodec resolved = ResolveH264(ffmpegName: "h264_qsv", vendor: GpuVendor.Intel, defaultRateControl: RateControlMode.Icq);
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(profileCrf: 22, resolved: resolved, extraFlags: flags);

        crf.Should().Be(expected: 0);
        flags[key: "-global_quality"].Should().Be(expected: "22", because: "Intel QSV uses ICQ via -global_quality");
        flags.Should().NotContainKey(unexpected: "-rc");
    }

    [Fact]
    public void ResolveQuality_Amf_MapsToCqpQp()
    {
        ResolvedCodec resolved = ResolveH264(ffmpegName: "h264_amf", vendor: GpuVendor.Amd, defaultRateControl: RateControlMode.Cqp);
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(profileCrf: 22, resolved: resolved, extraFlags: flags);

        crf.Should().Be(expected: 0);
        flags[key: "-rc"].Should().Be(expected: "cqp");
        flags[key: "-qp"].Should().Be(expected: "22");
    }

    [Fact]
    public void ResolveQuality_Vaapi_MapsToCqpQp()
    {
        ResolvedCodec resolved = ResolveH264(ffmpegName: "h264_vaapi", vendor: GpuVendor.Intel, defaultRateControl: RateControlMode.Cqp);
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(profileCrf: 22, resolved: resolved, extraFlags: flags);

        crf.Should().Be(expected: 0);
        flags[key: "-rc"].Should().Be(expected: "cqp");
        flags[key: "-qp"].Should().Be(expected: "22");
    }

    [Fact]
    public void ResolveQuality_VideoToolbox_MapsToQ_ScaledIntoPercentRange()
    {
        // VideoToolbox quality is 0-100 (higher = better). The profile's CRF
        // is H264 reference 0-51. Scaled: round(50/51*100)=98.
        // Without scaling, passing 50 raw would be mid-range quality instead
        // of near-lossless as the profile intended.
        ResolvedCodec resolved = ResolveH264(
            ffmpegName: "h264_videotoolbox",
            vendor: GpuVendor.Apple,
            defaultRateControl: RateControlMode.QualityLevel
        );
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(profileCrf: 50, resolved: resolved, extraFlags: flags);

        crf.Should().Be(expected: 0);
        flags[key: "-q:v"]
            .Should()
            .Be(expected: "98", because: "50/51 of the 0-100 VideoToolbox range = 98 (near-max quality)");
        flags.Should().NotContainKey(unexpected: "-rc");
    }

    [Fact]
    public void ResolveQuality_ZeroCrf_IsNoOp()
    {
        // 0 = "not configured by profile" — resolver must not emit anything.
        ResolvedCodec nvenc = ResolveH264(ffmpegName: "h264_nvenc", vendor: GpuVendor.Nvidia, defaultRateControl: RateControlMode.Cq);
        Dictionary<string, string> flags = [];

        int crf = EncoderArgumentResolver.ResolveQuality(profileCrf: 0, resolved: nvenc, extraFlags: flags);

        crf.Should().Be(expected: 0);
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
        ResolvedCodec amf = ResolveH264(ffmpegName: "h264_amf", vendor: GpuVendor.Amd, defaultRateControl: RateControlMode.Cqp);
        Dictionary<string, string> flags = new(dictionary: amf.EncoderInfo.VendorSpecificFlags);
        flags[key: "-usage"].Should().Be(expected: "transcoding");

        EncoderArgumentResolver.ResolveQuality(profileCrf: 25, resolved: amf, extraFlags: flags);

        flags[key: "-usage"].Should().Be(expected: "transcoding", because: "vendor flags must survive ResolveQuality");
        flags[key: "-rc"].Should().Be(expected: "cqp");
        flags[key: "-qp"].Should().Be(expected: "25");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HEVC-specific — videotoolbox -tag:v hvc1 is mandatory for Apple playback
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HevcVideoToolbox_HasMandatoryHvc1Tag()
    {
        EncoderInfo vt = GetEncoder(codec: VideoCodecType.H265, ffmpegName: "hevc_videotoolbox");
        vt.VendorSpecificFlags.Should()
            .ContainKey(expected: "-tag:v", because: "HEVC in MP4 without hvc1 tag plays as video/octet on Apple");
        vt.VendorSpecificFlags[key: "-tag:v"].Should().Be(expected: "hvc1");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ResolvePreset — per encoder family
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolvePreset_Software_UsesProfilePresetWhenSupported()
    {
        EncoderInfo libx264 = GetH264Encoder(ffmpegName: "libx264");
        string? preset = EncoderArgumentResolver.ResolvePreset(profilePreset: "slow", encoder: libx264);
        preset.Should().Be(expected: "slow");
    }

    [Fact]
    public void ResolvePreset_Nvenc_UnsupportedPresetFallsBackToMiddle()
    {
        EncoderInfo nvenc = GetH264Encoder(ffmpegName: "h264_nvenc");
        // "slow" isn't in NVENC's p1..p7 set — resolver must substitute.
        string? preset = EncoderArgumentResolver.ResolvePreset(profilePreset: "slow", encoder: nvenc);
        preset.Should().Be(expected: "p4", because: "NVENC's 7-preset middle is p4");
    }

    [Fact]
    public void ResolvePreset_Vaapi_ReturnsNull()
    {
        // VAAPI has no preset concept — the driver doesn't accept -preset.
        EncoderInfo vaapi = GetH264Encoder(ffmpegName: "h264_vaapi");
        vaapi.Presets.Should().BeEmpty();

        string? preset = EncoderArgumentResolver.ResolvePreset(profilePreset: "medium", encoder: vaapi);
        preset.Should().BeNull();
    }

    [Fact]
    public void ResolvePreset_VideoToolbox_ReturnsNull()
    {
        EncoderInfo vt = GetH264Encoder(ffmpegName: "h264_videotoolbox");
        vt.Presets.Should().BeEmpty();
        EncoderArgumentResolver.ResolvePreset(profilePreset: "medium", encoder: vt).Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ResolveProfile — fallback behavior
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolveProfile_UnsupportedProfile_FallsBackToFirst()
    {
        EncoderInfo nvenc = GetH264Encoder(ffmpegName: "h264_nvenc");
        // high10 isn't in NVENC's profile set — driver rejects it, so we fall back.
        string? profile = EncoderArgumentResolver.ResolveProfile(profileValue: "high10", encoder: nvenc);
        profile.Should().Be(expected: "baseline", because: "NVENC's first-declared profile is the safe fallback");
    }

    [Fact]
    public void ResolveProfile_SupportedProfile_PassedThrough()
    {
        EncoderInfo libx264 = GetH264Encoder(ffmpegName: "libx264");
        EncoderArgumentResolver.ResolveProfile(profileValue: "high10", encoder: libx264).Should().Be(expected: "high10");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ResolveDimensions — no-upscale, even height
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolveDimensions_DoesNotUpscaleBeyondSource()
    {
        VideoOutput profile = MakeVideoOutput(width: 1920);
        (int w, int h) = EncoderArgumentResolver.ResolveDimensions(profile: profile, sourceWidth: 1280, sourceHeight: 720);
        w.Should().Be(expected: 1280, because: "upscaling is off by design");
    }

    [Fact]
    public void ResolveDimensions_NullWidth_KeepsSourceWidth()
    {
        // An archive preset re-encodes the codec without rescaling: null width
        // means "keep source width", never the 2-pixel-wide floor a literal
        // 0 used to collapse to.
        VideoOutput profile = MakeVideoOutput(width: null);
        (int w, int h) = EncoderArgumentResolver.ResolveDimensions(profile: profile, sourceWidth: 3840, sourceHeight: 1606);
        w.Should().Be(expected: 3840, because: "null width means keep the source width");
        h.Should().Be(expected: 1606, because: "height derives from the source aspect ratio");
    }

    [Fact]
    public void ResolveDimensions_LegacyZeroWidth_KeepsSourceWidth()
    {
        // Presets persisted before this field went nullable stored Width: 0.
        // That must resolve exactly like null — keep the source width —
        // never the 2-pixel-wide floor 0 used to collapse to.
        VideoOutput profile = MakeVideoOutput(width: 0);
        (int w, int h) = EncoderArgumentResolver.ResolveDimensions(profile: profile, sourceWidth: 3840, sourceHeight: 2160);
        w.Should().Be(expected: 3840, because: "legacy 0 width means keep the source width, same as null");
        h.Should().Be(expected: 2160);
    }

    [Fact]
    public void ResolveDimensions_ForcesEvenHeight()
    {
        VideoOutput profile = MakeVideoOutput(width: 853); // 853x480 → 853*(480/1920) = 213.25
        (int w, int h) = EncoderArgumentResolver.ResolveDimensions(profile: profile, sourceWidth: 1920, sourceHeight: 480);
        (h % 2).Should().Be(expected: 0, because: "h.264/h.265 require even dimensions");
    }

    [Fact]
    public void ResolveDimensions_UsesExplicitHeightWhenProvided()
    {
        VideoOutput profile = MakeVideoOutput(width: 1280, height: 720);
        (int w, int h) = EncoderArgumentResolver.ResolveDimensions(profile: profile, sourceWidth: 1920, sourceHeight: 1080);
        w.Should().Be(expected: 1280);
        h.Should().Be(expected: 720);
    }

    [Fact]
    public void ResolveDimensions_ZeroHeight_DerivesFromSourceAspect()
    {
        // A width-only ladder rung (e.g. a 4K profile) reaches the resolver with
        // Height == 0, not null: LadderRung.Height is a non-nullable int, so the
        // "derive me" null gets collapsed to 0 by `?? 0` when a VideoOutput is
        // turned into a rung and back. Zero must be treated like null — derive
        // from the source aspect ratio. Otherwise the variant is named and
        // advertised "3840x0" (RESOLUTION=3840x0) and players skip the rendition.
        VideoOutput profile = MakeVideoOutput(width: 3840, height: 0);
        (int w, int h) = EncoderArgumentResolver.ResolveDimensions(profile: profile, sourceWidth: 3840, sourceHeight: 2160);
        w.Should().Be(expected: 3840);
        h.Should().Be(expected: 2160, because: "zero height means derive from source AR, never literal 0");
    }

    [Fact]
    public void ResolveDimensions_ZeroHeight_DerivesEvenHeightForScopeSource()
    {
        // 2.39:1 scope source (a "Marvel 4K" master is rarely 16:9): 3840-wide
        // output must derive an even height from the real AR, not 2160.
        VideoOutput profile = MakeVideoOutput(width: 3840, height: 0);
        (int w, int h) = EncoderArgumentResolver.ResolveDimensions(profile: profile, sourceWidth: 3840, sourceHeight: 1606);
        w.Should().Be(expected: 3840);
        h.Should().Be(expected: 1606, because: "derived height tracks the source AR");
        (h % 2).Should().Be(expected: 0, because: "encoders require even dimensions");
    }

    [Fact]
    public void ResolveProfile_HighOnNumericVocabulary_MapsToHighNotFirstEntry()
    {
        // h264_videotoolbox exposes numeric profiles ("66"=Baseline, "77"=Main,
        // "100"=High). "high" must map to "100", never silently fall back to the
        // first entry "66" (Baseline) — that ran Baseline while the profile
        // asked for High.
        EncoderInfo videotoolbox = new(
            FfmpegName: "h264_videotoolbox",
            RequiredVendor: null,
            Presets: [],
            Profiles: ["66", "77", "100"],
            Levels: [],
            QualityRange: new(Min: 0, Max: 100, Default: 50),
            SupportedRateControl: [RateControlMode.QualityLevel],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: 3,
            PixelFormat10Bit: "yuv420p10le",
            VendorSpecificFlags: new()
        );

        EncoderArgumentResolver.ResolveProfile(profileValue: "high", encoder: videotoolbox).Should().Be(expected: "100");
        EncoderArgumentResolver.ResolveProfile(profileValue: "main", encoder: videotoolbox).Should().Be(expected: "77");
        EncoderArgumentResolver.ResolveProfile(profileValue: "baseline", encoder: videotoolbox).Should().Be(expected: "66");
    }

    [Fact]
    public void ResolveProfile_KnownVocabulary_PassesThroughVerbatim()
    {
        EncoderInfo libx264 = new(
            FfmpegName: "libx264",
            RequiredVendor: null,
            Presets: ["medium"],
            Profiles: ["baseline", "main", "high"],
            Levels: ["4.1"],
            QualityRange: new(Min: 0, Max: 51, Default: 23),
            SupportedRateControl: [RateControlMode.Crf],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: int.MaxValue,
            PixelFormat10Bit: "yuv420p10le",
            VendorSpecificFlags: new()
        );

        EncoderArgumentResolver.ResolveProfile(profileValue: "high", encoder: libx264).Should().Be(expected: "high");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ResolveProfile — bit-depth coupling (10-bit must not emit an 8-bit profile)
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: "high")]
    [InlineData(data: "main")]
    [InlineData(data: "baseline")]
    public void ResolveProfile_H264_TenBit_PromotesEightBitTierToHigh10(string requested)
    {
        // libx264 with a 10-bit pixel format rejects an 8-bit-only profile
        // string ("high profile doesn't support a bit depth of 10"). H.264's
        // ONLY 10-bit profile is high10 (there is no h264 "main10"), so every
        // 8-bit tier promotes to high10 — the profile libx264 actually accepts.
        EncoderInfo libx264 = GetH264Encoder(ffmpegName: "libx264");
        EncoderArgumentResolver
            .ResolveProfile(profileValue: requested, encoder: libx264, finalBitDepth: 10)
            .Should()
            .Be(expected: "high10");
    }

    [Fact]
    public void ResolveProfile_H264_TenBit_KeepsAlreadyTenBitProfile()
    {
        EncoderInfo libx264 = GetH264Encoder(ffmpegName: "libx264");
        EncoderArgumentResolver
            .ResolveProfile(profileValue: "high10", encoder: libx264, finalBitDepth: 10)
            .Should()
            .Be(expected: "high10");
    }

    [Fact]
    public void ResolveProfile_H264_EightBit_LeavesEightBitTierUntouched()
    {
        // The promotion must never fire at 8-bit — "high" stays "high".
        EncoderInfo libx264 = GetH264Encoder(ffmpegName: "libx264");
        EncoderArgumentResolver
            .ResolveProfile(profileValue: "high", encoder: libx264, finalBitDepth: 8)
            .Should()
            .Be(expected: "high");
    }

    [Fact]
    public void ResolveProfile_H265_TenBit_PromotesMainToMain10()
    {
        EncoderInfo libx265 = GetEncoder(codec: VideoCodecType.H265, ffmpegName: "libx265");
        EncoderArgumentResolver
            .ResolveProfile(profileValue: "main", encoder: libx265, finalBitDepth: 10)
            .Should()
            .Be(expected: "main10");
    }

    [Fact]
    public void ResolveProfile_H264_TenBit_NullProfile_FallsBackToTenBitCapable()
    {
        // A null (Auto) request at 10-bit must not fall back to Profiles[0]
        // ("baseline", 8-bit only) — the fallback itself has to be 10-bit-capable.
        EncoderInfo libx264 = GetH264Encoder(ffmpegName: "libx264");
        string? resolved = EncoderArgumentResolver.ResolveProfile(profileValue: null, encoder: libx264, finalBitDepth: 10);
        resolved.Should().BeOneOf(validValues: ["high10", "high422", "high444p"]);
    }

    [Fact]
    public void ResolveProfile_Vp9_TenBit_DoesNotApplyH26xPromotion()
    {
        // VP9 numeric profiles mean chroma/bit-depth, NOT an H.264 tier. The
        // H26x promotion must not touch them — "main" is not VP9 vocabulary, so
        // it falls back to the encoder's own first profile, never "main10".
        EncoderInfo vpx = GetEncoder(codec: VideoCodecType.Vp9, ffmpegName: "libvpx-vp9");
        EncoderArgumentResolver
            .ResolveProfile(profileValue: "main", encoder: vpx, finalBitDepth: 10)
            .Should()
            .Be(expected: "0", because: "VP9 falls back to its own first profile, never an H26x sibling");
    }

    [Fact]
    public void ResolveDimensions_OddExplicitWidth_IsRoundedEven()
    {
        // 1921 passes validation (Width > 0) but reaches scale=1921:-2, which
        // libx264 rejects ("width not divisible by 2"). Width must be evened.
        VideoOutput profile = MakeVideoOutput(width: 1921);
        (int w, int _) = EncoderArgumentResolver.ResolveDimensions(profile: profile, sourceWidth: 3840, sourceHeight: 2160);
        (w % 2).Should().Be(expected: 0, because: "an odd luma width aborts the encode");
        w.Should().Be(expected: 1920, because: "round DOWN so the result never exceeds the request/source");
    }

    [Fact]
    public void ResolveDimensions_OddManualRungWidth_IsRoundedEven()
    {
        // Manual ladder rungs carry hand-authored widths (e.g. 853 for 480p);
        // an odd rung width hits the same scale=W:-2 abort as an odd profile width.
        VideoOutput rung = MakeVideoOutput(width: 853, height: 480);
        (int w, int h) = EncoderArgumentResolver.ResolveDimensions(profile: rung, sourceWidth: 1920, sourceHeight: 1080);
        (w % 2).Should().Be(expected: 0);
        (h % 2).Should().Be(expected: 0);
        w.Should().Be(expected: 852);
    }

    [Fact]
    public void ResolveDimensions_ZeroSourceWidth_DoesNotCollapseHeightToZero()
    {
        // A corrupt/partial probe can report sourceWidth = 0. With a derived
        // (null) height the old code produced height 0 → RESOLUTION=WIDTHx0 in
        // the HLS master. Height must fall back to a real value.
        VideoOutput profile = MakeVideoOutput(width: 1920, height: null);
        (int _, int h) = EncoderArgumentResolver.ResolveDimensions(profile: profile, sourceWidth: 0, sourceHeight: 0);
        h.Should().BeGreaterThan(expected: 0, because: "a zero-height variant is skipped by every player");
        (h % 2).Should().Be(expected: 0);
    }

    [Fact]
    public void ResolveDimensions_ExplicitHeightAboveSource_IsClampedNoUpscale()
    {
        // Height=2160 on a 1280x720 source passed the width-only upscale guard
        // and reached scale_cuda=1280:2160 — a stretched upscale. Explicit
        // height must clamp to the source just like width does.
        VideoOutput profile = MakeVideoOutput(width: 1280, height: 2160);
        (int _, int h) = EncoderArgumentResolver.ResolveDimensions(profile: profile, sourceWidth: 1280, sourceHeight: 720);
        h.Should().Be(expected: 720, because: "explicit height must not upscale beyond source");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static EncoderInfo GetH264Encoder(string ffmpegName) =>
        GetEncoder(codec: VideoCodecType.H264, ffmpegName: ffmpegName);

    private static EncoderInfo GetEncoder(VideoCodecType codec, string ffmpegName)
    {
        foreach ((VideoCodecType c, EncoderInfo encoder) in Registry.EnumerateVideoEncoders())
        {
            if (c == codec && encoder.FfmpegName == ffmpegName)
                return encoder;
        }
        throw new InvalidOperationException(message: $"Encoder {ffmpegName} not registered for {codec}");
    }

    private static ResolvedCodec ResolveH264(
        string ffmpegName,
        GpuVendor? vendor,
        RateControlMode defaultRateControl
    )
    {
        EncoderInfo encoder = GetH264Encoder(ffmpegName: ffmpegName);
        GpuDevice? device = vendor is null
            ? null
            : new GpuDevice(
                Vendor: vendor.Value,
                Name: $"Test {vendor.Value}",
                VramMb: 16_384,
                MaxEncoderSessions: 12,
                SupportedCodecs: [VideoCodecType.H264]
            );
        return new(FfmpegEncoderName: ffmpegName, EncoderInfo: encoder, Device: device, DefaultRateControl: defaultRateControl);
    }

    private static VideoOutput MakeVideoOutput(int? width, int? height = null) =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: VideoCodecType.H264,
            Width: width,
            Height: height,
            RateControl: V2RateControlMode.Crf,
            Crf: 22,
            BitrateKbps: 4000,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: "medium",
            CodecProfile: CodecProfile.High,
            Level: null,
            Tune: null,
            BitDepth: 8,
            PixelFormat: null,
            KeyframeIntervalSeconds: 2,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
        );
}
