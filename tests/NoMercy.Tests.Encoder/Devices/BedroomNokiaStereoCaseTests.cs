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

/// <summary>
/// Explicit regression file for the bedroom Nokia stereo-only TV.
/// Nokia box is Android TV, stereo speakers only, no HDR, 1080p max, LowRam tier.
/// Source content is typically 5.1 or 7.1 with eac3/ac3 audio.
/// </summary>
public class BedroomNokiaStereoCaseTests
{
    private readonly DeviceAwareVariantSelector _selector = new();

    [Fact]
    public void NokiaBedroomTv_5_1Source_StereoOnly_TranscodeSignal()
    {
        DeviceCapabilities nokia = new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac", "ac3"],
            VideoCodecs = ["h264", "hevc"],
            MaxVideoHeight = 1080,
            HdrSupport = false,
            RamTier = DeviceRamTier.LowRam,
            Notes = "Nokia bedroom TV — stereo speaker only",
        };

        VariantDescriptor[] variants =
        [
            new(0, 2160, 3840, "hevc", 12000, 6, "eac3"),
            new(1, 1080, 1920, "h264", 6000, 6, "ac3"),
            new(2, 720, 1280, "h264", 3000, 2, "aac"),
        ];

        VariantSelection sel = _selector.Select(variants, nokia);

        // Variant 2 is stereo + aac + h264 + 720p — passes audio/codec/height filters.
        // But variant 2 is 3000 kbps. LowRam ceiling is 2000 kbps. So no variant fits → transcode.
        sel.VariantIndex.Should().BeNull();
        sel.AudioConstraint.Should().Be(new AudioConstraint(2, "aac"));
        sel.VideoConstraint.Should().NotBeNull();
        sel.VideoConstraint!.MaxHeight.Should().Be(1080);
        sel.VideoConstraint.Codec.Should().BeOneOf("h264", "hevc");
        sel.Reason.Should().Contain("LowRam");
    }

    [Fact]
    public void NokiaBedroomTv_StereoVariantExistsBelowRamCeiling_NoTranscode()
    {
        DeviceCapabilities nokia = new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac"],
            VideoCodecs = ["h264"],
            MaxVideoHeight = 1080,
            RamTier = DeviceRamTier.LowRam,
        };

        VariantDescriptor[] variants =
        [
            new(0, 1080, 1920, "h264", 6000, 6, "eac3"),
            new(1, 720, 1280, "h264", 1500, 2, "aac"),
        ];

        VariantSelection sel = _selector.Select(variants, nokia);

        // Variant 1 is 1500 kbps — below LowRam ceiling (2000 kbps), stereo aac h264 720p
        sel.VariantIndex.Should().Be(1);
        sel.AudioConstraint.Should().BeNull();
        sel.VideoConstraint.Should().BeNull();
    }

    [Fact]
    public void NokiaBedroomTv_NoVariantsAtAll_HandledGracefully()
    {
        DeviceCapabilities nokia = new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac"],
            VideoCodecs = ["h264"],
            RamTier = DeviceRamTier.LowRam,
        };

        VariantSelection sel = _selector.Select([], nokia);

        sel.VariantIndex.Should().BeNull();
        sel.Reason.Should().Contain("no variants available");
    }

    [Fact]
    public void NokiaBedroomTv_CapabilitiesRecord_HasExpectedDefaults_ForNewField()
    {
        DeviceCapabilities nokia = new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac", "ac3"],
            VideoCodecs = ["h264", "hevc"],
            MaxVideoHeight = 1080,
            RamTier = DeviceRamTier.LowRam,
        };

        nokia.HdrSupport.Should().BeFalse();
        nokia.DolbyVision.Should().Be(DolbyVisionProfile.None);
        nokia.PlayerBufferCapMb.Should().BeNull();
    }
}
