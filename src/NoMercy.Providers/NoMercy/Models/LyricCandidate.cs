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

using NoMercy.Providers.Abstractions;

namespace NoMercy.Providers.NoMercy.Models;

/// <summary>
/// A single lyrics result from any provider, reduced to the fields the matcher
/// needs to decide whether it belongs to the requested track.
/// </summary>
public sealed record LyricCandidate(
    string Title,
    string Artist,
    int? DurationSeconds,
    bool HasSyncedLyrics,
    LyricLine[] Lines
);
