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
using NoMercy.Storage;

namespace NoMercy.Data.Resolvers;

/// <summary>
/// <see cref="IDriverConfigResolver"/> backed by <see cref="MediaContext"/>.
/// Performs a synchronous DB lookup — acceptable because this is called at most
/// once per (folderId, driverId) pair due to StorageFactory caching.
/// </summary>
public class DriverConfigResolver(IDbContextFactory<MediaContext> contextFactory)
    : IDriverConfigResolver
{
    public (string Type, string? ConfigJson)? Resolve(Ulid driverId)
    {
        using MediaContext context = contextFactory.CreateDbContext();
        Driver? driver = context.Drivers.AsNoTracking().FirstOrDefault(d => d.Id == driverId);

        if (driver is null)
            return null;

        return (driver.Type, driver.Config);
    }
}
