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
using NoMercy.Api.DTOs.Common;
using NoMercy.Database;

namespace NoMercy.Api.DTOs.Media;

public record RecommendationDto
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "link")]
    public Uri Link =>
        new(
            uriString: $"/dashboard/recommendations/{(Type != "movie" ? "tv" : "movie")}/{Id}",
            uriKind: UriKind.Relative
        );

    // Internal properties used for scoring/diversity — not serialized to JSON
    [JsonIgnore]
    public string? TitleSort { get; set; }

    [JsonIgnore]
    public string? Backdrop { get; set; }

    [JsonIgnore]
    public double Score { get; set; }

    [JsonIgnore]
    public int SourceCount { get; set; }

    [JsonIgnore]
    public List<int> SourceIds { get; set; } = [];
}

public record RecommendationDetailDto
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "media_type")]
    public string MediaType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "voteAverage")]
    public double? VoteAverage { get; set; }

    [JsonProperty(propertyName: "genres")]
    public IEnumerable<GenreDto> Genres { get; set; } = [];

    [JsonProperty(propertyName: "content_ratings")]
    public IEnumerable<ContentRating> ContentRatings { get; set; } = [];

    [JsonProperty(propertyName: "external_ids")]
    public ExternalIds? ExternalIds { get; set; }

    [JsonProperty(propertyName: "because_you_have")]
    public List<RecommendationDetailSourceDto> BecauseYouHave { get; set; } = [];

    [JsonProperty(propertyName: "link")]
    public Uri Link => new(uriString: $"/{MediaType}/{Id}", uriKind: UriKind.Relative);
}

public record RecommendationDetailSourceDto
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link => new(uriString: $"/{MediaType}/{Id}", uriKind: UriKind.Relative);

    [JsonProperty(propertyName: "media_type")]
    public string MediaType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "have_items")]
    public int HaveItems { get; set; }

    [JsonProperty(propertyName: "number_of_items")]
    public int NumberOfItems { get; set; }

    [JsonProperty(propertyName: "duration")]
    public int? Duration { get; set; }

    [JsonProperty(propertyName: "tags")]
    public IEnumerable<string> Tags { get; set; } = [];
}
