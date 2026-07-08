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

using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Encoder.Storage;

/// <summary>
/// Builds a permissive <see cref="LocalStorage"/> for tests that need a
/// real filesystem but don't care about the path-allowlist enforcement.
/// </summary>
internal static class TestStorageFactory
{
    public static LocalStorage CreateLocal()
    {
        LocalStorageDriver driver = new();
        return new(driver, new([], driver));
    }
}
