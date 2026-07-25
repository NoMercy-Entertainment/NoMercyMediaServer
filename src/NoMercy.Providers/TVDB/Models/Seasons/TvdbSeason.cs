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
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("seriesId")]
    public long SeriesId { get; set; }

    [JsonProperty("type")]
    public TvdbSeasonType? Type { get; set; }

    [JsonProperty("number")]
    public int Number { get; set; }

    [JsonProperty("nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty("overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty("image")]
    public Uri? Image { get; set; }

    [JsonProperty("imageType")]
    public int? ImageType { get; set; }

    [JsonProperty("companies")]
    public TvdbCompany[]? Companies { get; set; }

    [JsonProperty("lastUpdated")]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonProperty("year")]
    public string? Year { get; set; }
}

public class TvdbSeasonExtended : TvdbSeason
{
    [JsonProperty("artwork")]
    public TvdbArtwork[]? Artwork { get; set; }

    [JsonProperty("awards")]
    public TvdbAward[]? Awards { get; set; }

    [JsonProperty("characters")]
    public TvdbCharacter[]? Characters { get; set; }

    [JsonProperty("episodes")]
    public TvdbEpisode[]? Episodes { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("overview")]
    public string? Overview { get; set; }

    [JsonProperty("tagOptions")]
    public TvdbTagOption[]? TagOptions { get; set; }

    [JsonProperty("trailers")]
    public TvdbTrailer[]? Trailers { get; set; }

    [JsonProperty("translations")]
    public TvdbTranslations? Translations { get; set; }
}

public class TvdbSeasonType
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("alternateName")]
    public string? AlternateName { get; set; }
}
