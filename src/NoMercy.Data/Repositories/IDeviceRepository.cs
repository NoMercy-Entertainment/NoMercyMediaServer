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

using Microsoft.EntityFrameworkCore.Query;
using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Repositories;

public interface IDeviceRepository
{
    IIncludableQueryable<Device, ICollection<ActivityLog>> GetDevices();

    Task AddDeviceAsync(Device device);

    Task DeleteDeviceAsync(Device device);

    Task<Device?> GetOwnerDeviceAsync(Ulid deviceId, Guid ownerUserId);

    Task<List<Device>> GetOwnerDevicesAsync(Guid ownerUserId);

    Task DeleteDeviceWithLogsAsync(Ulid deviceId);

    Task DeleteActivityLogsByOwnerAsync(Guid ownerUserId);

    Task<Device?> GetByIdAsync(Ulid deviceId);

    Task<List<Device>> GetAllAsync();

    Task DeleteAllActivityLogsAsync();
}
