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

namespace NoMercy.Providers.TMDB.Models.Credits;

public class TmdbCreditDetails
{
    [JsonProperty("credit_type")]
    public string CreditType { get; set; } = string.Empty;

    [JsonProperty("department")]
    public string Department { get; set; } = string.Empty;

    [JsonProperty("job")]
    public string Job { get; set; } = string.Empty;

    [JsonProperty("media")]
    public TmdbMedia? Media { get; set; }

    [JsonProperty("media_type")]
    public string? MediaType { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("person")]
    public TmdbPerson TmdbPerson { get; set; } = new();
}
