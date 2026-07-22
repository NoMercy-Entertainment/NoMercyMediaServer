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
using NoMercy.Providers.TVDB.Models.Genres;
using NoMercy.Providers.TVDB.Models.Inspirations;
using NoMercy.Providers.TVDB.Models.Lists;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Movies;

public class TvdbMovieResponse : TvdbResponse<TvdbMovie> { }

public class TvdbMovieExtendedResponse : TvdbResponse<TvdbMovieExtended> { }

public class TvdbMoviesResponse : TvdbResponse<TvdbMovie[]> { }

public class TvdbMovieStatusesResponse : TvdbResponse<TvdbStatus[]> { }

public class TvdbMovieTranslationResponse : TvdbResponse<TvdbTranslationData> { }

public class TvdbMovie
{
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty(propertyName: "image")]
    public Uri? Image { get; set; }

    [JsonProperty(propertyName: "nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty(propertyName: "overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty(propertyName: "aliases")]
    public TvdbAlias[] Aliases { get; set; } = [];

    [JsonProperty(propertyName: "score")]
    public double Score { get; set; }

    [JsonProperty(propertyName: "runtime")]
    public int? Runtime { get; set; }

    [JsonProperty(propertyName: "status")]
    public TvdbStatus? Status { get; set; }

    [JsonProperty(propertyName: "lastUpdated")]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonProperty(propertyName: "year")]
    public string? Year { get; set; }
}

public class TvdbMovieExtended : TvdbMovie
{
    [JsonProperty(propertyName: "artworks")]
    public TvdbArtwork[] Artworks { get; set; } = [];

    [JsonProperty(propertyName: "audioLanguages")]
    public string[]? AudioLanguages { get; set; }

    [JsonProperty(propertyName: "awards")]
    public TvdbAward[]? Awards { get; set; }

    [JsonProperty(propertyName: "boxOffice")]
    public string? BoxOffice { get; set; }

    [JsonProperty(propertyName: "boxOfficeUS")]
    public string? BoxOfficeUS { get; set; }

    [JsonProperty(propertyName: "budget")]
    public string? Budget { get; set; }

    [JsonProperty(propertyName: "characters")]
    public TvdbCharacter[]? Characters { get; set; }

    [JsonProperty(propertyName: "companies")]
    public TvdbMovieCompanies? Companies { get; set; }

    [JsonProperty(propertyName: "contentRatings")]
    public TvdbContentRating[]? ContentRatings { get; set; }

    [JsonProperty(propertyName: "first_release")]
    public TvdbRelease? FirstRelease { get; set; }

    [JsonProperty(propertyName: "genres")]
    public TvdbGenre[]? Genres { get; set; }

    [JsonProperty(propertyName: "inspirations")]
    public TvdbInspiration[]? Inspirations { get; set; }

    [JsonProperty(propertyName: "lists")]
    public TvdbList[]? Lists { get; set; }

    [JsonProperty(propertyName: "originalCountry")]
    public string? OriginalCountry { get; set; }

    [JsonProperty(propertyName: "originalLanguage")]
    public string? OriginalLanguage { get; set; }

    [JsonProperty(propertyName: "releases")]
    public TvdbRelease[]? Releases { get; set; }

    [JsonProperty(propertyName: "remoteIds")]
    public TvdbRemoteId[]? RemoteIds { get; set; }

    [JsonProperty(propertyName: "spoken_languages")]
    public string[]? SpokenLanguages { get; set; }

    [JsonProperty(propertyName: "studios")]
    public TvdbCompany[]? Studios { get; set; }

    [JsonProperty(propertyName: "subtitleLanguages")]
    public string[]? SubtitleLanguages { get; set; }

    [JsonProperty(propertyName: "tagOptions")]
    public TvdbTagOption[]? TagOptions { get; set; }

    [JsonProperty(propertyName: "trailers")]
    public TvdbTrailer[]? Trailers { get; set; }

    [JsonProperty(propertyName: "translations")]
    public TvdbTranslations? Translations { get; set; }
}

public class TvdbMovieCompanies
{
    [JsonProperty(propertyName: "studio")]
    public TvdbCompany[]? Studio { get; set; }

    [JsonProperty(propertyName: "network")]
    public TvdbCompany[]? Network { get; set; }

    [JsonProperty(propertyName: "production")]
    public TvdbCompany[]? Production { get; set; }

    [JsonProperty(propertyName: "distributor")]
    public TvdbCompany[]? Distributor { get; set; }

    [JsonProperty(propertyName: "special_effects")]
    public TvdbCompany[]? SpecialEffects { get; set; }
}

public class TvdbRelease
{
    [JsonProperty(propertyName: "country")]
    public string Country { get; set; } = string.Empty;

    [JsonProperty(propertyName: "date")]
    public string Date { get; set; } = string.Empty;

    [JsonProperty(propertyName: "detail")]
    public string? Detail { get; set; }
}
