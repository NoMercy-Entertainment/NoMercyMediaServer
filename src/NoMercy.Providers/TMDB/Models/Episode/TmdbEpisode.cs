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

namespace NoMercy.Providers.TMDB.Models.Episode;

public class TmdbEpisode
{
    [JsonProperty(propertyName: "air_date")]
    public DateTime? AirDate { get; set; }

    [JsonProperty(propertyName: "episode_number")]
    public int EpisodeNumber { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "production_code")]
    public string? ProductionCode { get; set; }

    [JsonProperty(propertyName: "season_number")]
    public int SeasonNumber { get; set; }

    [JsonProperty(propertyName: "still_path")]
    public string? StillPath { get; set; }

    [JsonProperty(propertyName: "vote_average")]
    public float? VoteAverage { get; set; }

    [JsonProperty(propertyName: "vote_count")]
    public int VoteCount { get; set; }
}
