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

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbEpisodeGroupEpisode
{
    [JsonProperty(propertyName: "air_date")]
    public string? AirDate { get; set; }

    [JsonProperty(propertyName: "episode_number")]
    public int EpisodeNumber { get; set; }

    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "overview")]
    public string Overview { get; set; } = string.Empty;

    [JsonProperty(propertyName: "production_code")]
    public string? ProductionCode { get; set; }

    [JsonProperty(propertyName: "runtime")]
    public int? Runtime { get; set; }

    [JsonProperty(propertyName: "season_number")]
    public int SeasonNumber { get; set; }

    [JsonProperty(propertyName: "show_id")]
    public int ShowId { get; set; }

    [JsonProperty(propertyName: "still_path")]
    public string? StillPath { get; set; }

    [JsonProperty(propertyName: "vote_average")]
    public double VoteAverage { get; set; }

    [JsonProperty(propertyName: "vote_count")]
    public int VoteCount { get; set; }

    [JsonProperty(propertyName: "order")]
    public int Order { get; set; }
}
