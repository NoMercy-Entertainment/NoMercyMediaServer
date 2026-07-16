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
/// Redirects for renamed built-ins. A built-in's id is a hash of its name, so
/// renaming one mints a new id and leaves every folder linked to the old id
/// pointing at a preset that no longer ships.
/// </summary>
public static class BuiltinPresetRenames
{
    /// <summary>
    /// <c>oldId → newId</c>, applied by <see cref="BuiltinPresetSeeder"/> before
    /// it prunes built-ins that no longer ship. Add an entry whenever a built-in
    /// is renamed and the new one is a fair substitute for the old.
    ///
    /// Leaving a rename out is safe but lossy in a different way: the seeder
    /// keeps the retired preset as a user preset instead of redirecting to the
    /// replacement. Either way a folder link is never dropped on the floor.
    /// </summary>
    public static readonly IReadOnlyDictionary<Ulid, Ulid> IdRedirects =
        new Dictionary<Ulid, Ulid>();

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
