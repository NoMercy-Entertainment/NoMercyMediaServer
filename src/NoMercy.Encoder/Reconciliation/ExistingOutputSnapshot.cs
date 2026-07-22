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

namespace NoMercy.Encoder.Reconciliation;

/// <summary>
/// What the reconciler found already on disk for a previously-encoded file.
/// <see cref="ProfileFingerprint"/> is null for every output produced before
/// fingerprinting shipped — that is treated as "same profile, unknown
/// version" rather than "profile changed", so upgrading the server never
/// forces a full re-encode of an existing library on its own.
/// </summary>
public sealed record ExistingOutputSnapshot(
    string? ProfileFingerprint,
    IReadOnlyCollection<ExistingOutputEntry> BundleFiles,
    int ValidOcrSidecarCount
)
{
    public static readonly ExistingOutputSnapshot Empty = new(ProfileFingerprint: null, BundleFiles: [], ValidOcrSidecarCount: 0);
}
