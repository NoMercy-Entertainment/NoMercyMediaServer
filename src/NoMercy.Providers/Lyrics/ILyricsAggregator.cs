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

using NoMercy.Database.Models.Music;
using NoMercy.Providers.Abstractions;

namespace NoMercy.Providers.Lyrics;

/// <summary>
/// Resolves a track's lyrics across providers (Lrclib preferred, Musixmatch
/// fallback), validating every candidate against the requested track.
/// </summary>
public interface ILyricsAggregator
{
    Task<LyricLine[]?> SearchLyrics(Track track);
}
