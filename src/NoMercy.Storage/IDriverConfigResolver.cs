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

namespace NoMercy.Storage;

/// <summary>
/// Resolves a driver instance by its ID to its type and JSON config.
/// Implemented in a higher-level project that has DB access;
/// injected into <see cref="IStorageFactory"/> at DI registration time.
/// </summary>
public interface IDriverConfigResolver
{
    /// <summary>
    /// Returns (type, configJson) for the given <paramref name="driverId"/>,
    /// or <c>null</c> if no Driver row exists with that ID.
    /// </summary>
    (string Type, string? ConfigJson)? Resolve(Ulid driverId);
}
