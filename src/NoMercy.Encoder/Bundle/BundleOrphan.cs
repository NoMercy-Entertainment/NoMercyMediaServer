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
/// Describes a blueprint <c>encodes[]</c> entry that is no longer backed by
/// a live preset row and should be reviewed for purge.
/// </summary>
public record BundleOrphan(
    /// <summary>The encode's output location, or the media folder holding
    /// the <c>.nomercy.json</c> blueprint when no output location was
    /// recorded.</summary>
    string Path,
    string PresetSlug,
    string PresetId,
    /// <summary>Why the entry is considered orphaned: currently always
    /// <c>"preset deleted"</c>.</summary>
    string Reason
);
