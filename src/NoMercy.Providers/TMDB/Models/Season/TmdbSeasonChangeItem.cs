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

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonChangeItem
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "action")]
    public Action? Action { get; set; }

    [JsonProperty(propertyName: "time")]
    public string Time { get; set; } = string.Empty;

    [JsonProperty(propertyName: "value")]
    public string? Value { get; set; }

    [JsonProperty(propertyName: "original_value")]
    public string OriginalValue { get; set; } = string.Empty;

    [JsonProperty(propertyName: "iso_639_1")]
    public string Iso6391 { get; set; } = string.Empty;
}
