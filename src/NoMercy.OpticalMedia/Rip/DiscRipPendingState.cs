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

using NoMercy.OpticalMedia.Metadata;

namespace NoMercy.OpticalMedia.Rip;

/// <summary>
/// Written as a JSON sidecar (<c>{outputDir}/pending_{titleIndex:D2}.json</c>)
/// when a rip completes but the top TMDB candidate's confidence falls below
/// the auto-apply threshold. The dashboard reads these files to surface
/// "awaiting manual confirmation" entries for the user to resolve.
/// </summary>
public sealed record DiscRipPendingState(
    string RipOutputPath,
    int TitleIndex,
    string DrivePath,
    int DiscDurationSec,
    DiscCandidate[] Candidates,
    DateTimeOffset CreatedAt
);
