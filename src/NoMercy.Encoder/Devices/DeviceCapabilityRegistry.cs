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
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Encoder.Devices;

public class DeviceCapabilityRegistry(IDbContextFactory<MediaContext> contextFactory)
    : IDeviceCapabilityRegistry
{
    private readonly ConcurrentDictionary<string, DeviceCapabilities> _cache = new();

    public DeviceCapabilities? Get(string deviceId) =>
        _cache.TryGetValue(deviceId, out DeviceCapabilities? caps) ? caps : null;

    public void Set(string deviceId, DeviceCapabilities capabilities) =>
        _cache[deviceId] = capabilities;

    public void Invalidate(string deviceId) => _cache.TryRemove(deviceId, out _);

    public async Task<DeviceCapabilities?> LoadFromDbAsync(string deviceId, CancellationToken ct)
    {
        await using MediaContext ctx = await contextFactory.CreateDbContextAsync(ct);
        Device? device = await ctx.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);
        if (device?.CapabilitiesJson is null)
            return null;

        DeviceCapabilities? caps = JsonConvert.DeserializeObject<DeviceCapabilities>(
            device.CapabilitiesJson
        );
        if (caps is not null)
            _cache[deviceId] = caps;
        return caps;
    }
}
