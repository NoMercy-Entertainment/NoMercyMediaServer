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
using NoMercy.Providers.TVDB.Models.Episodes;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Seasons;

public class TvdbSeasonResponse : TvdbResponse<TvdbSeason> { }

public class TvdbSeasonExtendedResponse : TvdbResponse<TvdbSeasonExtended> { }

public class TvdbSeasonTypesResponse : TvdbResponse<TvdbSeasonType[]> { }

public class TvdbSeasonTranslationResponse : TvdbResponse<TvdbTranslationData> { }

public class TvdbSeason
{
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "seriesId")]
    public long SeriesId { get; set; }

    [JsonProperty(propertyName: "type")]
    public TvdbSeasonType? Type { get; set; }

    [JsonProperty(propertyName: "number")]
    public int Number { get; set; }

    [JsonProperty(propertyName: "nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty(propertyName: "overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty(propertyName: "image")]
    public Uri? Image { get; set; }

    [JsonProperty(propertyName: "imageType")]
    public int? ImageType { get; set; }

    [JsonProperty(propertyName: "companies")]
    public TvdbCompany[]? Companies { get; set; }

    [JsonProperty(propertyName: "lastUpdated")]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonProperty(propertyName: "year")]
    public string? Year { get; set; }
}

public class TvdbSeasonExtended : TvdbSeason
{
    [JsonProperty(propertyName: "artwork")]
    public TvdbArtwork[]? Artwork { get; set; }

    [JsonProperty(propertyName: "awards")]
    public TvdbAward[]? Awards { get; set; }

    [JsonProperty(propertyName: "characters")]
    public TvdbCharacter[]? Characters { get; set; }

    [JsonProperty(propertyName: "episodes")]
    public TvdbEpisode[]? Episodes { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "tagOptions")]
    public TvdbTagOption[]? TagOptions { get; set; }

    [JsonProperty(propertyName: "trailers")]
    public TvdbTrailer[]? Trailers { get; set; }

    [JsonProperty(propertyName: "translations")]
    public TvdbTranslations? Translations { get; set; }
}

public class TvdbSeasonType
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "alternateName")]
    public string? AlternateName { get; set; }
}
