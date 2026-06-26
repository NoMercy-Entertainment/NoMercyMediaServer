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
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty("image")]
    public Uri? Image { get; set; }

    [JsonProperty("nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty("overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty("aliases")]
    public TvdbAlias[] Aliases { get; set; } = [];

    [JsonProperty("score")]
    public double Score { get; set; }

    [JsonProperty("runtime")]
    public int? Runtime { get; set; }

    [JsonProperty("status")]
    public TvdbStatus? Status { get; set; }

    [JsonProperty("lastUpdated")]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonProperty("year")]
    public string? Year { get; set; }
}

public class TvdbMovieExtended : TvdbMovie
{
    [JsonProperty("artworks")]
    public TvdbArtwork[] Artworks { get; set; } = [];

    [JsonProperty("audioLanguages")]
    public string[]? AudioLanguages { get; set; }

    [JsonProperty("awards")]
    public TvdbAward[]? Awards { get; set; }

    [JsonProperty("boxOffice")]
    public string? BoxOffice { get; set; }

    [JsonProperty("boxOfficeUS")]
    public string? BoxOfficeUS { get; set; }

    [JsonProperty("budget")]
    public string? Budget { get; set; }

    [JsonProperty("characters")]
    public TvdbCharacter[]? Characters { get; set; }

    [JsonProperty("companies")]
    public TvdbMovieCompanies? Companies { get; set; }

    [JsonProperty("contentRatings")]
    public TvdbContentRating[]? ContentRatings { get; set; }

    [JsonProperty("first_release")]
    public TvdbRelease? FirstRelease { get; set; }

    [JsonProperty("genres")]
    public TvdbGenre[]? Genres { get; set; }

    [JsonProperty("inspirations")]
    public TvdbInspiration[]? Inspirations { get; set; }

    [JsonProperty("lists")]
    public TvdbList[]? Lists { get; set; }

    [JsonProperty("originalCountry")]
    public string? OriginalCountry { get; set; }

    [JsonProperty("originalLanguage")]
    public string? OriginalLanguage { get; set; }

    [JsonProperty("releases")]
    public TvdbRelease[]? Releases { get; set; }

    [JsonProperty("remoteIds")]
    public TvdbRemoteId[]? RemoteIds { get; set; }

    [JsonProperty("spoken_languages")]
    public string[]? SpokenLanguages { get; set; }

    [JsonProperty("studios")]
    public TvdbCompany[]? Studios { get; set; }

    [JsonProperty("subtitleLanguages")]
    public string[]? SubtitleLanguages { get; set; }

    [JsonProperty("tagOptions")]
    public TvdbTagOption[]? TagOptions { get; set; }

    [JsonProperty("trailers")]
    public TvdbTrailer[]? Trailers { get; set; }

    [JsonProperty("translations")]
    public TvdbTranslations? Translations { get; set; }
}

public class TvdbMovieCompanies
{
    [JsonProperty("studio")]
    public TvdbCompany[]? Studio { get; set; }

    [JsonProperty("network")]
    public TvdbCompany[]? Network { get; set; }

    [JsonProperty("production")]
    public TvdbCompany[]? Production { get; set; }

    [JsonProperty("distributor")]
    public TvdbCompany[]? Distributor { get; set; }

    [JsonProperty("special_effects")]
    public TvdbCompany[]? SpecialEffects { get; set; }
}

public class TvdbRelease
{
    [JsonProperty("country")]
    public string Country { get; set; } = string.Empty;

    [JsonProperty("date")]
    public string Date { get; set; } = string.Empty;

    [JsonProperty("detail")]
    public string? Detail { get; set; }
}
