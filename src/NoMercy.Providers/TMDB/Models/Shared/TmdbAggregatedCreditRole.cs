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

public class TmdbAggregatedCreditRole
{
    [JsonProperty(propertyName: "credit_id")]
    public string? CreditId { get; set; }

    [JsonProperty(propertyName: "character")]
    public string? Character { get; set; }

    [JsonProperty(propertyName: "episode_count")]
    public int EpisodeCount { get; set; }

    [JsonProperty(propertyName: "order")]
    public int? Order { get; set; }
}
