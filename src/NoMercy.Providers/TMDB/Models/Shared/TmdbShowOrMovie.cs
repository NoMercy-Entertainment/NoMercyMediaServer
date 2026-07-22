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
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbShowOrMovie : TmdbBase
{
    [JsonProperty(propertyName: "adult")]
    public bool? Adult { get; set; }

    [JsonProperty(propertyName: "genres")]
    public int[]? GenresIds { get; set; } = [];

    [JsonProperty(propertyName: "original_title")]
    public string? OriginalTitle { get; set; }

    [JsonProperty(propertyName: "tagline")]
    public string? Tagline { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "release_date")]
    public DateTime? ReleaseDate { get; set; }

    [JsonProperty(propertyName: "video")]
    public bool? Video { get; set; }

    [JsonProperty(propertyName: "first_air_date")]
    public DateTime? FirstAirDate { get; set; }

    [JsonProperty(propertyName: "genre_ids")]
    public int?[] GenreIds { get; set; } = [];

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "origin_country")]
    public string?[] OriginCountry { get; set; } = [];

    [JsonProperty(propertyName: "original_name")]
    public string? OriginalName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string? MediaType { get; set; } = string.Empty;

    public TmdbShowOrMovie(TmdbMovie movie)
    {
        Id = movie.Id;
        OriginalLanguage = movie.OriginalLanguage;
        Overview = movie.Overview;
        Popularity = movie.Popularity;
        PosterPath = movie.PosterPath;
        VoteAverage = movie.VoteAverage;
        VoteCount = movie.VoteCount;
        Adult = movie.Adult;
        GenresIds = movie.GenresIds;
        OriginalTitle = movie.OriginalTitle;
        Tagline = movie.Tagline;
        Title = movie.Title;
        ReleaseDate = movie.ReleaseDate;
        Video = movie.Video;
    }

    public TmdbShowOrMovie(TmdbTvShow show)
    {
        Id = show.Id;
        OriginalLanguage = show.OriginalLanguage;
        Overview = show.Overview;
        Popularity = show.Popularity;
        PosterPath = show.PosterPath;
        VoteAverage = show.VoteAverage;
        VoteCount = show.VoteCount;
        FirstAirDate = show.FirstAirDate;
        GenreIds = show.GenreIds;
        Name = show.Name;
        OriginCountry = show.OriginCountry;
        OriginalName = show.OriginalName;
        MediaType = show.MediaType;
    }
}
