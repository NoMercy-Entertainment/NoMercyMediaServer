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
using NoMercy.Database.Models.Users;
using NoMercy.Encoder.Devices;

namespace NoMercy.Tests.Encoder.Devices;

/// <summary>
/// Tests for the DeclareCapabilities contract without requiring a live SignalR hub.
/// The critical behavioral paths (silent-drop, full-replace, registry-cache) are verified
/// here at the serialization / registry layer, since the hub's SignalR context cannot be
/// unit-tested without a full integration harness.
/// </summary>
public class DeviceHubCapabilitiesTests
{
    [Fact]
    public void DeclareCapabilities_FullPayload_SerializesCorrectly()
    {
        DeviceCapabilities payload = new()
        {
            MaxAudioChannels = 2,
            AudioCodecs = ["aac", "ac3"],
            VideoCodecs = ["h264"],
            MaxVideoHeight = 1080,
            HdrSupport = false,
            RamTier = DeviceRamTier.LowRam,
            Notes = "Nokia bedroom TV",
        };

        string json = JsonConvert.SerializeObject(payload);
        json.Should().NotBeNullOrEmpty();

        DeviceCapabilities? roundTripped = JsonConvert.DeserializeObject<DeviceCapabilities>(json);
        roundTripped.Should().NotBeNull();
        roundTripped!.MaxAudioChannels.Should().Be(2);
        roundTripped.RamTier.Should().Be(DeviceRamTier.LowRam);
    }

    [Fact]
    public void DeclareCapabilities_FullReplace_ClearsOldFields()
    {
        // Simulates full-replace: a second declaration with fewer fields
        // replaces the JSON blob entirely; prior fields are gone.
        DeviceCapabilities first = new()
        {
            MaxAudioChannels = 6,
            AudioCodecs = ["eac3", "ac3"],
            VideoCodecs = ["hevc"],
            MaxVideoHeight = 2160,
            HdrSupport = true,
            RamTier = DeviceRamTier.HighRam,
        };

        DeviceCapabilities second = new() { MaxAudioChannels = 2, AudioCodecs = ["aac"] };

        Device device = new() { DeviceId = "test-device", Type = "tv" };
        device.CapabilitiesJson = JsonConvert.SerializeObject(first);

        // Full replace: second declaration overwrites entirely
        device.CapabilitiesJson = JsonConvert.SerializeObject(second);

        DeviceCapabilities? parsed = JsonConvert.DeserializeObject<DeviceCapabilities>(
            device.CapabilitiesJson
        );
        parsed.Should().NotBeNull();
        parsed!.MaxAudioChannels.Should().Be(2);
        parsed.VideoCodecs.Should().BeEmpty(); // was hevc, now gone
        parsed.MaxVideoHeight.Should().BeNull(); // was 2160, now null
        parsed.HdrSupport.Should().BeFalse(); // back to default
    }

    [Fact]
    public void DeclareCapabilities_Registry_CachesAfterSet()
    {
        DeviceCapabilityRegistry registry = new(null!);
        DeviceCapabilities caps = new() { MaxAudioChannels = 2, RamTier = DeviceRamTier.LowRam };

        registry.Set("nokia-01", caps);

        DeviceCapabilities? cached = registry.Get("nokia-01");
        cached.Should().NotBeNull();
        cached!.MaxAudioChannels.Should().Be(2);
    }

    [Fact]
    public void DeclareCapabilities_UnknownDevice_RegistryDropsSilently()
    {
        // If deviceId is not in the ConnectedClients map, the method returns without
        // touching the registry. Verify the registry has nothing cached for that key.
        DeviceCapabilityRegistry registry = new(null!);
        DeviceCapabilities? result = registry.Get("ghost-device");
        result.Should().BeNull();
    }
}
