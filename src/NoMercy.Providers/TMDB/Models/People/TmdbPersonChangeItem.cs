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

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonChangeItem
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "action")]
    public string Action { get; set; } = string.Empty;

    [JsonProperty(propertyName: "time")]
    public string Time { get; set; } = string.Empty;

    [JsonProperty(propertyName: "original_value")]
    public TmdbPersonChangeOriginalValue TmdbPersonChangeOriginalValue { get; set; } = new();
}
