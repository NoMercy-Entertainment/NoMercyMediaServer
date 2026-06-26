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
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovie : TmdbBase
{
    [JsonProperty("adult")]
    public bool Adult { get; set; }

    [JsonProperty("genres")]
    public int[]? GenresIds { get; set; } = [];

    [JsonProperty("original_title")]
    public string OriginalTitle { get; set; } = string.Empty;

    [JsonProperty("tagline")]
    public string? Tagline { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("release_date")]
    public DateTime? ReleaseDate { get; set; }

    [JsonProperty("video")]
    public bool? Video { get; set; }
}
