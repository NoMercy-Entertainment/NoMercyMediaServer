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

namespace NoMercy.Providers.KitsuIo;

public class Attributes
{
    [JsonProperty(propertyName: "createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty(propertyName: "updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonProperty(propertyName: "slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty(propertyName: "synopsis")]
    public string Synopsis { get; set; } = string.Empty;

    [JsonProperty(propertyName: "description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty(propertyName: "coverImageTopOffset")]
    public int CoverImageTopOffset { get; set; }

    [JsonProperty(propertyName: "titles")]
    public Titles Titles { get; set; } = new();

    [JsonProperty(propertyName: "canonicalTitle")]
    public string CanonicalTitle { get; set; } = string.Empty;

    [JsonProperty(propertyName: "abbreviatedTitles")]
    public string[] AbbreviatedTitles { get; set; } = [];

    [JsonProperty(propertyName: "averageRating")]
    public string? AverageRating { get; set; }

    [JsonProperty(propertyName: "ratingFrequencies")]
    public Dictionary<string, int> RatingFrequencies { get; set; } = new();

    [JsonProperty(propertyName: "userCount")]
    public int UserCount { get; set; }

    [JsonProperty(propertyName: "favoritesCount")]
    public int? FavoritesCount { get; set; }

    [JsonProperty(propertyName: "startDate")]
    public DateTime? StartDate { get; set; }

    [JsonProperty(propertyName: "endDate")]
    public DateTime? EndDate { get; set; }

    [JsonProperty(propertyName: "nextRelease")]
    public object? NextRelease { get; set; }

    [JsonProperty(propertyName: "popularityRank")]
    public int? PopularityRank { get; set; }

    [JsonProperty(propertyName: "ratingRank")]
    public int? RatingRank { get; set; }

    [JsonProperty(propertyName: "ageRating")]
    public string? AgeRating { get; set; }

    [JsonProperty(propertyName: "ageRatingGuide")]
    public string AgeRatingGuide { get; set; } = string.Empty;

    [JsonProperty(propertyName: "subtype")]
    public string Subtype { get; set; } = string.Empty;

    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty(propertyName: "tba")]
    public string? Tba { get; set; }

    [JsonProperty(propertyName: "posterImage")]
    public PosterImage? PosterImage { get; set; }

    [JsonProperty(propertyName: "coverImage")]
    public CoverImage? CoverImage { get; set; }

    [JsonProperty(propertyName: "episodeCount")]
    public int? EpisodeCount { get; set; }

    [JsonProperty(propertyName: "episodeLength")]
    public int? EpisodeLength { get; set; }

    [JsonProperty(propertyName: "totalLength")]
    public int? TotalLength { get; set; }

    [JsonProperty(propertyName: "youtubeVideoId")]
    public string? YoutubeVideoId { get; set; }

    [JsonProperty(propertyName: "showType")]
    public string? ShowType { get; set; }

    [JsonProperty(propertyName: "nsfw")]
    public bool Nsfw { get; set; }
}
