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

using NoMercy.Database.Models.Storage;

namespace NoMercy.Data.Repositories;

public interface IDriverRepository
{
    Task<List<Driver>> GetAllDriversAsync();

    Task<Driver?> GetDriverByIdAsync(Ulid id);

    Task<bool> DriverExistsAsync(Ulid id);

    Task<bool> NameExistsAsync(string name, Ulid? excludeId = null);

    Task<int> LibraryFolderCountAsync(Ulid driverId);

    Task<Driver> CreateDriverAsync(Driver driver);

    Task<Driver> UpdateDriverAsync(Driver driver);

    Task<int> DeleteDriverAsync(Driver driver);
}
