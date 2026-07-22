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

namespace NoMercy.Providers.TMDB.Models.Collections;

public class TmdbCollectionPart
{
    [JsonProperty(propertyName: "adult")]
    public bool Adult { get; set; }

    [JsonProperty(propertyName: "backdrop_path")]
    public object? BackdropPath { get; set; }

    [JsonProperty(propertyName: "genre_ids")]
    public int[] GenreIds { get; set; } = [];

    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "original_language")]
    public string? OriginalLanguage { get; set; }

    [JsonProperty(propertyName: "original_title")]
    public string OriginalTitle { get; set; } = string.Empty;

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "release_date")]
    public DateTime? ReleaseDate { get; set; }

    [JsonProperty(propertyName: "poster_path")]
    public string? PosterPath { get; set; }

    [JsonProperty(propertyName: "popularity")]
    public double? Popularity { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "video")]
    public bool? Video { get; set; }

    [JsonProperty(propertyName: "vote_average")]
    public double VoteAverage { get; set; }

    [JsonProperty(propertyName: "vote_count")]
    public int VoteCount { get; set; }
}
