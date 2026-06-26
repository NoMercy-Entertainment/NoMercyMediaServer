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

public class TmdbAggregatedCrewJob
{
    [JsonProperty("credit_id")]
    public string? CreditId { get; set; }

    [JsonProperty("job")]
    public string? Job { get; set; }

    [JsonProperty("episode_count")]
    public int EpisodeCount { get; set; }

    [JsonProperty("order")]
    public int? Order { get; set; }
}
