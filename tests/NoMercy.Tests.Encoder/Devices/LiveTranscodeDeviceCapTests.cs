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
using NoMercy.Encoder.Devices;
using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Tests.Encoder.Devices;

/// <summary>
/// Tests for DeviceAwareVariantSelector.ApplyDeviceCaps — the bridge between
/// declared DeviceCapabilities and the ClientCapabilities used by LiveFfmpegRunner.
/// </summary>
public class LiveTranscodeDeviceCapTests
{
    private readonly DeviceAwareVariantSelector _selector = new();

    private static ClientCapabilities MakeHiFiClient() =>
        new(
            [VideoCodecType.H264, VideoCodecType.H265],
            [],
            [],
            3840,
            2160,
            true,
            true,
            20000,
            8
        );

    private static ClientCapabilities MakeStereoClient() =>
        new(
            [VideoCodecType.H264],
            [],
            [],
            1920,
            1080,
            false,
            false,
            6000,
            2
        );

    // ──────────────────────────────────────────────────────────────────────────
    // Test 1: No device caps → client caps unchanged
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoCaps_ClientCapsUnchanged()
    {
        ClientCapabilities client = MakeHiFiClient();
        ClientCapabilities result = _selector.ApplyDeviceCaps(client, null);

        result.MaxAudioChannels.Should().Be(8);
        result.MaxHeight.Should().Be(2160);
        result.SupportsHdr.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 2: Stereo TV + 5.1 client → channels clamped to 2
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void StereoTv_FiveOneClient_ChannelsClampedTo2()
    {
        ClientCapabilities client = MakeHiFiClient();
        DeviceCapabilities nokiaCaps = new()
        {
            MaxAudioChannels = 2,
            RamTier = DeviceRamTier.LowRam,
        };

        ClientCapabilities result = _selector.ApplyDeviceCaps(client, nokiaCaps);

        result.MaxAudioChannels.Should().Be(2);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 3: HiFi phone + 5.1 source → no audio constraint
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HiFiPhone_FiveOneSource_NoAudioConstraint()
    {
        ClientCapabilities client = MakeHiFiClient();
        DeviceCapabilities phoneCaps = new()
        {
            MaxAudioChannels = 8,
            AudioCodecs = ["aac", "eac3", "truehd"],
            RamTier = DeviceRamTier.HighRam,
        };

        ClientCapabilities result = _selector.ApplyDeviceCaps(client, phoneCaps);

        result.MaxAudioChannels.Should().Be(8);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 4: HDR client + non-HDR device → HDR disabled
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HdrClient_NonHdrDevice_HdrDisabled()
    {
        ClientCapabilities client = MakeHiFiClient(); // SupportsHdr = true
        DeviceCapabilities deviceCaps = new() { HdrSupport = false };

        ClientCapabilities result = _selector.ApplyDeviceCaps(client, deviceCaps);

        result.SupportsHdr.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 5: Device height < client height → more restrictive height wins
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DeviceHeightLower_MoreRestrictiveWins()
    {
        ClientCapabilities client = MakeHiFiClient(); // MaxHeight = 2160
        DeviceCapabilities deviceCaps = new() { MaxVideoHeight = 1080 };

        ClientCapabilities result = _selector.ApplyDeviceCaps(client, deviceCaps);

        result.MaxHeight.Should().Be(1080);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 6: Device channels > client channels → client (lower) wins
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DeviceChannelsHigher_ClientChannelsWin()
    {
        ClientCapabilities client = MakeStereoClient(); // MaxAudioChannels = 2
        DeviceCapabilities deviceCaps = new() { MaxAudioChannels = 6 };

        ClientCapabilities result = _selector.ApplyDeviceCaps(client, deviceCaps);

        result.MaxAudioChannels.Should().Be(2); // client is more restrictive
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 7: Nokia bedroom TV full case — all constraints applied
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NokiaBedroomTv_AllConstraintsApplied()
    {
        ClientCapabilities client = MakeHiFiClient();
        DeviceCapabilities nokia = new()
        {
            MaxAudioChannels = 2,
            VideoCodecs = ["h264", "hevc"],
            MaxVideoHeight = 1080,
            HdrSupport = false,
            RamTier = DeviceRamTier.LowRam,
        };

        ClientCapabilities result = _selector.ApplyDeviceCaps(client, nokia);

        result.MaxAudioChannels.Should().Be(2);
        result.MaxHeight.Should().Be(1080);
        result.SupportsHdr.Should().BeFalse();
    }
}
