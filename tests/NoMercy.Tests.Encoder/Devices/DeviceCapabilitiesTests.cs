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

using Newtonsoft.Json;
using NoMercy.Encoder.Devices;

namespace NoMercy.Tests.Encoder.Devices;

public class DeviceCapabilitiesTests
{
    [Fact]
    public void DefaultConstructed_HasExpectedDefaults()
    {
        DeviceCapabilities caps = new();

        caps.MaxAudioChannels.Should().BeNull();
        caps.AudioCodecs.Should().BeEmpty();
        caps.VideoCodecs.Should().BeEmpty();
        caps.MaxVideoHeight.Should().BeNull();
        caps.HdrSupport.Should().BeFalse();
        caps.DolbyVision.Should().Be(expected: DolbyVisionProfile.None);
        caps.RamTier.Should().Be(expected: DeviceRamTier.Standard);
        caps.PlayerBufferCapMb.Should().BeNull();
        caps.Notes.Should().BeNull();
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllFields()
    {
        DeviceCapabilities original = new()
        {
            MaxAudioChannels = 6,
            AudioCodecs = ["aac", "ac3", "eac3"],
            VideoCodecs = ["h264", "hevc"],
            MaxVideoHeight = 2160,
            HdrSupport = true,
            DolbyVision = DolbyVisionProfile.Profile81,
            RamTier = DeviceRamTier.HighRam,
            PlayerBufferCapMb = 500,
            Notes = "flagship phone",
        };

        string json = JsonConvert.SerializeObject(value: original);
        DeviceCapabilities? restored = JsonConvert.DeserializeObject<DeviceCapabilities>(value: json);

        restored.Should().NotBeNull();
        restored!.MaxAudioChannels.Should().Be(expected: 6);
        restored.AudioCodecs.Should().BeEquivalentTo(expectation: ["aac", "ac3", "eac3"]);
        restored.VideoCodecs.Should().BeEquivalentTo(expectation: ["h264", "hevc"]);
        restored.MaxVideoHeight.Should().Be(expected: 2160);
        restored.HdrSupport.Should().BeTrue();
        restored.DolbyVision.Should().Be(expected: DolbyVisionProfile.Profile81);
        restored.RamTier.Should().Be(expected: DeviceRamTier.HighRam);
        restored.PlayerBufferCapMb.Should().Be(expected: 500);
        restored.Notes.Should().Be(expected: "flagship phone");
    }

    [Fact]
    public void JsonRoundTrip_NullNotes_Preserved()
    {
        DeviceCapabilities original = new() { MaxAudioChannels = 2 };
        string json = JsonConvert.SerializeObject(value: original);
        DeviceCapabilities? restored = JsonConvert.DeserializeObject<DeviceCapabilities>(value: json);

        restored.Should().NotBeNull();
        restored!.Notes.Should().BeNull();
        restored.MaxAudioChannels.Should().Be(expected: 2);
    }

    [Fact]
    public void SettingCapabilitiesJson_WritesAndReadsBack()
    {
        DeviceCapabilities caps = new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac"],
            RamTier = DeviceRamTier.LowRam,
            Notes = "Nokia bedroom TV — stereo speaker only",
        };

        string json = JsonConvert.SerializeObject(value: caps);
        json.Should().NotBeNullOrEmpty();

        DeviceCapabilities? parsed = JsonConvert.DeserializeObject<DeviceCapabilities>(value: json);
        parsed.Should().NotBeNull();
        parsed!.MaxAudioChannels.Should().Be(expected: 2);
        parsed.AudioCodecs.Should().BeEquivalentTo(expectation: ["aac"]);
        parsed.RamTier.Should().Be(expected: DeviceRamTier.LowRam);
        parsed.Notes.Should().Be(expected: "Nokia bedroom TV — stereo speaker only");
    }

    [Fact]
    public void NullJson_DeserializesToNull()
    {
        DeviceCapabilities? result = JsonConvert.DeserializeObject<DeviceCapabilities>(value: "null");
        result.Should().BeNull();
    }
}
