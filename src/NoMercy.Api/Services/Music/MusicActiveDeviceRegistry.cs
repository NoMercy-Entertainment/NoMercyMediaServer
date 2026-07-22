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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Services.Music;

/// <summary>
/// Single source of truth for "which Device is the active MusicHub player for a
/// given user". Previously MusicHub kept this in a private static dict while
/// MusicPlayerState.DeviceId carried a second, independently-mutated copy of the
/// same fact — every new call site had to remember to update both (ChangeDeviceCommand
/// already needed a patch for exactly that divergence). Both MusicHub (promotion,
/// device switch) and MusicPlaybackService (liveness-triggered release) depend on
/// this registry instead, so there is exactly one place that can go stale.
/// </summary>
public class MusicActiveDeviceRegistry
{
    private readonly ConcurrentDictionary<Guid, Device> _current = new();

    public bool TryGet(Guid userId, [NotNullWhen(returnValue: true)] out Device? device)
    {
        return _current.TryGetValue(key: userId, value: out device);
    }

    public void Set(Guid userId, Device device)
    {
        _current[key: userId] = device;
    }

    public void Remove(Guid userId)
    {
        _current.TryRemove(key: userId, value: out _);
    }

    /// <summary>
    /// Removes the recorded active device only if it still matches deviceId — an
    /// atomic compare-and-remove so a liveness-triggered release can never clobber
    /// a device switch that raced in between the staleness check and this call.
    /// </summary>
    public bool RemoveIfMatches(Guid userId, string deviceId)
    {
        if (
            _current.TryGetValue(key: userId, value: out Device? existing)
            && existing.DeviceId.Equals(value: deviceId, comparisonType: StringComparison.OrdinalIgnoreCase)
        )
            return _current.TryRemove(item: new(key: userId, value: existing));

        return false;
    }
}
