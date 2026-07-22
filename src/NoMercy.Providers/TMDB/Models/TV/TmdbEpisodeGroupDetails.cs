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
using NoMercy.Providers.TMDB.Models.Networks;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbEpisodeGroupDetails
{
    [JsonProperty(propertyName: "description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty(propertyName: "episode_count")]
    public int EpisodeCount { get; set; }

    [JsonProperty(propertyName: "group_count")]
    public int GroupCount { get; set; }

    [JsonProperty(propertyName: "groups")]
    public TmdbEpisodeGroup[] Groups { get; set; } = [];

    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "network")]
    public TmdbNetwork? Network { get; set; }

    [JsonProperty(propertyName: "type")]
    public int Type { get; set; }
}
