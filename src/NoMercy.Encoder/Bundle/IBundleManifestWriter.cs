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

public interface IBundleManifestWriter
{
    Task WriteAsync(string path, BundleManifest manifest, CancellationToken ct);

    Task<BundleManifest?> ReadAsync(string path, CancellationToken ct);

    Task<ReconcileReport> ReconcileAsync(
        string bundleDirectory,
        BundleManifest manifest,
        CancellationToken ct
    );
}

public record ReconcileReport(IReadOnlyList<string> ExtraFiles, IReadOnlyList<string> MissingFiles);
