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
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbOriginalValueClass
{
    [JsonProperty("id")]
    public int? Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("credit_id")]
    public string CreditId { get; set; } = string.Empty;

    [JsonProperty("person_id")]
    public int? PersonId { get; set; }

    [JsonProperty("season_id")]
    public int? SeasonId { get; set; }

    [JsonProperty("poster")]
    public TmdbPoster TmdbPoster { get; set; } = new();

    [JsonProperty("department")]
    public string Department { get; set; } = string.Empty;

    [JsonProperty("job")]
    public string Job { get; set; } = string.Empty;
}
