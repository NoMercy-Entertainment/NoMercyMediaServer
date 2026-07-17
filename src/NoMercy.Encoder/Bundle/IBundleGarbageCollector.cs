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

public interface IBundleGarbageCollector
{
    /// <summary>
    /// Walks <paramref name="libraryRoot"/> for every per-media-item
    /// <c>.nomercy.json</c> blueprint, checks each <c>encodes[]</c> entry
    /// against DB preset rows, and returns orphan entries whose preset no
    /// longer exists and should be reviewed for purge.
    /// </summary>
    Task<IReadOnlyList<BundleOrphan>> SweepAsync(string libraryRoot, CancellationToken ct);
}
