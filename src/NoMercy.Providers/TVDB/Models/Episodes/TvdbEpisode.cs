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
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("seriesId")]
    public long SeriesId { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("aired")]
    public string? Aired { get; set; }

    [JsonProperty("runtime")]
    public int? Runtime { get; set; }

    [JsonProperty("nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty("overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty("overview")]
    public string? Overview { get; set; }

    [JsonProperty("image")]
    public Uri? Image { get; set; }

    [JsonProperty("imageType")]
    public int? ImageType { get; set; }

    [JsonProperty("isMovie")]
    public int? IsMovie { get; set; }

    [JsonProperty("seasons")]
    public Seasons.TvdbSeason[]? Seasons { get; set; }

    [JsonProperty("number")]
    public int Number { get; set; }

    [JsonProperty("seasonNumber")]
    public int SeasonNumber { get; set; }

    [JsonProperty("lastUpdated")]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonProperty("finaleType")]
    public string? FinaleType { get; set; }

    [JsonProperty("year")]
    public string? Year { get; set; }
}

public class TvdbEpisodeExtended : TvdbEpisode
{
    [JsonProperty("airsAfterSeason")]
    public int? AirsAfterSeason { get; set; }

    [JsonProperty("airsBeforeEpisode")]
    public int? AirsBeforeEpisode { get; set; }

    [JsonProperty("airsBeforeSeason")]
    public int? AirsBeforeSeason { get; set; }

    [JsonProperty("awards")]
    public TvdbAward[]? Awards { get; set; }

    [JsonProperty("characters")]
    public TvdbCharacter[]? Characters { get; set; }

    [JsonProperty("companies")]
    public TvdbCompany[]? Companies { get; set; }

    [JsonProperty("contentRatings")]
    public TvdbContentRating[]? ContentRatings { get; set; }

    [JsonProperty("networks")]
    public TvdbCompany[]? Networks { get; set; }

    [JsonProperty("nominations")]
    public string[]? Nominations { get; set; }

    [JsonProperty("productionCode")]
    public string? ProductionCode { get; set; }

    [JsonProperty("remoteIds")]
    public TvdbRemoteId[]? RemoteIds { get; set; }

    [JsonProperty("studios")]
    public TvdbCompany[]? Studios { get; set; }

    [JsonProperty("tagOptions")]
    public TvdbTagOption[]? TagOptions { get; set; }

    [JsonProperty("trailers")]
    public TvdbTrailer[]? Trailers { get; set; }

    [JsonProperty("translations")]
    public TvdbTranslations? Translations { get; set; }

    [JsonProperty("artworks")]
    public TvdbArtwork[]? Artworks { get; set; }
}
