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

namespace NoMercy.Providers.AniList.Models;

public record AniListMedia
{
    [JsonProperty("id")]
    public int Id { get; set; }

    // AniList's own cross-reference to MyAnimeList - lets a Jikan lookup go
    // straight to /anime/{id}, which stays reliable even when Jikan's own
    // /anime search endpoint is hard-down (verified live: search 504s on
    // every query while by-id lookups return 200 for the same titles).
    [JsonProperty("idMal")]
    public int? IdMal { get; set; }

    [JsonProperty("title")]
    public AniListTitle Title { get; set; } = new();

    [JsonProperty("synonyms")]
    public string[] Synonyms { get; set; } = [];

    [JsonProperty("countryOfOrigin")]
    public string? CountryOfOrigin { get; set; }

    [JsonProperty("seasonYear")]
    public int? SeasonYear { get; set; }

    [JsonProperty("season")]
    public string? Season { get; set; }

    [JsonProperty("genres")]
    public string[] Genres { get; set; } = [];

    [JsonProperty("tags")]
    public AniListMediaTag[] Tags { get; set; } = [];
}
