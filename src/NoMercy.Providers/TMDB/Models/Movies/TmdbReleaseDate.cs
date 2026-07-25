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

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbReleaseDate
{
    [JsonProperty("certification")]
    public string Certification { get; set; } = string.Empty;

    [JsonProperty("iso_639_1")]
    public string Iso6391 { get; set; } = string.Empty;

    [JsonProperty("release_date")]
    public DateTime ReleaseDateReleaseDate { get; set; } = DateTime.MinValue;

    [JsonProperty("type")]
    public int Type { get; set; }

    [JsonProperty("note")]
    public string? Note { get; set; }
}
