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

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Repositories;

public class DeviceRepository(MediaContext context) : IDeviceRepository
{
    public async Task<List<Device>> GetDevices()
    {
        return await context.Devices.Include(device => device.ActivityLogs).ToListAsync();
    }

    public async Task AddDeviceAsync(Device device)
    {
        await context.Devices.AddAsync(device);
        await context.SaveChangesAsync();
    }

    public Task DeleteDeviceAsync(Device device)
    {
        context.Devices.Remove(device);
        return context.SaveChangesAsync();
    }

    public async Task<Device?> GetOwnerDeviceAsync(Ulid deviceId, Guid ownerUserId)
    {
        return await context
            .Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.OwnerUserId == ownerUserId);
    }

    public async Task<List<Device>> GetOwnerDevicesAsync(Guid ownerUserId)
    {
        return await context
            .Devices.AsNoTracking()
            .Where(d => d.OwnerUserId == ownerUserId)
            .ToListAsync();
    }

    public Task DeleteDeviceWithLogsAsync(Ulid deviceId)
    {
        // ActivityLog.DeviceId → Device is configured with DeleteBehavior.Cascade
        // in MediaContext.OnModelCreating, so the DB removes dependent rows
        // atomically when the device row is deleted.  No app-level child sweep needed.
        return context.Devices.Where(d => d.Id == deviceId).ExecuteDeleteAsync();
    }

    public Task DeleteActivityLogsByOwnerAsync(Guid ownerUserId)
    {
        // Deletes ActivityLog rows whose Device is owned by the caller.
        // Does NOT delete the Device rows themselves — only clears the log history.
        return context
            .ActivityLogs.Where(log => log.Device.OwnerUserId == ownerUserId)
            .ExecuteDeleteAsync();
    }

    public async Task<Device?> GetByIdAsync(Ulid deviceId)
    {
        return await context.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId);
    }

    public async Task<List<Device>> GetAllAsync()
    {
        return await context.Devices.AsNoTracking().ToListAsync();
    }

    public Task DeleteAllActivityLogsAsync()
    {
        // Server-admin action: clear the entire device activity-log history.
        return context.ActivityLogs.ExecuteDeleteAsync();
    }
}
