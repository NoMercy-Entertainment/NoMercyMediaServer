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

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvVideo
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("iso_639_1")]
    public string Iso6391 { get; set; } = string.Empty;

    [JsonProperty("iso_3166_1")]
    public string Iso31661 { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;

    [JsonProperty("site")]
    public string Site { get; set; } = string.Empty;

    [JsonProperty("size")]
    public int Size { get; set; }

    [JsonProperty("official")]
    public bool Official { get; set; }

    [JsonProperty("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;
}
