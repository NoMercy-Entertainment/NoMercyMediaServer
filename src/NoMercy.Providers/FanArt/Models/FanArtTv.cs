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

public class FanArtTv
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("thetvdb_id")]
    public string TvdbId { get; set; } = "";

    [JsonProperty("tvposter")]
    public VideoImage? Poster { get; set; }

    [JsonProperty("clearlogo")]
    public VideoImage? ClearLogo { get; set; }

    [JsonProperty("seasonposter")]
    public VideoImage? SeasonPoster { get; set; }

    [JsonProperty("hdtvlogo")]
    public VideoImage? HdLogo { get; set; }

    [JsonProperty("tvthumb")]
    public VideoImage? Thumb { get; set; }

    [JsonProperty("tvbanner")]
    public VideoImage? Banner { get; set; }

    [JsonProperty("clearart")]
    public VideoImage? ClearArt { get; set; }

    [JsonProperty("hdclearart")]
    public VideoImage? HdClearArt { get; set; }

    [JsonProperty("seasonthumb")]
    public VideoImage? SeasonThumb { get; set; }

    [JsonProperty("characterart")]
    public VideoImage? CharacterArt { get; set; }

    [JsonProperty("seasonbanner")]
    public VideoImage? SeasonBanner { get; set; }
}
