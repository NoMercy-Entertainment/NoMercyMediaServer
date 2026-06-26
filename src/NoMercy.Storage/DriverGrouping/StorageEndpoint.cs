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

namespace NoMercy.Storage.DriverGrouping;

/// <summary>
/// Identifies the logical storage endpoint a folder root belongs to.
/// Used to ensure folders on different endpoints are never merged into
/// one driver.
///
/// Examples:
///   UNC share  → Key = "\\192.168.1.1\Media"
///   Win drive  → Key = "C:"
///   POSIX      → Key = "/" (or mount root)
/// </summary>
public sealed record StorageEndpoint(string Key, StorageEndpointKind Kind);
