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
    ) => new(index, height, width, vCodec, bitrateKbps, audioChannels, aCodec);

    // ──────────────────────────────────────────────────────────────────────────
    // Test 1: No caps → variant 0, no constraints
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoCaps_ReturnsVariant0_NoConstraints()
    {
        VariantDescriptor[] variants =
        [
            V(0, 1080, 1920, "h264", 6000, 6, "eac3"),
            V(1, 720, 1280, "h264", 3000, 2, "aac"),
        ];

        VariantSelection sel = _selector.Select(variants, null);

        sel.VariantIndex.Should().Be(0);
        sel.AppliedCapabilities.Should().BeNull();
        sel.AudioConstraint.Should().BeNull();
        sel.VideoConstraint.Should().BeNull();
        sel.Reason.Should().Contain("no capabilities declared");
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
            V(0, 1080, 1920, "h264", 6000, 6, "eac3"),
            V(1, 720, 1280, "h264", 3000, 2, "aac"),
        ];

        VariantSelection sel = _selector.Select(variants, caps);

        sel.VariantIndex.Should().Be(0);
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
            V(0, 1080, 1920, "h264", 6000, 6, "eac3"),
            V(1, 1080, 1920, "h264", 4000, 6, "ac3"),
        ];

        VariantSelection sel = _selector.Select(variants, caps);

        sel.VariantIndex.Should().BeNull();
        sel.AudioConstraint.Should().Be(new AudioConstraint(2, "aac"));
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

        VariantDescriptor[] variants = [V(0, 720, 1280, "h264", 3000, 2, "opus")];

        VariantSelection sel = _selector.Select(variants, caps);

        sel.VariantIndex.Should().BeNull();
        sel.AudioConstraint.Should().NotBeNull();
        sel.AudioConstraint!.Codec.Should().Be("aac");
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

        VariantDescriptor[] variants = [V(0, 2160, 3840, "h264", 15000, 2, "aac")];

        VariantSelection sel = _selector.Select(variants, caps);

        sel.VariantIndex.Should().BeNull();
        sel.VideoConstraint.Should().NotBeNull();
        sel.VideoConstraint!.MaxHeight.Should().Be(1080);
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

        VariantDescriptor[] variants = [V(0, 1080, 1920, "hevc", 6000, 2, "aac")];

        VariantSelection sel = _selector.Select(variants, caps);

        sel.VariantIndex.Should().BeNull();
        sel.VideoConstraint.Should().NotBeNull();
        sel.VideoConstraint!.Codec.Should().Be("h264");
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
            V(0, 1080, 1920, "h264", 6000, 6, "eac3"),
            V(1, 720, 1280, "h264", 3000, 2, "aac"),
        ];

        VariantSelection sel = _selector.Select(variants, caps);

        sel.VariantIndex.Should().Be(1);
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
            V(0, 1080, 1920, "h264", 7000, 2, "aac"),
            V(1, 720, 1280, "h264", 4000, 2, "aac"),
            V(2, 480, 854, "h264", 1500, 2, "aac"),
        ];

        VariantSelection sel = _selector.Select(variants, caps);

        // Variant 0 at 7000 kbps is within 8000 kbps ceiling and is highest
        sel.VariantIndex.Should().Be(0);
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
            V(0, 720, 1280, "h264", 3000, 2, "aac"), // above ceiling
            V(1, 480, 854, "h264", 1500, 2, "aac"), // below ceiling
        ];

        VariantSelection sel = _selector.Select(variants, caps);

        sel.VariantIndex.Should().Be(1); // only variant within LowRam ceiling
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
            V(0, 2160, 3840, "hevc", 15000, 6, "eac3"), // filtered by codec+height
            V(1, 1080, 1920, "h264", 6000, 2, "aac"), // matches all caps
        ];

        VariantSelection sel = _selector.Select(variants, caps);

        sel.VariantIndex.Should().Be(1);
        sel.AudioConstraint.Should().BeNull();
        sel.VideoConstraint.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 11: Reason is always set
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Reason_IsAlwaysNonEmpty()
    {
        VariantDescriptor[] variants = [V(0, 1080, 1920, "h264", 5000, 2, "aac")];

        VariantSelection noCaps = _selector.Select(variants, null);
        noCaps.Reason.Should().NotBeNullOrEmpty();

        VariantSelection withCaps = _selector.Select(
            variants,
            new() { RamTier = DeviceRamTier.Standard }
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
            V(0, 720, 1280, "h264", 3000, 2, "aac"), // 3000 > 2000 LowRam ceiling
            V(1, 1080, 1920, "h264", 6000, 2, "aac"), // 6000 > 2000
        ];

        VariantSelection sel = _selector.Select(variants, caps);

        sel.VariantIndex.Should().BeNull();
        sel.Reason.Should().Contain("LowRam");
    }
}
