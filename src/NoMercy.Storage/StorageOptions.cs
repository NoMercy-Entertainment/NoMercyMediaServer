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
/// Configuration for <see cref="LocalStorage"/>. When
/// <see cref="AllowedRoots"/> is empty the path guard runs in a
/// permissive mode (only structural checks: empty path, null bytes,
/// Windows device paths). Once consumers have finished migrating to
/// <see cref="IStorage"/> the host should populate
/// <see cref="AllowedRoots"/> with the union of library roots, output
/// roots, scratch dirs, etc.
/// </summary>
public sealed class StorageOptions
{
    public List<string> AllowedRoots { get; set; } = [];
}
