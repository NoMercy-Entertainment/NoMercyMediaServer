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
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Services.Video;

public class VideoDeviceManager
{
    private readonly ConcurrentDictionary<Guid, Device> _currentDevices = new();
    private readonly MediaContext _mediaContext;

    public VideoDeviceManager(MediaContext mediaContext)
    {
        _mediaContext = mediaContext;
    }

    public Device? GetUserDevice(Guid userId)
    {
        return _currentDevices.TryGetValue(userId, out Device? device) ? device : null;
    }

    public void SetUserDevice(Guid userId, Device device)
    {
        _currentDevices.AddOrUpdate(userId, device, (_, _) => device);
    }

    public bool RemoveUserDevice(Guid userId)
    {
        return _currentDevices.TryRemove(userId, out _);
    }

    public async Task UpdateDeviceVolume(string deviceId, int volume)
    {
        await _mediaContext
            .Devices.Where(d => d.DeviceId == deviceId)
            .ExecuteUpdateAsync(d => d.SetProperty(x => x.VolumePercent, volume));
    }
}
