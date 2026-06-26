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

namespace NoMercy.Encoder.Bundle;

/// <summary>
/// Describes a bundle directory that is no longer backed by a live preset
/// row (or has a structural anomaly) and should be reviewed for purge.
/// </summary>
public record BundleOrphan(
    /// <summary>Bundle directory path relative to the library root.</summary>
    string Path,
    string PresetSlug,
    string PresetId,
    /// <summary>
    /// Why the bundle is considered orphaned:
    /// "preset deleted" | "extra-files" | "missing-files" | "duplicate-manifest"
    /// </summary>
    string Reason
);
