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

using Newtonsoft.Json;

namespace NoMercy.Encoder.Bundle;

/// <summary>
/// Lean identity block: enough to tie the media item to the official TMDB
/// record and let a human disambiguate a title+year collision. No overview,
/// cast, artwork, or full TMDB response — see spec Part 1.
/// </summary>
public record BlueprintIdentity(
    /// <summary>"movie" | "episode".</summary>
    [property: JsonProperty("type")] string Type,
    [property: JsonProperty("tmdb_id")] long TmdbId,
    /// <summary>Present for episodes only; null for movies.</summary>
    [property: JsonProperty("show")] BlueprintShow? Show,
    /// <summary>Present for episodes only; null for movies.</summary>
    [property: JsonProperty("season")] int? Season,
    /// <summary>Present for episodes only; null for movies.</summary>
    [property: JsonProperty("episode")] int? Episode,
    [property: JsonProperty("title")] string Title,
    [property: JsonProperty("year")] int? Year
);
