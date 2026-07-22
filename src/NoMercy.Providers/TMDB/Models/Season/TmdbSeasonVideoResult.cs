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

public class TmdbSeasonVideoResult
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "iso_639_1")]
    public string Iso6391 { get; set; } = string.Empty;

    [JsonProperty(propertyName: "iso_3166_1")]
    public string Iso31661 { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "key")]
    public string Key { get; set; } = string.Empty;

    [JsonProperty(propertyName: "site")]
    public Uri? Site { get; set; }

    [JsonProperty(propertyName: "size")]
    public int Size { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "official")]
    public bool Official { get; set; }

    [JsonProperty(propertyName: "published_at")]
    public DateTime? PublishedAt { get; set; }
}
