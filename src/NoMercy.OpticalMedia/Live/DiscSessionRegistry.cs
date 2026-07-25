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

namespace NoMercy.OpticalMedia.Live;

public class DiscSessionRegistry : IDiscSessionRegistry
{
    private readonly ConcurrentDictionary<string, string> _map = new(
        StringComparer.OrdinalIgnoreCase
    );

    public void Register(string drivePath, string sessionId) => _map[drivePath] = sessionId;

    public bool TryGet(string drivePath, out string sessionId) =>
        _map.TryGetValue(drivePath, out sessionId!);

    public void Remove(string drivePath) => _map.TryRemove(drivePath, out _);
}
