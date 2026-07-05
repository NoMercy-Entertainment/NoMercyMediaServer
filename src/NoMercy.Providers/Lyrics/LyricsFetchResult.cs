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

namespace NoMercy.Providers.Lyrics;

/// <summary>
/// Outcome of a lyrics resolve. Distinguishes a confirmed "no lyrics anywhere"
/// (every provider was queried cleanly and none matched -- safe to permanently
/// negative-cache) from a transient failure (timeout, rate limit, unexpected
/// error on any provider stage -- must NOT be cached the same way, since the
/// track was never actually checked).
/// </summary>
public sealed record LyricsFetchResult(LyricLine[]? Lines, bool IsTransientError, string? Winner)
{
    public static readonly LyricsFetchResult NotFound = new(null, false, null);

    public static readonly LyricsFetchResult TransientFailure = new(null, true, null);

    public static LyricsFetchResult Found(LyricLine[] lines, string winner) =>
        new(lines, false, winner);
}
