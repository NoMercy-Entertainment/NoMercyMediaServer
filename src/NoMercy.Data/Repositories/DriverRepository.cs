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
using NoMercy.Database.Models.Storage;

namespace NoMercy.Data.Repositories;

public class DriverRepository(MediaContext context) : IDriverRepository
{
    public Task<List<Driver>> GetAllDriversAsync()
    {
        return context
            .Drivers.AsNoTracking()
            .Include(d => d.Folders)
            .OrderBy(d => d.Name)
            .ThenBy(d => d.Id)
            .ToListAsync();
    }

    public Task<Driver?> GetDriverByIdAsync(Ulid id)
    {
        return context
            .Drivers.AsNoTracking()
            .Include(d => d.Folders)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public Task<bool> DriverExistsAsync(Ulid id)
    {
        return context.Drivers.AnyAsync(d => d.Id == id);
    }

    public Task<bool> NameExistsAsync(string name, Ulid? excludeId = null)
    {
        return context.Drivers.AnyAsync(d =>
            d.Name == name && (excludeId == null || d.Id != excludeId.Value)
        );
    }

    public Task<int> LibraryFolderCountAsync(Ulid driverId)
    {
        return context.Folders.CountAsync(f =>
            f.DriverId == driverId && f.FolderLibraries.Count > 0
        );
    }

    public async Task<Driver> CreateDriverAsync(Driver driver)
    {
        context.Drivers.Add(driver);
        await context.SaveChangesAsync();
        return driver;
    }

    public async Task<Driver> UpdateDriverAsync(Driver driver)
    {
        context.Drivers.Update(driver);
        await context.SaveChangesAsync();
        return driver;
    }

    public async Task<int> DeleteDriverAsync(Driver driver)
    {
        await context
            .Folders.Where(f => f.DriverId == driver.Id && f.FolderLibraries.Count == 0)
            .ExecuteDeleteAsync();

        return await context.Drivers.Where(d => d.Id == driver.Id).ExecuteDeleteAsync();
    }
}
