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

namespace NoMercy.Storage.Factory;

/// <summary>
/// Builds an <see cref="IStorage"/> for a single driver type (or family of
/// types). Drivers are registered as DI services keyed implicitly by
/// <see cref="SupportedTypes"/>; adding a new driver requires only registering
/// a new <see cref="IStorageDriverBuilder"/>, never editing StorageFactory.
/// </summary>
public interface IStorageDriverBuilder
{
    /// <summary>Driver-type keys this builder handles (e.g. ["s3", "r2"]).</summary>
    IReadOnlyCollection<string> SupportedTypes { get; }

    IStorage Build(Ulid folderId, string driverType, string? driverConfigJson, string subPath);
}
