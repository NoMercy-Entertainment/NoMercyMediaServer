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

using NoMercy.Storage;

namespace NoMercy.Encoder.Bundle;

public interface IBundleManifestWriter
{
    /// <summary>
    /// Writes <paramref name="manifest"/> to <paramref name="path"/> on
    /// <paramref name="storage"/>. Storage is a per-call parameter (not
    /// constructor-injected) so a caller's folder-scoped
    /// <c>EncodingContext.DestinationStorage</c> is honoured — matches
    /// <see cref="IReconstructionWriter"/>'s established pattern.
    /// </summary>
    Task WriteAsync(IStorage storage, string path, BundleManifest manifest, CancellationToken ct);

    Task<BundleManifest?> ReadAsync(IStorage storage, string path, CancellationToken ct);

    Task<ReconcileReport> ReconcileAsync(
        IStorage storage,
        string bundleDirectory,
        BundleManifest manifest,
        CancellationToken ct
    );
}

public record ReconcileReport(IReadOnlyList<string> ExtraFiles, IReadOnlyList<string> MissingFiles);
