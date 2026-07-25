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

using NoMercy.Api.Services.Music;
using NoMercy.Database.Models.Users;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Unit")]
public class MusicActiveDeviceRegistryTests
{
    private static Device MakeDevice(string deviceId)
    {
        return new() { DeviceId = deviceId, Type = "web" };
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenNoDeviceRegisteredForUser()
    {
        MusicActiveDeviceRegistry registry = new();

        bool found = registry.TryGet(Guid.NewGuid(), out Device? device);

        found.Should().BeFalse();
        device.Should().BeNull();
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsTheSameDevice()
    {
        MusicActiveDeviceRegistry registry = new();
        Guid userId = Guid.NewGuid();
        Device device = MakeDevice("device-a");

        registry.Set(userId, device);

        registry.TryGet(userId, out Device? found).Should().BeTrue();
        found.Should().BeSameAs(device);
    }

    [Fact]
    public void Set_Overwrites_PreviousActiveDeviceForSameUser()
    {
        MusicActiveDeviceRegistry registry = new();
        Guid userId = Guid.NewGuid();
        registry.Set(userId, MakeDevice("device-a"));

        Device replacement = MakeDevice("device-b");
        registry.Set(userId, replacement);

        registry.TryGet(userId, out Device? found).Should().BeTrue();
        found.Should().BeSameAs(replacement);
    }

    [Fact]
    public void Remove_ClearsTheActiveDevice_Unconditionally()
    {
        MusicActiveDeviceRegistry registry = new();
        Guid userId = Guid.NewGuid();
        registry.Set(userId, MakeDevice("device-a"));

        registry.Remove(userId);

        registry.TryGet(userId, out _).Should().BeFalse();
    }

    [Fact]
    public void RemoveIfMatches_RemovesEntry_WhenDeviceIdMatches()
    {
        MusicActiveDeviceRegistry registry = new();
        Guid userId = Guid.NewGuid();
        registry.Set(userId, MakeDevice("stale-device"));

        bool removed = registry.RemoveIfMatches(userId, "stale-device");

        removed.Should().BeTrue();
        registry.TryGet(userId, out _).Should().BeFalse();
    }

    [Fact]
    public void RemoveIfMatches_IsCaseInsensitive()
    {
        MusicActiveDeviceRegistry registry = new();
        Guid userId = Guid.NewGuid();
        registry.Set(userId, MakeDevice("Stale-Device"));

        bool removed = registry.RemoveIfMatches(userId, "stale-device");

        removed.Should().BeTrue();
    }

    [Fact]
    public void RemoveIfMatches_DoesNotClobberADeviceSwitchThatRacedIn()
    {
        // Simulates a liveness sweep reading the active device as "old-device",
        // then losing a race to ChangeDeviceCommand, which promotes "new-device"
        // before the sweep's RemoveIfMatches actually runs. The switch must win.
        MusicActiveDeviceRegistry registry = new();
        Guid userId = Guid.NewGuid();
        registry.Set(userId, MakeDevice("old-device"));

        registry.Set(userId, MakeDevice("new-device"));

        bool removed = registry.RemoveIfMatches(userId, "old-device");

        removed.Should().BeFalse();
        registry.TryGet(userId, out Device? found).Should().BeTrue();
        found!.DeviceId.Should().Be("new-device");
    }

    [Fact]
    public void RemoveIfMatches_ReturnsFalse_WhenNoEntryExists()
    {
        MusicActiveDeviceRegistry registry = new();

        bool removed = registry.RemoveIfMatches(Guid.NewGuid(), "anything");

        removed.Should().BeFalse();
    }

    [Fact]
    public void RemoveIfMatches_ReturnsFalse_WhenDeviceIdDoesNotMatchCurrentActive()
    {
        MusicActiveDeviceRegistry registry = new();
        Guid userId = Guid.NewGuid();
        registry.Set(userId, MakeDevice("device-a"));

        bool removed = registry.RemoveIfMatches(userId, "device-b");

        removed.Should().BeFalse();
        registry.TryGet(userId, out Device? found).Should().BeTrue();
        found!.DeviceId.Should().Be("device-a");
    }
}
