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

namespace NoMercy.Tests.Encoder.Codecs;

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
        EncoderInfo libx265 = Get(ffmpegName: "libx265");
        libx265.Supports10Bit.Should().BeTrue();
        libx265.SupportsHdr.Should().BeTrue();
        libx265.Profiles.Should().Contain(expected: "main10");
        libx265.PixelFormat10Bit.Should().Be(expected: "yuv420p10le");
    }

    [Fact]
    public void HevcNvenc_MapsCrfToVbrCq()
    {
        ResolvedCodec nvenc = Resolve(ffmpegName: "hevc_nvenc", vendor: GpuVendor.Nvidia, defaultRateControl: RateControlMode.Cq);
        Dictionary<string, string> flags = [];

        EncoderArgumentResolver.ResolveQuality(profileCrf: 28, resolved: nvenc, extraFlags: flags);

        flags[key: "-rc"].Should().Be(expected: "vbr");
        flags[key: "-cq"].Should().Be(expected: "28", because: "hevc default CRF is 28, not 23 like H264");
    }

    [Fact]
    public void HevcAmf_CarriesUsageTranscoding()
    {
        // AMF REQUIRES -usage transcoding or the encoder picks ultra-low-latency
        // mode by default → pixellated output at any reasonable bitrate.
        EncoderInfo amf = Get(ffmpegName: "hevc_amf");
        amf.VendorSpecificFlags.Should().ContainKey(expected: "-usage");
        amf.VendorSpecificFlags[key: "-usage"].Should().Be(expected: "transcoding");
    }

    [Fact]
    public void HevcQsv_QualityStartsAt1_NotZero()
    {
        // Intel QSV's HEVC encoder rejects -global_quality 0 (interpreted as
        // "default"). Minimum is 1. Getting this wrong means silent fallback.
        EncoderInfo qsv = Get(ffmpegName: "hevc_qsv");
        qsv.QualityRange.Min.Should().Be(expected: 1);
    }

    [Fact]
    public void HevcVaapi_HasNoPresetConcept()
    {
        // VAAPI doesn't accept -preset. ResolvePreset must return null.
        EncoderInfo vaapi = Get(ffmpegName: "hevc_vaapi");
        vaapi.Presets.Should().BeEmpty();
        EncoderArgumentResolver.ResolvePreset(profilePreset: "medium", encoder: vaapi).Should().BeNull();
    }

    [Fact]
    public void HevcVideoToolbox_UsesNumericProfiles()
    {
        // Apple's HEVC encoder doesn't accept "main"/"main10" — it takes
        // integer profile IDs. "1" = Main, "2" = Main10. Profile mapping from
        // a library-style profile string must fall back to the first
        // declared profile, which is "1" (= Main).
        EncoderInfo vt = Get(ffmpegName: "hevc_videotoolbox");
        vt.Profiles.Should().BeEquivalentTo(expectation: ["1", "2"]);
        EncoderArgumentResolver.ResolveProfile(profileValue: "main", encoder: vt).Should().Be(expected: "1");
    }

    [Fact]
    public void HevcVideoToolbox_EmitsHvc1Tag()
    {
        EncoderInfo vt = Get(ffmpegName: "hevc_videotoolbox");
        vt.VendorSpecificFlags[key: "-tag:v"]
            .Should()
            .Be(expected: "hvc1", because: "Apple clients reject HEVC MP4 without hvc1 branding");
    }

    [Fact]
    public void HevcVideoToolbox_DoesNotSupport10Bit()
    {
        // hevc_videotoolbox has Supports10Bit=false so the PlanStage downgrade
        // guard kicks in for TenBit=true profiles on Apple Silicon / Intel Mac.
        EncoderInfo vt = Get(ffmpegName: "hevc_videotoolbox");
        vt.Supports10Bit.Should().BeFalse();
    }

    private static EncoderInfo Get(string ffmpegName)
    {
        foreach ((VideoCodecType c, EncoderInfo encoder) in Registry.EnumerateVideoEncoders())
        {
            if (c == VideoCodecType.H265 && encoder.FfmpegName == ffmpegName)
                return encoder;
        }
        throw new InvalidOperationException(message: $"HEVC encoder {ffmpegName} not registered");
    }

    private static ResolvedCodec Resolve(
        string ffmpegName,
        GpuVendor? vendor,
        RateControlMode defaultRateControl
    )
    {
        EncoderInfo encoder = Get(ffmpegName: ffmpegName);
        GpuDevice? device = vendor is null
            ? null
            : new GpuDevice(
                Vendor: vendor.Value,
                Name: $"Test {vendor.Value}",
                VramMb: 16_384,
                MaxEncoderSessions: 12,
                SupportedCodecs: [VideoCodecType.H265]
            );
        return new(FfmpegEncoderName: ffmpegName, EncoderInfo: encoder, Device: device, DefaultRateControl: defaultRateControl);
    }
}
