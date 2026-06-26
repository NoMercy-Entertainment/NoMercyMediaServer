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

namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// Reads the raw CD Table of Contents from a drive.
/// Returns null when the TOC cannot be read (no disc, unsupported platform,
/// or read error), causing identification to degrade to
/// <see cref="DiscIdentification.NeedsManualAssignment"/>.
/// </summary>
public interface ITocReader
{
    Task<DiscToc?> ReadTocAsync(string drivePath, CancellationToken ct);
}
