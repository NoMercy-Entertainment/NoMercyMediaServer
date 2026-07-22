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

[Trait(name: "Category", value: "Unit")]
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

        bool found = registry.TryGet(userId: Guid.NewGuid(), device: out Device? device);

        found.Should().BeFalse();
        device.Should().BeNull();
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsTheSameDevice()
    {
        MusicActiveDeviceRegistry registry = new();
        Guid userId = Guid.NewGuid();
        Device device = MakeDevice(deviceId: "device-a");

        registry.Set(userId: userId, device: device);

        registry.TryGet(userId: userId, device: out Device? found).Should().BeTrue();
        found.Should().BeSameAs(expected: device);
    }

    [Fact]
    public void Set_Overwrites_PreviousActiveDeviceForSameUser()
    {
        MusicActiveDeviceRegistry registry = new();
        Guid userId = Guid.NewGuid();
        registry.Set(userId: userId, device: MakeDevice(deviceId: "device-a"));

        Device replacement = MakeDevice(deviceId: "device-b");
        registry.Set(userId: userId, device: replacement);

        registry.TryGet(userId: userId, device: out Device? found).Should().BeTrue();
        found.Should().BeSameAs(expected: replacement);
    }

    [Fact]
    public void Remove_ClearsTheActiveDevice_Unconditionally()
    {
        MusicActiveDeviceRegistry registry = new();
        Guid userId = Guid.NewGuid();
        registry.Set(userId: userId, device: MakeDevice(deviceId: "device-a"));

        registry.Remove(userId: userId);

        registry.TryGet(userId: userId, device: out _).Should().BeFalse();
    }

    [Fact]
    public void RemoveIfMatches_RemovesEntry_WhenDeviceIdMatches()
    {
        MusicActiveDeviceRegistry registry = new();
        Guid userId = Guid.NewGuid();
        registry.Set(userId: userId, device: MakeDevice(deviceId: "stale-device"));

        bool removed = registry.RemoveIfMatches(userId: userId, deviceId: "stale-device");

        removed.Should().BeTrue();
        registry.TryGet(userId: userId, device: out _).Should().BeFalse();
    }

    [Fact]
    public void RemoveIfMatches_IsCaseInsensitive()
    {
        MusicActiveDeviceRegistry registry = new();
        Guid userId = Guid.NewGuid();
        registry.Set(userId: userId, device: MakeDevice(deviceId: "Stale-Device"));

        bool removed = registry.RemoveIfMatches(userId: userId, deviceId: "stale-device");

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
        registry.Set(userId: userId, device: MakeDevice(deviceId: "old-device"));

        registry.Set(userId: userId, device: MakeDevice(deviceId: "new-device"));

        bool removed = registry.RemoveIfMatches(userId: userId, deviceId: "old-device");

        removed.Should().BeFalse();
        registry.TryGet(userId: userId, device: out Device? found).Should().BeTrue();
        found!.DeviceId.Should().Be(expected: "new-device");
    }

    [Fact]
    public void RemoveIfMatches_ReturnsFalse_WhenNoEntryExists()
    {
        MusicActiveDeviceRegistry registry = new();

        bool removed = registry.RemoveIfMatches(userId: Guid.NewGuid(), deviceId: "anything");

        removed.Should().BeFalse();
    }

    [Fact]
    public void RemoveIfMatches_ReturnsFalse_WhenDeviceIdDoesNotMatchCurrentActive()
    {
        MusicActiveDeviceRegistry registry = new();
        Guid userId = Guid.NewGuid();
        registry.Set(userId: userId, device: MakeDevice(deviceId: "device-a"));

        bool removed = registry.RemoveIfMatches(userId: userId, deviceId: "device-b");

        removed.Should().BeFalse();
        registry.TryGet(userId: userId, device: out Device? found).Should().BeTrue();
        found!.DeviceId.Should().Be(expected: "device-a");
    }
}
