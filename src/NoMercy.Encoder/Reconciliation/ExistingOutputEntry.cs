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
/// One file the reconciler found on disk (or in <c>manifest.json</c>),
/// relative to the encode bundle directory. Validity is a cheap
/// existence + non-zero-size check — never a per-segment re-probe of an
/// HLS playlist's referenced segments.
/// </summary>
public sealed record ExistingOutputEntry(string RelativePath, long SizeBytes)
{
    public bool IsValid => SizeBytes > 0;
}
