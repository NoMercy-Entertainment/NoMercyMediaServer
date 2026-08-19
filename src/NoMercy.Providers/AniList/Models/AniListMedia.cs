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
