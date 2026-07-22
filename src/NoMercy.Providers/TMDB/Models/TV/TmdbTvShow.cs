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

public class TmdbTvShow : TmdbBase
{
    [JsonProperty(propertyName: "first_air_date")]
    public DateTime? FirstAirDate { get; set; }

    [JsonProperty(propertyName: "genre_ids")]
    public int?[] GenreIds { get; set; } = [];

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "origin_country")]
    public string[] OriginCountry { get; set; } = [];

    [JsonProperty(propertyName: "original_name")]
    public string OriginalName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string MediaType { get; set; } = string.Empty;
}
