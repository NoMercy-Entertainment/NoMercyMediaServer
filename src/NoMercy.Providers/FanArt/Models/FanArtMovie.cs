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
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("tmdb_id")]
    public int TmdbId { get; set; }

    [JsonProperty("imdb_id")]
    public string ImdbId { get; set; } = string.Empty;

    [JsonProperty("hdmovielogo")]
    public VideoImage? HdLogo { get; set; }

    [JsonProperty("movieposter")]
    public VideoImage? Poster { get; set; }

    [JsonProperty("moviedisc")]
    public VideoImage? Disc { get; set; }

    [JsonProperty("movielogo")]
    public VideoImage? Logo { get; set; }

    [JsonProperty("moviethumb")]
    public VideoImage? Thumb { get; set; }

    [JsonProperty("moviebanner")]
    public VideoImage? Banner { get; set; }
}
