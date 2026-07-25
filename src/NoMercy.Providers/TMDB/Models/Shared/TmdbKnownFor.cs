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

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbKnownFor
{
    [JsonProperty("poster_path")]
    public string PosterPath { get; set; } = string.Empty;

    [JsonProperty("adult")]
    public bool? Adult { get; set; }

    [JsonProperty("overview")]
    public string Overview { get; set; } = string.Empty;

    [JsonProperty("release_date")]
    public DateTime? ReleaseDate { get; set; }

    [JsonProperty("original_title")]
    public string OriginalTitle { get; set; } = string.Empty;

    [JsonProperty("genre_ids")]
    public int[] GenreIds { get; set; } = [];

    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("media_type")]
    public string MediaType { get; set; } = string.Empty;

    [JsonProperty("original_language")]
    public string OriginalLanguage { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("backdrop_path")]
    public string BackdropPath { get; set; } = string.Empty;

    [JsonProperty("popularity")]
    public double Popularity { get; set; }

    [JsonProperty("vote_count")]
    public int VoteCount { get; set; }

    [JsonProperty("video")]
    public bool? Video { get; set; }

    [JsonProperty("vote_average")]
    public float VoteAverage { get; set; }

    [JsonProperty("first_air_date")]
    public DateTime? FirstAirDate { get; set; }

    [JsonProperty("origin_country")]
    public string[] OriginCountry { get; set; } = [];

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("original_name")]
    public string OriginalName { get; set; } = string.Empty;
}
