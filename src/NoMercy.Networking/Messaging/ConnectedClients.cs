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
using NoMercy.Networking.Http;

namespace NoMercy.Networking.Messaging;

public class ConnectedClients
{
    public ConcurrentDictionary<string, Client> Clients { get; } = new();

    /// <summary>
    /// How many live hub connections each device currently holds.
    /// </summary>
    /// <remarks>
    /// Every hub in the app derives from the same base, so one client opens five or six of
    /// them at once. <see cref="Clients"/> is keyed by connection and cannot answer "is this
    /// device already here" without a race: the hubs connect within milliseconds of each
    /// other, and each one reads the dictionary before any of them has written to it.
    /// </remarks>
    private readonly ConcurrentDictionary<string, int> _connectionsPerDevice = new();

    /// <summary>
    /// Registers one hub connection for a device and reports whether the device has just
    /// arrived — that is, whether this is its only connection.
    /// </summary>
    public bool RegisterDeviceConnection(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
            return false;

        return _connectionsPerDevice.AddOrUpdate(deviceId, 1, (_, count) => count + 1) == 1;
    }

    /// <summary>
    /// Releases one hub connection for a device and reports whether the device has actually
    /// left — that is, whether that was the last one.
    /// </summary>
    public bool ReleaseDeviceConnection(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
            return false;

        int remaining = _connectionsPerDevice.AddOrUpdate(deviceId, 0, (_, count) => count - 1);

        if (remaining > 0)
            return false;

        // Nobody left holding it, so stop tracking it. A negative count would mean more
        // releases than registrations, which is still a departure as far as callers care.
        _connectionsPerDevice.TryRemove(deviceId, out int _);
        return true;
    }
}
