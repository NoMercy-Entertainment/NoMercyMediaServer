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

using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Service.Hosting;

public static class BootstrapStorageFactory
{
    public static (IStorage storage, IStorageDriver driver) Create()
    {
        IStorageDriver driver = new LocalStorageDriver();
        StoragePathGuard guard = new(allowedRoots: [], driver: driver);
        IStorage storage = new LocalStorage(driver: driver, guard: guard);
        
        return (storage, driver);
    }
}
