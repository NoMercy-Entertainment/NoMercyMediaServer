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

namespace NoMercy.Providers.NoMercy.Models;

/// <summary>
/// Normalized description of the local track we want lyrics for. Drives the
/// match scoring so a provider result is only accepted when it really is the
/// same song and (for synced lyrics) the same release length.
/// </summary>
public sealed record LyricQuery(
    string Title,
    IReadOnlyList<string> Artists,
    string? Album,
    int? DurationSeconds
);
