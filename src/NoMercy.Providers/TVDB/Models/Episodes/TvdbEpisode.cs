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
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Episodes;

public class TvdbEpisodeResponse : TvdbResponse<TvdbEpisode> { }

public class TvdbEpisodeExtendedResponse : TvdbResponse<TvdbEpisodeExtended> { }

public class TvdbEpisodeTranslationResponse : TvdbResponse<TvdbTranslationData> { }

public class TvdbEpisode
{
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "seriesId")]
    public long SeriesId { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "aired")]
    public string? Aired { get; set; }

    [JsonProperty(propertyName: "runtime")]
    public int? Runtime { get; set; }

    [JsonProperty(propertyName: "nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty(propertyName: "overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "image")]
    public Uri? Image { get; set; }

    [JsonProperty(propertyName: "imageType")]
    public int? ImageType { get; set; }

    [JsonProperty(propertyName: "isMovie")]
    public int? IsMovie { get; set; }

    [JsonProperty(propertyName: "seasons")]
    public Seasons.TvdbSeason[]? Seasons { get; set; }

    [JsonProperty(propertyName: "number")]
    public int Number { get; set; }

    [JsonProperty(propertyName: "seasonNumber")]
    public int SeasonNumber { get; set; }

    [JsonProperty(propertyName: "lastUpdated")]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonProperty(propertyName: "finaleType")]
    public string? FinaleType { get; set; }

    [JsonProperty(propertyName: "year")]
    public string? Year { get; set; }
}

public class TvdbEpisodeExtended : TvdbEpisode
{
    [JsonProperty(propertyName: "airsAfterSeason")]
    public int? AirsAfterSeason { get; set; }

    [JsonProperty(propertyName: "airsBeforeEpisode")]
    public int? AirsBeforeEpisode { get; set; }

    [JsonProperty(propertyName: "airsBeforeSeason")]
    public int? AirsBeforeSeason { get; set; }

    [JsonProperty(propertyName: "awards")]
    public TvdbAward[]? Awards { get; set; }

    [JsonProperty(propertyName: "characters")]
    public TvdbCharacter[]? Characters { get; set; }

    [JsonProperty(propertyName: "companies")]
    public TvdbCompany[]? Companies { get; set; }

    [JsonProperty(propertyName: "contentRatings")]
    public TvdbContentRating[]? ContentRatings { get; set; }

    [JsonProperty(propertyName: "networks")]
    public TvdbCompany[]? Networks { get; set; }

    [JsonProperty(propertyName: "nominations")]
    public string[]? Nominations { get; set; }

    [JsonProperty(propertyName: "productionCode")]
    public string? ProductionCode { get; set; }

    [JsonProperty(propertyName: "remoteIds")]
    public TvdbRemoteId[]? RemoteIds { get; set; }

    [JsonProperty(propertyName: "studios")]
    public TvdbCompany[]? Studios { get; set; }

    [JsonProperty(propertyName: "tagOptions")]
    public TvdbTagOption[]? TagOptions { get; set; }

    [JsonProperty(propertyName: "trailers")]
    public TvdbTrailer[]? Trailers { get; set; }

    [JsonProperty(propertyName: "translations")]
    public TvdbTranslations? Translations { get; set; }

    [JsonProperty(propertyName: "artworks")]
    public TvdbArtwork[]? Artworks { get; set; }
}
