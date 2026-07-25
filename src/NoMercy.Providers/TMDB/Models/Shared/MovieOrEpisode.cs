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

namespace NoMercy.Providers.TMDB.Models.Shared;

public record MovieOrEpisode
{
    [JsonProperty("id")]
    public dynamic Id { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// English show name for episodes — server-side only, not emitted in the
    /// API response (the API surfaces this baked into MovieFile.Title via the
    /// filelist's '<show> SxxExx <episode title>' label).
    /// </summary>
    [JsonIgnore]
    public string? ShowName { get; set; }

    [JsonProperty("duration")]
    public TimeSpan? Duration { get; set; }

    [JsonProperty("adult")]
    public bool Adult { get; set; }

    [JsonProperty("overview")]
    public string? Overview { get; set; }

    [JsonProperty("episode_number")]
    public int EpisodeNumber { get; set; }

    [JsonProperty("season_number")]
    public int SeasonNumber { get; set; }

    [JsonProperty("still")]
    public string? Still { get; set; }
}
