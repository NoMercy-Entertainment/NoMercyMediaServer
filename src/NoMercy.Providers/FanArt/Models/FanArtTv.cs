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
    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = "";

    [JsonProperty(propertyName: "thetvdb_id")]
    public string TvdbId { get; set; } = "";

    [JsonProperty(propertyName: "tvposter")]
    public VideoImage? Poster { get; set; }

    [JsonProperty(propertyName: "clearlogo")]
    public VideoImage? ClearLogo { get; set; }

    [JsonProperty(propertyName: "seasonposter")]
    public VideoImage? SeasonPoster { get; set; }

    [JsonProperty(propertyName: "hdtvlogo")]
    public VideoImage? HdLogo { get; set; }

    [JsonProperty(propertyName: "tvthumb")]
    public VideoImage? Thumb { get; set; }

    [JsonProperty(propertyName: "tvbanner")]
    public VideoImage? Banner { get; set; }

    [JsonProperty(propertyName: "clearart")]
    public VideoImage? ClearArt { get; set; }

    [JsonProperty(propertyName: "hdclearart")]
    public VideoImage? HdClearArt { get; set; }

    [JsonProperty(propertyName: "seasonthumb")]
    public VideoImage? SeasonThumb { get; set; }

    [JsonProperty(propertyName: "characterart")]
    public VideoImage? CharacterArt { get; set; }

    [JsonProperty(propertyName: "seasonbanner")]
    public VideoImage? SeasonBanner { get; set; }
}
