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
using NoMercy.Providers.TMDB.Models.Combined;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieAppends : TmdbMovieDetails
{
    [JsonProperty(propertyName: "alternative_titles")]
    public TmdbMovieAlternativeTitles AlternativeTitles { get; set; } = new();

    [JsonProperty(propertyName: "credits")]
    public TmdbMovieCredits Credits { get; set; } = new();

    [JsonProperty(propertyName: "external_ids")]
    public TmdbMovieExternalIds ExternalIds { get; set; } = new();

    [JsonProperty(propertyName: "images")]
    public TmdbImages Images { get; set; } = new();

    [JsonProperty(propertyName: "keywords")]
    public TmdbMovieKeywords Keywords { get; set; } = new();

    [JsonProperty(propertyName: "recommendations")]
    public TmdbMovieRecommendations Recommendations { get; set; } = new();

    [JsonProperty(propertyName: "similar")]
    public TmdbMovieSimilar Similar { get; set; } = new();

    [JsonProperty(propertyName: "translations")]
    public TmdbCombinedTranslations Translations { get; set; } = new();

    [JsonProperty(propertyName: "videos")]
    public TmdbMovieVideos Videos { get; set; } = new();

    [JsonProperty(propertyName: "watch/providers")]
    public TmdbWatchProviders WatchProviders { get; set; } = new();

    [JsonProperty(propertyName: "genres")]
    public new TmdbGenre[] Genres { get; set; } = [];

    [JsonProperty(propertyName: "release_dates")]
    public TmdbMovieReleaseDates ReleaseDates { get; set; } = new();
}
