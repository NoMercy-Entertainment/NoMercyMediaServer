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
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Networks;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvShowDetails : TmdbTvShow
{
    [JsonProperty(propertyName: "adult")]
    public bool Adult { get; set; }

    [JsonProperty(propertyName: "created_by")]
    public TmdbCreatedBy[] CreatedBy { get; set; } = [];

    [JsonProperty(propertyName: "episode_run_time")]
    public int[]? EpisodeRunTime { get; set; } = [];

    [JsonProperty(propertyName: "genres")]
    public TmdbGenre[] Genres { get; set; } = [];

    [JsonProperty(propertyName: "homepage")]
    public Uri? Homepage { get; set; }

    [JsonProperty(propertyName: "in_production")]
    public bool InProduction { get; set; }

    [JsonProperty(propertyName: "languages")]
    public string[] Languages { get; set; } = [];

    [JsonProperty(propertyName: "last_episode_to_air")]
    public TmdbEpisode? LastEpisodeToAir { get; set; }

    [JsonProperty(propertyName: "next_episode_to_air")]
    public TmdbEpisode? NextEpisodeToAir { get; set; }

    [JsonProperty(propertyName: "networks")]
    public TmdbNetwork[] Networks { get; set; } = [];

    [JsonProperty(propertyName: "number_of_episodes")]
    public int NumberOfEpisodes { get; set; }

    [JsonProperty(propertyName: "number_of_seasons")]
    public int NumberOfSeasons { get; set; }

    [JsonProperty(propertyName: "production_companies")]
    public TmdbProductionCompany[] ProductionCompanies { get; set; } = [];

    [JsonProperty(propertyName: "production_countries")]
    public TmdbProductionCountry[] ProductionCountries { get; set; } = [];

    [JsonProperty(propertyName: "seasons")]
    public List<TmdbSeason> Seasons { get; set; } = [];

    [JsonProperty(propertyName: "spoken_languages")]
    public TmdbSpokenLanguage[] SpokenLanguages { get; set; } = [];

    [JsonProperty(propertyName: "status")]
    public string? Status { get; set; }

    [JsonProperty(propertyName: "tagline")]
    public string? Tagline { get; set; }

    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }
}
