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
    [JsonProperty(propertyName: "id")]
    public int? Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "credit_id")]
    public string CreditId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "person_id")]
    public int? PersonId { get; set; }

    [JsonProperty(propertyName: "season_id")]
    public int? SeasonId { get; set; }

    [JsonProperty(propertyName: "poster")]
    public TmdbPoster TmdbPoster { get; set; } = new();

    [JsonProperty(propertyName: "department")]
    public string Department { get; set; } = string.Empty;

    [JsonProperty(propertyName: "job")]
    public string Job { get; set; } = string.Empty;
}
