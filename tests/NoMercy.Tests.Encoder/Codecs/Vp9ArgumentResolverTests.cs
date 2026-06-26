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
/// VP9 has the narrowest hardware support of the video codecs: only Intel
/// (QSV + VAAPI). NVIDIA, AMD, and Apple do not ship VP9 encoders. These
/// tests pin that hardware matrix plus VP9-specific behaviors:
///
///   - libvpx-vp9 has no presets (uses -deadline / -cpu-used instead).
///   - libvpx-vp9 uses numeric profile IDs ("profile0".."profile3") where
///     profile2/profile3 enable 10-bit.
///   - vp9_qsv lacks 10-bit support. vp9_vaapi also lacks it.
///   - vp9_vaapi uses 0-255 QP (VA-API convention), not 0-63 like libvpx.
/// </summary>
public class Vp9ArgumentResolverTests
{
    private static readonly CodecRegistry Registry = new();

    [Fact]
    public void LibVpxVp9_HasNoPresetConcept()
    {
        // -preset isn't accepted by libvpx-vp9. Speed tuning happens via
        // -deadline / -cpu-used which are not part of the preset array.
        EncoderInfo vpx = Get("libvpx-vp9");
        vpx.Presets.Should().BeEmpty();
        EncoderArgumentResolver.ResolvePreset("medium", vpx).Should().BeNull();
    }

    [Fact]
    public void LibVpxVp9_UsesNumericProfiles()
    {
        EncoderInfo vpx = Get("libvpx-vp9");
        vpx.Profiles.Should().BeEquivalentTo("profile0", "profile1", "profile2", "profile3");
        // "main" from H264/HEVC profile strings must fall back to profile0.
        EncoderArgumentResolver.ResolveProfile("main", vpx).Should().Be("profile0");
    }

    [Fact]
    public void LibVpxVp9_Supports10BitButNotHdr()
    {
        // VP9 profile2/profile3 carry 10-bit but VP9 doesn't define HDR10
        // static metadata semantics, so SupportsHdr must be false even
        // though bit depth goes to 10.
        EncoderInfo vpx = Get("libvpx-vp9");
        vpx.Supports10Bit.Should().BeTrue();
        vpx.SupportsHdr.Should().BeFalse();
    }

    [Fact]
    public void Vp9_HardwareEncoders_AreIntelOnly()
    {
        EncoderInfo[] hwEncoders = Registry
            .EnumerateVideoEncoders()
            .Where(t => t.CodecType == VideoCodecType.Vp9 && t.Encoder.RequiredVendor is not null)
            .Select(t => t.Encoder)
            .ToArray();

        hwEncoders.Should().OnlyContain(e => e.RequiredVendor == GpuVendor.Intel);
        hwEncoders.Should().NotContain(e => e.RequiredVendor == GpuVendor.Nvidia);
        hwEncoders.Should().NotContain(e => e.RequiredVendor == GpuVendor.Amd);
        hwEncoders.Should().NotContain(e => e.RequiredVendor == GpuVendor.Apple);
    }

    [Fact]
    public void Vp9Qsv_MapsCrfToGlobalQuality_ScaledFromVpxRange()
    {
        // vp9_qsv uses 1-51 ICQ, but the profile's CRF is written in the VP9
        // software reference scale (0-63). Scaled: round(33/63*51)=27.
        ResolvedCodec qsv = Resolve("vp9_qsv", GpuVendor.Intel, RateControlMode.Icq);
        Dictionary<string, string> flags = [];

        EncoderArgumentResolver.ResolveQuality(33, qsv, flags);

        flags["-global_quality"]
            .Should()
            .Be("27", "33 on the 0-63 VP9 reference scale maps to 27 on the 1-51 QSV scale");
    }

    [Fact]
    public void Vp9Vaapi_UsesFullQpRange_LacksPresets()
    {
        EncoderInfo vaapi = Get("vp9_vaapi");
        vaapi.QualityRange.Max.Should().Be(255);
        vaapi.Presets.Should().BeEmpty();
    }

    private static EncoderInfo Get(string ffmpegName)
    {
        foreach ((VideoCodecType c, EncoderInfo encoder) in Registry.EnumerateVideoEncoders())
        {
            if (c == VideoCodecType.Vp9 && encoder.FfmpegName == ffmpegName)
                return encoder;
        }
        throw new InvalidOperationException($"VP9 encoder {ffmpegName} not registered");
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
                SupportedCodecs: [VideoCodecType.Vp9]
            );
        return new(ffmpegName, encoder, device, defaultRateControl);
    }
}
