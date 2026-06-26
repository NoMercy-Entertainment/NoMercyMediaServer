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
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Providers.TMDB.Models.Find;

public class TmdbFindById
{
    [JsonProperty("movie_results")]
    public TmdbMovie[] MovieResults { get; set; } = [];

    [JsonProperty("person_results")]
    public TmdbPerson[] PersonResults { get; set; } = [];

    [JsonProperty("tv_results")]
    public TmdbTvShow[] TvResults { get; set; } = [];

    [JsonProperty("tv_episode_results")]
    public TmdbEpisode[] TvEpisodeResults { get; set; } = [];

    [JsonProperty("tv_season_results")]
    public TmdbSeason[] TvSeasonResults { get; set; } = [];
}
