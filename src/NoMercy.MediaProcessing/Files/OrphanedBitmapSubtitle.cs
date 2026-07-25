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

namespace NoMercy.MediaProcessing.Files;

/// <summary>
/// A preserved bitmap subtitle sidecar with no text sibling, queued for OCR
/// backfill. <see cref="MediaTitle"/> is the filename stem before
/// <c>.{Language}.{Variant}.{ext}</c>.
/// </summary>
public readonly record struct OrphanedBitmapSubtitle(
    string SupName,
    string MediaTitle,
    string Language,
    string Variant
);
