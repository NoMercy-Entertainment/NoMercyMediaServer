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

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Maps old built-in preset identifiers to new ones when a preset is renamed.
/// Each entry carries both a Ulid redirect (kept in DB) and an optional slug
/// redirect (used by <see cref="BundleSlugRenamer"/> to rename on-disk bundle
/// directories and patch their manifest.json files).
/// </summary>
public static class BuiltinPresetRenames
{
    /// <summary>
    /// Ulid redirect map. Applied by <see cref="BuiltinPresetSeeder"/> so that
    /// existing DB rows survive a built-in rename without losing history.
    /// </summary>
    public static readonly IReadOnlyDictionary<Ulid, Ulid> V1ToV2 = new Dictionary<Ulid, Ulid>();

    /// <summary>
    /// Slug redirect map (<c>oldSlug → newSlug</c>). Consumed by
    /// <see cref="BundleSlugRenamer"/> on startup to rename
    /// <c>encodes/{oldSlug}/</c> directories and patch <c>manifest.json</c>
    /// files found inside them.
    ///
    /// Add an entry here whenever a built-in preset's display name (and
    /// therefore its computed slug) changes between releases.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> SlugRenames = new Dictionary<
        string,
        string
    >(StringComparer.Ordinal);
}
