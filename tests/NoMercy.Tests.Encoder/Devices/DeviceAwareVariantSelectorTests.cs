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

using NoMercy.Encoder.Devices;

namespace NoMercy.Tests.Encoder.Devices;

public class DeviceAwareVariantSelectorTests
{
    private readonly DeviceAwareVariantSelector _selector = new();

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static VariantDescriptor V(
        int index,
        int height,
        int width,
        string vCodec,
        int bitrateKbps,
        int audioChannels,
        string aCodec
    ) => new(Index: index, Height: height, Width: width, VideoCodec: vCodec, VideoBitrateKbps: bitrateKbps, AudioChannels: audioChannels, AudioCodec: aCodec);

    // ──────────────────────────────────────────────────────────────────────────
    // Test 1: No caps → variant 0, no constraints
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoCaps_ReturnsVariant0_NoConstraints()
    {
        VariantDescriptor[] variants =
        [
            V(index: 0, height: 1080, width: 1920, vCodec: "h264", bitrateKbps: 6000, audioChannels: 6, aCodec: "eac3"),
            V(index: 1, height: 720, width: 1280, vCodec: "h264", bitrateKbps: 3000, audioChannels: 2, aCodec: "aac"),
        ];

        VariantSelection sel = _selector.Select(variants: variants, caps: null);

        sel.VariantIndex.Should().Be(expected: 0);
        sel.AppliedCapabilities.Should().BeNull();
        sel.AudioConstraint.Should().BeNull();
        sel.VideoConstraint.Should().BeNull();
        sel.Reason.Should().Contain(expected: "no capabilities declared");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 2: 5.1 caps + 5.1 variant exists → that variant selected
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FiveOneCaps_FiveOneVariantExists_PicksIt()
    {
        DeviceCapabilities caps = new()
        {
            MaxAudioChannels = 6,
            AudioCodecs = ["eac3", "ac3", "aac"],
            VideoCodecs = ["h264", "hevc"],
            MaxVideoHeight = 1080,
            RamTier = DeviceRamTier.Standard,
        };

        VariantDescriptor[] variants =
        [
            V(index: 0, height: 1080, width: 1920, vCodec: "h264", bitrateKbps: 6000, audioChannels: 6, aCodec: "eac3"),
            V(index: 1, height: 720, width: 1280, vCodec: "h264", bitrateKbps: 3000, audioChannels: 2, aCodec: "aac"),
        ];

        VariantSelection sel = _selector.Select(variants: variants, caps: caps);

        sel.VariantIndex.Should().Be(expected: 0);
        sel.AudioConstraint.Should().BeNull();
        sel.VideoConstraint.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 3: Stereo cap + 5.1-only variants → transcode AudioConstraint(2,"aac")
    // THIS IS THE BEDROOM NOKIA CASE — duplicate here for D1 coverage
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void StereoCap_FiveOneOnlyVariants_TranscodeSignal_AudioConstraint2Aac()
    {
        DeviceCapabilities caps = new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac"],
            VideoCodecs = ["h264"],
            MaxVideoHeight = 1080,
            RamTier = DeviceRamTier.Standard,
        };

        VariantDescriptor[] variants =
        [
            V(index: 0, height: 1080, width: 1920, vCodec: "h264", bitrateKbps: 6000, audioChannels: 6, aCodec: "eac3"),
            V(index: 1, height: 1080, width: 1920, vCodec: "h264", bitrateKbps: 4000, audioChannels: 6, aCodec: "ac3"),
        ];

        VariantSelection sel = _selector.Select(variants: variants, caps: caps);

        sel.VariantIndex.Should().BeNull();
        sel.AudioConstraint.Should().Be(expected: new AudioConstraint(Channels: 2, Codec: "aac"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 4: AAC-only cap + Opus variant → transcode with aac
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AacOnlyCap_OpusVariant_TranscodeAac()
    {
        DeviceCapabilities caps = new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac"],
            VideoCodecs = ["h264"],
            RamTier = DeviceRamTier.Standard,
        };

        VariantDescriptor[] variants = [V(index: 0, height: 720, width: 1280, vCodec: "h264", bitrateKbps: 3000, audioChannels: 2, aCodec: "opus")];

        VariantSelection sel = _selector.Select(variants: variants, caps: caps);

        sel.VariantIndex.Should().BeNull();
        sel.AudioConstraint.Should().NotBeNull();
        sel.AudioConstraint!.Codec.Should().Be(expected: "aac");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 5: 1080p cap + 4K-only variants → transcode VideoConstraint(1080)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HD1080Cap_4KOnlyVariants_TranscodeVideoConstraint()
    {
        DeviceCapabilities caps = new()
        {
            MaxVideoHeight = 1080,
            VideoCodecs = ["h264"],
            RamTier = DeviceRamTier.Standard,
        };

        VariantDescriptor[] variants = [V(index: 0, height: 2160, width: 3840, vCodec: "h264", bitrateKbps: 15000, audioChannels: 2, aCodec: "aac")];

        VariantSelection sel = _selector.Select(variants: variants, caps: caps);

        sel.VariantIndex.Should().BeNull();
        sel.VideoConstraint.Should().NotBeNull();
        sel.VideoConstraint!.MaxHeight.Should().Be(expected: 1080);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 6: H.264-only cap + HEVC-only variants → transcode codec h264
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void H264OnlyCap_HevcOnlyVariants_TranscodeH264()
    {
        DeviceCapabilities caps = new()
        {
            VideoCodecs = ["h264"],
            RamTier = DeviceRamTier.Standard,
        };

        VariantDescriptor[] variants = [V(index: 0, height: 1080, width: 1920, vCodec: "hevc", bitrateKbps: 6000, audioChannels: 2, aCodec: "aac")];

        VariantSelection sel = _selector.Select(variants: variants, caps: caps);

        sel.VariantIndex.Should().BeNull();
        sel.VideoConstraint.Should().NotBeNull();
        sel.VideoConstraint!.Codec.Should().Be(expected: "h264");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 7: Variant matching all caps → selected, no transcode
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void VariantMatchingAllCaps_Selected_NoTranscode()
    {
        DeviceCapabilities caps = new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac"],
            VideoCodecs = ["h264"],
            MaxVideoHeight = 720,
            RamTier = DeviceRamTier.Standard,
        };

        VariantDescriptor[] variants =
        [
            V(index: 0, height: 1080, width: 1920, vCodec: "h264", bitrateKbps: 6000, audioChannels: 6, aCodec: "eac3"),
            V(index: 1, height: 720, width: 1280, vCodec: "h264", bitrateKbps: 3000, audioChannels: 2, aCodec: "aac"),
        ];

        VariantSelection sel = _selector.Select(variants: variants, caps: caps);

        sel.VariantIndex.Should().Be(expected: 1);
        sel.AudioConstraint.Should().BeNull();
        sel.VideoConstraint.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 8: Multiple matching variants → pick highest bitrate within RAM ceiling
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MultipleMatchingVariants_PicksHighestBitrateWithinCeiling()
    {
        DeviceCapabilities caps = new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac"],
            VideoCodecs = ["h264"],
            RamTier = DeviceRamTier.Standard, // ceiling = 8000 kbps
        };

        VariantDescriptor[] variants =
        [
            V(index: 0, height: 1080, width: 1920, vCodec: "h264", bitrateKbps: 7000, audioChannels: 2, aCodec: "aac"),
            V(index: 1, height: 720, width: 1280, vCodec: "h264", bitrateKbps: 4000, audioChannels: 2, aCodec: "aac"),
            V(index: 2, height: 480, width: 854, vCodec: "h264", bitrateKbps: 1500, audioChannels: 2, aCodec: "aac"),
        ];

        VariantSelection sel = _selector.Select(variants: variants, caps: caps);

        // Variant 0 at 7000 kbps is within 8000 kbps ceiling and is highest
        sel.VariantIndex.Should().Be(expected: 0);
        sel.AudioConstraint.Should().BeNull();
        sel.VideoConstraint.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 9: LowRam tier caps bitrate at 2000 kbps
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LowRamTier_CapsBitrateAt2000Kbps()
    {
        DeviceCapabilities caps = new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac"],
            VideoCodecs = ["h264"],
            RamTier = DeviceRamTier.LowRam, // ceiling = 2000 kbps
        };

        VariantDescriptor[] variants =
        [
            V(index: 0, height: 720, width: 1280, vCodec: "h264", bitrateKbps: 3000, audioChannels: 2, aCodec: "aac"), // above ceiling
            V(index: 1, height: 480, width: 854, vCodec: "h264", bitrateKbps: 1500, audioChannels: 2, aCodec: "aac"), // below ceiling
        ];

        VariantSelection sel = _selector.Select(variants: variants, caps: caps);

        sel.VariantIndex.Should().Be(expected: 1); // only variant within LowRam ceiling
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 10: HDR variant + HdrSupport=false → not filtered by selector
    //          (selector doesn't have HDR metadata on VariantDescriptor;
    //           height/codec filter is the guard here)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NonHdrCap_4KHevcFiltered_FallsBackToSdrH264()
    {
        DeviceCapabilities caps = new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac"],
            VideoCodecs = ["h264"], // no hevc
            MaxVideoHeight = 1080,
            HdrSupport = false,
            RamTier = DeviceRamTier.Standard,
        };

        VariantDescriptor[] variants =
        [
            V(index: 0, height: 2160, width: 3840, vCodec: "hevc", bitrateKbps: 15000, audioChannels: 6, aCodec: "eac3"), // filtered by codec+height
            V(index: 1, height: 1080, width: 1920, vCodec: "h264", bitrateKbps: 6000, audioChannels: 2, aCodec: "aac"), // matches all caps
        ];

        VariantSelection sel = _selector.Select(variants: variants, caps: caps);

        sel.VariantIndex.Should().Be(expected: 1);
        sel.AudioConstraint.Should().BeNull();
        sel.VideoConstraint.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 11: Reason is always set
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Reason_IsAlwaysNonEmpty()
    {
        VariantDescriptor[] variants = [V(index: 0, height: 1080, width: 1920, vCodec: "h264", bitrateKbps: 5000, audioChannels: 2, aCodec: "aac")];

        VariantSelection noCaps = _selector.Select(variants: variants, caps: null);
        noCaps.Reason.Should().NotBeNullOrEmpty();

        VariantSelection withCaps = _selector.Select(
            variants: variants,
            caps: new() { RamTier = DeviceRamTier.Standard }
        );
        withCaps.Reason.Should().NotBeNullOrEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 12: All variants above LowRam ceiling → no variant matches → transcode
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LowRamTier_AllVariantsAboveCeiling_SignalsTranscode_ReasonContainsLowRam()
    {
        DeviceCapabilities caps = new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac"],
            VideoCodecs = ["h264"],
            RamTier = DeviceRamTier.LowRam,
        };

        VariantDescriptor[] variants =
        [
            V(index: 0, height: 720, width: 1280, vCodec: "h264", bitrateKbps: 3000, audioChannels: 2, aCodec: "aac"), // 3000 > 2000 LowRam ceiling
            V(index: 1, height: 1080, width: 1920, vCodec: "h264", bitrateKbps: 6000, audioChannels: 2, aCodec: "aac"), // 6000 > 2000
        ];

        VariantSelection sel = _selector.Select(variants: variants, caps: caps);

        sel.VariantIndex.Should().BeNull();
        sel.Reason.Should().Contain(expected: "LowRam");
    }
}
