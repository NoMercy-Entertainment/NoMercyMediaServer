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

namespace NoMercy.Tests.Plugins;

internal static class TestStorageHelper
{
    public static IStorageDriver CreateBackend() => new LocalStorageDriver();

    public static IStorage CreateStorage(string rootPath)
    {
        IStorageDriver driver = CreateBackend();
        return new LocalStorage(driver, new([rootPath], driver));
    }
}
