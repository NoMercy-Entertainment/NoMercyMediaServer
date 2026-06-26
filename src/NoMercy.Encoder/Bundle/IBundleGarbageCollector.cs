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
    /// Walks <paramref name="libraryRoot"/> for <c>encodes/*/manifest.json</c>
    /// files, reconciles each manifest against DB preset rows and on-disk files,
    /// and returns orphan bundles that should be reviewed for purge.
    /// </summary>
    Task<IReadOnlyList<BundleOrphan>> SweepAsync(string libraryRoot, CancellationToken ct);
}
