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

using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Services.Music;

/// <summary>
/// Pure per-device volume decisions for MusicHub, kept free of SignalR/EF plumbing
/// so they're unit-testable without a live hub context. Volume is owned by the
/// device, not the session: the scoped MusicPlayerState.VolumePercentage always
/// mirrors whichever device is currently active; every other device's level lives
/// only in DeviceVolumes until that device becomes active itself.
/// </summary>
public static class MusicVolumeResolver
{
    public static int Clamp(int volume) => Math.Clamp(value: volume, min: 0, max: 100);

    public static bool IsTargetActive(string targetDeviceId, string? activeDeviceId) =>
        activeDeviceId is not null
        && activeDeviceId.Equals(value: targetDeviceId, comparisonType: StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Applies an already-clamped volume from SetDeviceVolumeCommand/ChangeVolumeCommand.
    /// Always stamps the target device's DeviceVolumes entry. Only mirrors onto the
    /// scoped VolumePercentage when the target IS the active device — a passive device
    /// tuning its own level, or a passive controller tuning the active device's level,
    /// must never cross-contaminate the other device's stored volume.
    /// </summary>
    public static void ApplyDeviceVolume(
        MusicPlayerState state,
        string targetDeviceId,
        string? activeDeviceId,
        int clampedVolume
    )
    {
        state.DeviceVolumes[key: targetDeviceId] = clampedVolume;

        if (IsTargetActive(targetDeviceId: targetDeviceId, activeDeviceId: activeDeviceId))
            state.VolumePercentage = clampedVolume;
    }

    /// <summary>
    /// Resolves the level a device transferred TO (via ChangeDeviceCommand) should
    /// play at. Prefers a level already remembered for it in DeviceVolumes (set by a
    /// prior SetDeviceVolumeCommand or an earlier activation), then the device's own
    /// persisted VolumePercent (e.g. a TV reporting its last hardware level on
    /// connect), then the shared safe default. Never reads the outgoing active
    /// device's level — the caller must not pass it in.
    /// </summary>
    public static int ResolveTransferVolume(
        MusicPlayerState state,
        string targetDeviceId,
        int? targetPersistedVolume
    )
    {
        if (state.DeviceVolumes.TryGetValue(key: targetDeviceId, value: out int remembered))
            return remembered;

        return targetPersistedVolume ?? Device.DefaultVolumePercent;
    }
}
