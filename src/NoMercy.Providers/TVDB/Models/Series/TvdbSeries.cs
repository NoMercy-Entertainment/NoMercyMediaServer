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
using NoMercy.Providers.TVDB.Models.Artwork;
using NoMercy.Providers.TVDB.Models.Awards;
using NoMercy.Providers.TVDB.Models.Characters;
using NoMercy.Providers.TVDB.Models.Companies;
using NoMercy.Providers.TVDB.Models.ContentRatings;
using NoMercy.Providers.TVDB.Models.Episodes;
using NoMercy.Providers.TVDB.Models.Genres;
using NoMercy.Providers.TVDB.Models.Lists;
using NoMercy.Providers.TVDB.Models.Seasons;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Series;

public class TvdbSeriesResponse : TvdbResponse<TvdbSeries> { }

public class TvdbSeriesExtendedResponse : TvdbResponse<TvdbSeriesExtended> { }

public class TvdbSeriesEpisodesResponse : TvdbResponse<TvdbSeriesEpisodes> { }

public class TvdbSeriesStatusesResponse : TvdbResponse<TvdbStatus[]> { }

public class TvdbSeriesTranslationResponse : TvdbResponse<TvdbTranslationData> { }

public class TvdbNextAiredResponse : TvdbResponse<TvdbSeries> { }

public class TvdbSeries
{
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty(propertyName: "image")]
    public Uri? Image { get; set; }

    [JsonProperty(propertyName: "abbreviation")]
    public string? Abbreviation { get; set; }

    [JsonProperty(propertyName: "country")]
    public string? Country { get; set; }

    [JsonProperty(propertyName: "defaultSeasonType")]
    public int DefaultSeasonType { get; set; }

    [JsonProperty(propertyName: "episodes")]
    public TvdbEpisode[]? Episodes { get; set; }

    [JsonProperty(propertyName: "firstAired")]
    public string? FirstAired { get; set; }

    [JsonProperty(propertyName: "lastAired")]
    public string? LastAired { get; set; }

    [JsonProperty(propertyName: "nextAired")]
    public string? NextAired { get; set; }

    [JsonProperty(propertyName: "originalCountry")]
    public string? OriginalCountry { get; set; }

    [JsonProperty(propertyName: "originalLanguage")]
    public string? OriginalLanguage { get; set; }

    [JsonProperty(propertyName: "originalNetwork")]
    public TvdbCompany? OriginalNetwork { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "score")]
    public double Score { get; set; }

    [JsonProperty(propertyName: "status")]
    public TvdbStatus? Status { get; set; }

    [JsonProperty(propertyName: "year")]
    public string? Year { get; set; }

    [JsonProperty(propertyName: "nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty(propertyName: "overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty(propertyName: "aliases")]
    public TvdbAlias[] Aliases { get; set; } = [];

    [JsonProperty(propertyName: "averageRuntime")]
    public int? AverageRuntime { get; set; }

    [JsonProperty(propertyName: "isOrderRandomized")]
    public bool IsOrderRandomized { get; set; }

    [JsonProperty(propertyName: "lastUpdated")]
    public DateTimeOffset? LastUpdated { get; set; }
}

public class TvdbSeriesExtended : TvdbSeries
{
    [JsonProperty(propertyName: "artworks")]
    public TvdbArtwork[]? Artworks { get; set; }

    [JsonProperty(propertyName: "airsDays")]
    public TvdbAirsDays? AirsDays { get; set; }

    [JsonProperty(propertyName: "airsTime")]
    public string? AirsTime { get; set; }

    [JsonProperty(propertyName: "awards")]
    public TvdbAward[]? Awards { get; set; }

    [JsonProperty(propertyName: "characters")]
    public TvdbCharacter[]? Characters { get; set; }

    [JsonProperty(propertyName: "companies")]
    public TvdbCompany[]? Companies { get; set; }

    [JsonProperty(propertyName: "contentRatings")]
    public TvdbContentRating[]? ContentRatings { get; set; }

    [JsonProperty(propertyName: "genres")]
    public TvdbGenre[]? Genres { get; set; }

    [JsonProperty(propertyName: "latestNetwork")]
    public TvdbCompany? LatestNetwork { get; set; }

    [JsonProperty(propertyName: "lists")]
    public TvdbList[]? Lists { get; set; }

    [JsonProperty(propertyName: "networks")]
    public TvdbCompany[]? Networks { get; set; }

    [JsonProperty(propertyName: "remoteIds")]
    public TvdbRemoteId[]? RemoteIds { get; set; }

    [JsonProperty(propertyName: "seasons")]
    public TvdbSeason[]? Seasons { get; set; }

    [JsonProperty(propertyName: "seasonTypes")]
    public TvdbSeasonType[]? SeasonTypes { get; set; }

    [JsonProperty(propertyName: "studios")]
    public TvdbCompany[]? Studios { get; set; }

    [JsonProperty(propertyName: "tags")]
    public TvdbTagOption[]? Tags { get; set; }

    [JsonProperty(propertyName: "trailers")]
    public TvdbTrailer[]? Trailers { get; set; }

    [JsonProperty(propertyName: "translations")]
    public TvdbTranslations? Translations { get; set; }
}

public class TvdbAirsDays
{
    [JsonProperty(propertyName: "monday")]
    public bool Monday { get; set; }

    [JsonProperty(propertyName: "tuesday")]
    public bool Tuesday { get; set; }

    [JsonProperty(propertyName: "wednesday")]
    public bool Wednesday { get; set; }

    [JsonProperty(propertyName: "thursday")]
    public bool Thursday { get; set; }

    [JsonProperty(propertyName: "friday")]
    public bool Friday { get; set; }

    [JsonProperty(propertyName: "saturday")]
    public bool Saturday { get; set; }

    [JsonProperty(propertyName: "sunday")]
    public bool Sunday { get; set; }
}

public class TvdbSeriesEpisodes
{
    [JsonProperty(propertyName: "series")]
    public TvdbSeries? Series { get; set; }

    [JsonProperty(propertyName: "episodes")]
    public TvdbEpisode[] Episodes { get; set; } = [];
}
