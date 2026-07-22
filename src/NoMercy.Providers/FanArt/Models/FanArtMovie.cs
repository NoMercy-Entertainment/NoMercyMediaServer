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

namespace NoMercy.Providers.FanArt.Models;

public class FanArtMovie
{
    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "tmdb_id")]
    public int TmdbId { get; set; }

    [JsonProperty(propertyName: "imdb_id")]
    public string ImdbId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "hdmovielogo")]
    public VideoImage? HdLogo { get; set; }

    [JsonProperty(propertyName: "movieposter")]
    public VideoImage? Poster { get; set; }

    [JsonProperty(propertyName: "moviedisc")]
    public VideoImage? Disc { get; set; }

    [JsonProperty(propertyName: "movielogo")]
    public VideoImage? Logo { get; set; }

    [JsonProperty(propertyName: "moviethumb")]
    public VideoImage? Thumb { get; set; }

    [JsonProperty(propertyName: "moviebanner")]
    public VideoImage? Banner { get; set; }
}
