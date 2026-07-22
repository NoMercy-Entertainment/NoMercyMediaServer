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

/// <summary>
/// Pins the per-device volume contract MusicHub.Devices.cs delegates to:
/// SetDeviceVolumeCommand/ChangeVolumeCommand route through
/// <see cref="MusicVolumeResolver.ApplyDeviceVolume"/>, ChangeDeviceCommand routes
/// through <see cref="MusicVolumeResolver.ResolveTransferVolume"/>. Exercising the
/// resolver directly (rather than standing up a full SignalR hub context) covers
/// the real decision logic the hub methods call — the hub methods themselves are
/// thin wiring around these two entry points.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class MusicVolumeResolverTests
{
    [Theory]
    [InlineData(data: [-10, 0])]
    [InlineData(data: [0, 0])]
    [InlineData(data: [55, 55])]
    [InlineData(data: [150, 100])]
    public void Clamp_ClampsToZeroHundred(int input, int expected)
    {
        MusicVolumeResolver.Clamp(volume: input).Should().Be(expected: expected);
    }

    [Fact]
    public void IsTargetActive_True_WhenTargetIsTheActiveDevice()
    {
        MusicVolumeResolver.IsTargetActive(targetDeviceId: "tv-1", activeDeviceId: "tv-1").Should().BeTrue();
    }

    [Fact]
    public void IsTargetActive_IsCaseInsensitive()
    {
        MusicVolumeResolver.IsTargetActive(targetDeviceId: "TV-1", activeDeviceId: "tv-1").Should().BeTrue();
    }

    [Fact]
    public void IsTargetActive_False_WhenTargetIsAnotherDevice()
    {
        MusicVolumeResolver.IsTargetActive(targetDeviceId: "phone-1", activeDeviceId: "tv-1").Should().BeFalse();
    }

    [Fact]
    public void IsTargetActive_False_WhenThereIsNoActiveDeviceYet()
    {
        MusicVolumeResolver.IsTargetActive(targetDeviceId: "phone-1", activeDeviceId: null).Should().BeFalse();
    }

    [Fact]
    public void ApplyDeviceVolume_PassiveCallerTargetingActiveDevice_MirrorsOntoVolumePercentage()
    {
        // Phone (passive) drives the TV's (active) slider — the scoped
        // volume_percentage the active device reads must move.
        MusicPlayerState state = new() { DeviceId = "tv-1", VolumePercentage = 20 };

        MusicVolumeResolver.ApplyDeviceVolume(
            state: state,
            targetDeviceId: "tv-1",
            activeDeviceId: "tv-1",
            clampedVolume: 80
        );

        state.VolumePercentage.Should().Be(expected: 80);
        state.DeviceVolumes[key: "tv-1"].Should().Be(expected: 80);
    }

    [Fact]
    public void ApplyDeviceVolume_CallerTargetingOwnPassiveDevice_DoesNotTouchVolumePercentage()
    {
        // Phone (passive) sets its OWN slider while the TV stays active — the
        // scoped volume_percentage belongs to the TV and must not move.
        MusicPlayerState state = new() { DeviceId = "tv-1", VolumePercentage = 20 };

        MusicVolumeResolver.ApplyDeviceVolume(
            state: state,
            targetDeviceId: "phone-1",
            activeDeviceId: "tv-1",
            clampedVolume: 80
        );

        state.VolumePercentage.Should().Be(expected: 20, because: "the active device's mirrored volume is untouched");
        state
            .DeviceVolumes[key: "phone-1"]
            .Should()
            .Be(expected: 80, because: "the passive device's own remembered level still updates");
    }

    [Fact]
    public void ApplyDeviceVolume_AlwaysStampsTheDeviceVolumesEntry_RegardlessOfActiveStatus()
    {
        MusicPlayerState state = new() { DeviceId = "tv-1" };

        MusicVolumeResolver.ApplyDeviceVolume(state: state, targetDeviceId: "tv-1", activeDeviceId: "tv-1", clampedVolume: 10);
        MusicVolumeResolver.ApplyDeviceVolume(state: state, targetDeviceId: "phone-1", activeDeviceId: "tv-1", clampedVolume: 90);

        state.DeviceVolumes.Should().ContainKey(expected: "tv-1").WhoseValue.Should().Be(expected: 10);
        state.DeviceVolumes.Should().ContainKey(expected: "phone-1").WhoseValue.Should().Be(expected: 90);
    }

    [Fact]
    public void ResolveTransferVolume_PrefersARememberedDeviceVolume()
    {
        MusicPlayerState state = new() { VolumePercentage = 5 };
        state.DeviceVolumes[key: "phone-1"] = 65;

        int resolved = MusicVolumeResolver.ResolveTransferVolume(
            state: state,
            targetDeviceId: "phone-1",
            targetPersistedVolume: 30
        );

        resolved.Should().Be(expected: 65);
    }

    [Fact]
    public void ResolveTransferVolume_FallsBackToPersistedVolume_WhenNoneRemembered()
    {
        MusicPlayerState state = new() { VolumePercentage = 5 };

        int resolved = MusicVolumeResolver.ResolveTransferVolume(
            state: state,
            targetDeviceId: "phone-1",
            targetPersistedVolume: 30
        );

        resolved.Should().Be(expected: 30);
    }

    [Fact]
    public void ResolveTransferVolume_FallsBackToSharedDefault_WhenDeviceHasNeverReported()
    {
        MusicPlayerState state = new() { VolumePercentage = 5 };

        int resolved = MusicVolumeResolver.ResolveTransferVolume(
            state: state,
            targetDeviceId: "phone-1",
            targetPersistedVolume: null
        );

        resolved.Should().Be(expected: Device.DefaultVolumePercent);
    }

    [Fact]
    public void ResolveTransferVolume_NeverInheritsTheOutgoingActiveDevicesLevel()
    {
        // The outgoing active device (tv-1) is at 95. The target (phone-1) has
        // never reported a level and isn't remembered. It must land on the
        // shared default, never on tv-1's 95 — transfer never inherits.
        MusicPlayerState state = new() { DeviceId = "tv-1", VolumePercentage = 95 };

        int resolved = MusicVolumeResolver.ResolveTransferVolume(
            state: state,
            targetDeviceId: "phone-1",
            targetPersistedVolume: null
        );

        resolved.Should().Be(expected: Device.DefaultVolumePercent);
        resolved.Should().NotBe(unexpected: 95);
    }
}
