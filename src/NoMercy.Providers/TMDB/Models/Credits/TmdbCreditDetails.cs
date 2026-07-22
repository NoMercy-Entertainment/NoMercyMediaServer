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
    [JsonProperty(propertyName: "credit_type")]
    public string CreditType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "department")]
    public string Department { get; set; } = string.Empty;

    [JsonProperty(propertyName: "job")]
    public string Job { get; set; } = string.Empty;

    [JsonProperty(propertyName: "media")]
    public TmdbMedia? Media { get; set; }

    [JsonProperty(propertyName: "media_type")]
    public string? MediaType { get; set; }

    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "person")]
    public TmdbPerson TmdbPerson { get; set; } = new();
}
