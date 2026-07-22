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

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonCredit
{
    [JsonProperty(propertyName: "adult")]
    public bool Adult { get; set; }

    [JsonProperty(propertyName: "backdrop_path")]
    public string BackdropPath { get; set; } = string.Empty;

    [JsonProperty(propertyName: "character")]
    public string? Character { get; set; }

    [JsonProperty(propertyName: "credit_id")]
    public string CreditId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "department")]
    public string Department { get; set; } = string.Empty;

    [JsonProperty(propertyName: "episode_count")]
    public int EpisodeCount { get; set; }

    [JsonProperty(propertyName: "first_air_date")]
    public DateTime? FirstAirDate { get; set; }

    [JsonProperty(propertyName: "genre_ids")]
    public int[] GenreIds { get; set; } = [];

    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "job")]
    public string? Job { get; set; }

    [JsonProperty(propertyName: "media_type")]
    public string MediaType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "order")]
    public int Order { get; set; }

    [JsonProperty(propertyName: "origin_country")]
    public string[] OriginCountry { get; set; } = [];

    [JsonProperty(propertyName: "original_language")]
    public string OriginalLanguage { get; set; } = string.Empty;

    [JsonProperty(propertyName: "original_name")]
    public string OriginalName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "original_title")]
    public string OriginalTitle { get; set; } = string.Empty;

    [JsonProperty(propertyName: "overview")]
    public string Overview { get; set; } = string.Empty;

    [JsonProperty(propertyName: "popularity")]
    public double Popularity { get; set; }

    [JsonProperty(propertyName: "poster_path")]
    public string PosterPath { get; set; } = string.Empty;

    [JsonProperty(propertyName: "release_date")]
    public DateTime? ReleaseDate { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "video")]
    public bool Video { get; set; }

    [JsonProperty(propertyName: "vote_average")]
    public double VoteAverage { get; set; }

    [JsonProperty(propertyName: "vote_count")]
    public int VoteCount { get; set; }
}
