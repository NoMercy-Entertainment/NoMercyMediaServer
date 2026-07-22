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

namespace NoMercy.Tests.Encoder.Codecs;

/// <summary>
/// HLS CODECS strings for EXT-X-STREAM-INF / EXT-X-MEDIA tags follow strict
/// RFC 6381 / ISO 14496-15 / AV1 ISOBMFF binding formats. Wrong characters
/// or wrong levels make players reject the variant entirely — every value
/// here is a contract with player implementations.
/// </summary>
public class HlsCodecsStringBuilderTests
{
    // ── H.264 (avc1.PPCCLL) ────────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["baseline", "3.0", "avc1.42401E"])] // PP=0x42, CC=0x40 (constraint), LL=0x1E (30)
    [InlineData(data: ["main", "4.0", "avc1.4D0028"])] // PP=0x4D, CC=0x00, LL=0x28 (40)
    [InlineData(data: ["high", "4.1", "avc1.640029"])] // PP=0x64, CC=0x00, LL=0x29 (41)
    [InlineData(data: ["high10", "5.0", "avc1.6E0032"])] // PP=0x6E (High 10), LL=0x32 (50)
    [InlineData(data: ["high422", "5.1", "avc1.7A0033"])] // PP=0x7A (High 4:2:2), LL=0x33 (51)
    [InlineData(data: ["high444", "5.2", "avc1.F40034"])] // PP=0xF4 (High 4:4:4), LL=0x34 (52)
    public void ForH264_KnownProfileLevels(string profile, string level, string expected)
    {
        HlsCodecsStringBuilder.ForH264(profile: profile, level: level).Should().Be(expected: expected);
    }

    [Fact]
    public void ForH264_NullProfile_DefaultsToHigh()
    {
        HlsCodecsStringBuilder.ForH264(profile: null, level: "4.0").Should().Be(expected: "avc1.640028");
    }

    [Fact]
    public void ForH264_NullLevel_DefaultsTo4_0()
    {
        HlsCodecsStringBuilder.ForH264(profile: "high", level: null).Should().Be(expected: "avc1.640028");
    }

    [Theory]
    [InlineData(data: ["constrained baseline", "avc1.42401E"])] // Marketing-name alias
    [InlineData(data: ["hi10p", "avc1.6E001E"])] // x264-style alias (10-bit)
    public void ForH264_AcceptsCommonAliases(string profile, string expectedPrefix)
    {
        HlsCodecsStringBuilder.ForH264(profile: profile, level: "3.0").Should().Be(expected: expectedPrefix);
    }

    // ── HEVC (hvc1.P.CC.LLL.B0) ────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["3.1", false, "hvc1.1.6.L93.B0"])] // SDR Main
    [InlineData(data: ["4.0", true, "hvc1.2.4.L120.B0"])] // HDR Main10
    [InlineData(data: ["5.1", false, "hvc1.1.6.L153.B0"])] // SDR Main, 4K
    [InlineData(data: ["5.1", true, "hvc1.2.4.L153.B0"])] // HDR Main10, 4K
    public void ForHevc_KnownLevels(string level, bool tenBit, string expected)
    {
        HlsCodecsStringBuilder.ForHevc(profile: "main", level: level, tenBit: tenBit).Should().Be(expected: expected);
    }

    [Fact]
    public void ForHevc_NullLevel_DefaultsByBitDepth()
    {
        HlsCodecsStringBuilder.ForHevc(profile: "main", level: null, tenBit: false).Should().Be(expected: "hvc1.1.6.L93.B0");
        HlsCodecsStringBuilder.ForHevc(profile: "main", level: null, tenBit: true).Should().Be(expected: "hvc1.2.4.L120.B0");
    }

    // ── AV1 (av01.0.LLT.DD) ────────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["4.0", false, "av01.0.08M.08"])] // 1080p AV1 8-bit
    [InlineData(data: ["5.0", false, "av01.0.12M.08"])] // 4K AV1 8-bit
    [InlineData(data: ["5.3", true, "av01.0.15M.10"])] // 4K AV1 10-bit
    [InlineData(data: ["3.0", false, "av01.0.04M.08"])] // 720p AV1 8-bit
    public void ForAv1_KnownLevels(string level, bool tenBit, string expected)
    {
        HlsCodecsStringBuilder.ForAv1(level: level, tenBit: tenBit).Should().Be(expected: expected);
    }

    [Fact]
    public void ForAv1_NullLevel_DefaultsByBitDepth()
    {
        HlsCodecsStringBuilder.ForAv1(level: null, tenBit: false).Should().Be(expected: "av01.0.08M.08");
        HlsCodecsStringBuilder.ForAv1(level: null, tenBit: true).Should().Be(expected: "av01.0.15M.10");
    }

    // ── Audio codec strings ────────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["aac", false, "mp4a.40.2"])] // AAC-LC
    [InlineData(data: ["aac", true, "mp4a.40.5"])] // HE-AAC
    [InlineData(data: ["libfdk_aac", false, "mp4a.40.2"])]
    [InlineData(data: ["libfdk_aac", true, "mp4a.40.5"])]
    [InlineData(data: ["ac3", false, "ac-3"])]
    [InlineData(data: ["eac3", false, "ec-3"])]
    [InlineData(data: ["libopus", false, "opus"])]
    [InlineData(data: ["opus", false, "opus"])]
    [InlineData(data: ["unknown_codec", false, "mp4a.40.2"])] // Defaults to AAC-LC
    public void AudioCodecString_KnownEncoders(string encoder, bool heAac, string expected)
    {
        HlsCodecsStringBuilder.AudioCodecString(encoderName: encoder, heAac: heAac).Should().Be(expected: expected);
    }

    [Fact]
    public void AudioCodecString_Copy_ReturnsNullInsteadOfLying()
    {
        // Same reasoning as VideoCodecString_Copy — the source audio codec
        // could be anything; defaulting to mp4a.40.2 (AAC-LC) misdeclares it.
        HlsCodecsStringBuilder.AudioCodecString(encoderName: "copy").Should().BeNull();
    }

    // ── VideoCodecString dispatch via classifier ────────────────────────────

    [Theory]
    [InlineData(data: ["libx264", "main", "4.0", false, "avc1.4D0028"])]
    [InlineData(data: ["h264_nvenc", "high", "4.1", false, "avc1.640029"])]
    [InlineData(data: ["libx265", "main", "3.1", false, "hvc1.1.6.L93.B0"])]
    [InlineData(data: ["hevc_nvenc", "main10", "4.0", true, "hvc1.2.4.L120.B0"])]
    [InlineData(data: ["libsvtav1", null, "5.0", false, "av01.0.12M.08"])]
    [InlineData(data: ["av1_nvenc", null, "5.3", true, "av01.0.15M.10"])]
    [InlineData(data: ["libvpx-vp9", null, null, false, "vp09.00.41.08"])]
    [InlineData(data: ["vp9_qsv", null, null, true, "vp09.00.41.10"])]
    public void VideoCodecString_ClassifiesEncoderCorrectly(
        string encoder,
        string? profile,
        string? level,
        bool tenBit,
        string expected
    )
    {
        HlsCodecsStringBuilder
            .VideoCodecString(encoderName: encoder, profile: profile, level: level, tenBit: tenBit)
            .Should()
            .Be(expected: expected);
    }

    [Fact]
    public void VideoCodecString_UnknownEncoder_FallsBackToH264HighL4_0()
    {
        HlsCodecsStringBuilder
            .VideoCodecString(encoderName: "totally_unknown_encoder", profile: null, level: null, tenBit: false)
            .Should()
            .Be(expected: "avc1.640028");
    }

    [Fact]
    public void VideoCodecString_Copy_ReturnsNullInsteadOfLying()
    {
        // "copy" passes the source stream through untouched — the real codec
        // could be HEVC, AV1, VP9, anything. Falling back to the H.264 default
        // (the pre-fix behavior) makes the master playlist advertise the
        // wrong codec, which some players (hls.js) hard-reject.
        HlsCodecsStringBuilder.VideoCodecString(encoderName: "copy", profile: null, level: null, tenBit: false).Should().BeNull();
    }

    [Theory]
    [InlineData(data: "COPY")]
    [InlineData(data: "Copy")]
    public void VideoCodecString_CopyCaseInsensitive_ReturnsNull(string encoder)
    {
        HlsCodecsStringBuilder.VideoCodecString(encoderName: encoder, profile: "high", level: "4.0", tenBit: false).Should().BeNull();
    }

    // ── ParseH264Level numeric input ───────────────────────────────────────

    [Theory]
    [InlineData(data: ["40", "avc1.640028"])] // "40" parses as numeric 40 = level 4.0
    [InlineData(data: ["51", "avc1.640033"])] // "51" → 0x33 = level 5.1
    public void ForH264_AcceptsNumericLevelInput(string level, string expected)
    {
        HlsCodecsStringBuilder.ForH264(profile: "high", level: level).Should().Be(expected: expected);
    }

    [Fact]
    public void ForH264_UnknownLevelString_DefaultsTo4_0()
    {
        // Garbage that fails numeric parse falls through the lookup default.
        HlsCodecsStringBuilder.ForH264(profile: "high", level: "garbage").Should().Be(expected: "avc1.640028");
    }

    // ── HEVC additional levels ──────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["3", "hvc1.1.6.L90.B0"])]
    [InlineData(data: ["4.1", "hvc1.1.6.L123.B0"])]
    [InlineData(data: ["6.0", "hvc1.1.6.L180.B0"])]
    [InlineData(data: ["6.2", "hvc1.1.6.L186.B0"])]
    public void ForHevc_AdditionalLevels(string level, string expected)
    {
        HlsCodecsStringBuilder.ForHevc(profile: "main", level: level, tenBit: false).Should().Be(expected: expected);
    }

    [Fact]
    public void ForHevc_UnknownLevel_DefaultsByBitDepth()
    {
        // Garbage level falls through the switch's `_ =>` arm.
        HlsCodecsStringBuilder
            .ForHevc(profile: "main", level: "weird", tenBit: false)
            .Should()
            .Be(expected: "hvc1.1.6.L93.B0");
        HlsCodecsStringBuilder
            .ForHevc(profile: "main", level: "weird", tenBit: true)
            .Should()
            .Be(expected: "hvc1.2.4.L120.B0");
    }

    // ── AV1 additional levels ──────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["2.0", false, "av01.0.00M.08"])]
    [InlineData(data: ["2.1", false, "av01.0.01M.08"])]
    [InlineData(data: ["3.1", false, "av01.0.05M.08"])]
    [InlineData(data: ["4.1", false, "av01.0.09M.08"])]
    [InlineData(data: ["5.1", false, "av01.0.13M.08"])]
    [InlineData(data: ["5.2", true, "av01.0.14M.10"])]
    [InlineData(data: ["6.0", false, "av01.0.16M.08"])]
    [InlineData(data: ["6.3", true, "av01.0.19M.10"])]
    public void ForAv1_AdditionalLevels(string level, bool tenBit, string expected)
    {
        HlsCodecsStringBuilder.ForAv1(level: level, tenBit: tenBit).Should().Be(expected: expected);
    }

    [Fact]
    public void ForAv1_UnknownLevel_Defaults_AndBitDepthIndependent()
    {
        // Non-empty garbage level → switch falls through to index 8 (level 4.0)
        // regardless of bit depth. Only the null/empty case defaults to 15 for
        // 10-bit content — the lookup default is always 8.
        HlsCodecsStringBuilder.ForAv1(level: "garbage", tenBit: false).Should().Be(expected: "av01.0.08M.08");
        HlsCodecsStringBuilder.ForAv1(level: "garbage", tenBit: true).Should().Be(expected: "av01.0.08M.10");
    }

    // ── Audio constants ─────────────────────────────────────────────────────

    [Fact]
    public void AudioConstants_ReturnSpecExactStrings()
    {
        // Pin the exact strings — these are wire-format identifiers from
        // the MP4 Registration Authority. Any drift breaks player parsers
        // that string-match against well-known values.
        HlsCodecsStringBuilder.ForAacLc().Should().Be(expected: "mp4a.40.2");
        HlsCodecsStringBuilder.ForHeAac().Should().Be(expected: "mp4a.40.5");
        HlsCodecsStringBuilder.ForAc3().Should().Be(expected: "ac-3");
        HlsCodecsStringBuilder.ForEac3().Should().Be(expected: "ec-3");
    }

    [Fact]
    public void AudioCodecString_CaseInsensitiveEncoderName()
    {
        // Encoder names land here as-is from PlaylistGenerator; the matcher
        // must accept the canonical lowercase as well as upper/mixed case.
        HlsCodecsStringBuilder.AudioCodecString(encoderName: "AAC", heAac: false).Should().Be(expected: "mp4a.40.2");
        HlsCodecsStringBuilder.AudioCodecString(encoderName: "OPUS", heAac: false).Should().Be(expected: "opus");
        HlsCodecsStringBuilder.AudioCodecString(encoderName: "EAC3", heAac: false).Should().Be(expected: "ec-3");
    }

    // ── Resolution-derived level floor ──────────────────────────────────────
    //
    // The Punisher master advertised hvc1.2.4.L120.B0 (HEVC level 4.0) for the
    // 4K HDR rung — a level that legally tops out at 1080p. The level came
    // straight from the preset (or its null → L4.0 default) and was never
    // checked against the resolution. A player validating the codec string can
    // reject the variant. The advertised level must be clamped UP to the
    // minimum the resolution + frame rate actually require.

    [Fact]
    public void ForHevc_4KHdr_ClampsLevelUpFromPresetDefault()
    {
        // 3840×2160 @ 23.976 needs ≥ L5.0 (150). A preset saying "4.0" (120)
        // or nothing must NOT win — the emitted level rises to the 4K floor.
        HlsCodecsStringBuilder
            .ForHevc(profile: "main10", level: "4.0", tenBit: true, width: 3840, height: 2160, frameRate: 23.976)
            .Should()
            .Be(expected: "hvc1.2.4.L150.B0");

        HlsCodecsStringBuilder
            .ForHevc(profile: "main10", level: null, tenBit: true, width: 3840, height: 2160, frameRate: 23.976)
            .Should()
            .Be(expected: "hvc1.2.4.L150.B0");
    }

    [Fact]
    public void ForHevc_4KHighFrameRate_NeedsLevel5_1()
    {
        // 3840×2160 @ 60 exceeds L5.0's sample-rate ceiling → L5.1 (153).
        HlsCodecsStringBuilder
            .ForHevc(profile: "main10", level: "4.0", tenBit: true, width: 3840, height: 2160, frameRate: 60)
            .Should()
            .Be(expected: "hvc1.2.4.L153.B0");
    }

    [Fact]
    public void ForHevc_1080p_KeepsPresetLevelWhenResolutionAllows()
    {
        // 1080p genuinely fits L4.0 — the clamp is a floor, not an override, so
        // the correct low level is preserved.
        HlsCodecsStringBuilder
            .ForHevc(profile: "main10", level: "4.0", tenBit: true, width: 1920, height: 1080, frameRate: 23.976)
            .Should()
            .Be(expected: "hvc1.2.4.L120.B0");
    }

    [Fact]
    public void ForHevc_PresetAboveFloor_IsNotLowered()
    {
        // A preset that already over-states the level (5.2 on 1080p) is kept —
        // the clamp only raises, never lowers.
        HlsCodecsStringBuilder
            .ForHevc(profile: "main10", level: "5.2", tenBit: true, width: 1920, height: 1080, frameRate: 23.976)
            .Should()
            .Be(expected: "hvc1.2.4.L156.B0");
    }

    [Fact]
    public void ForH264_4K_ClampsToLevel5_1()
    {
        // 3840×2160 H.264 needs ≥ L5.1 (0x33) — L4.0 cannot carry the frame
        // size. Preset "4.0" must be raised.
        HlsCodecsStringBuilder
            .ForH264(profile: "high", level: "4.0", width: 3840, height: 2160, frameRate: 23.976)
            .Should()
            .Be(expected: "avc1.640033");
    }

    [Fact]
    public void ForAv1_4K8Bit_ClampsToLevel5_0()
    {
        // 3840×2160 @ 23.976 8-bit AV1 needs level index 12 (5.0). A preset
        // "4.0" (index 8) under-states it.
        HlsCodecsStringBuilder
            .ForAv1(level: "4.0", tenBit: false, width: 3840, height: 2160, frameRate: 23.976)
            .Should()
            .Be(expected: "av01.0.12M.08");
    }

    [Fact]
    public void VideoCodecString_4KHevc_EmitsResolutionCorrectLevel()
    {
        // End-to-end through the classifier: the exact regression the Punisher
        // master hit — hevc_nvenc, 4K, preset level 4.0 → must NOT be L120.
        HlsCodecsStringBuilder
            .VideoCodecString(
                encoderName: "hevc_nvenc",
                profile: "main10",
                level: "4.0",
                tenBit: true,
                width: 3840,
                height: 2160,
                frameRate: 23.976
            )
            .Should()
            .Be(expected: "hvc1.2.4.L150.B0");
    }

    [Fact]
    public void VideoCodecString_NoResolution_LeavesLevelUnclamped()
    {
        // Callers that don't pass a resolution (width/height default 0) keep the
        // pre-clamp behavior — the floor only applies with real dimensions.
        HlsCodecsStringBuilder
            .VideoCodecString(encoderName: "hevc_nvenc", profile: "main10", level: "4.0", tenBit: true)
            .Should()
            .Be(expected: "hvc1.2.4.L120.B0");
    }
}
