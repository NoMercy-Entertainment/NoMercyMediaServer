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
using NoMercy.Providers.TMDB.Models.Season;

namespace NoMercy.Providers.TMDB.Models.Credits;

public class TmdbMedia
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("original_name")]
    public string OriginalName { get; set; } = string.Empty;

    [JsonProperty("character")]
    public string Character { get; set; } = string.Empty;

    [JsonProperty("episodes")]
    public TmdbEpisode[] Episodes { get; set; } = [];

    [JsonProperty("seasons")]
    public TmdbSeason[] Seasons { get; set; } = [];
}
