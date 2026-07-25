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
using NoMercy.NmSystem.NewtonSoftConverters;

namespace NoMercy.Providers.FanArt.Models;

public class FanArtArtistDetails : FanArtArtist
{
    [JsonProperty("artistthumb")]
    public Image[] Thumbs { get; set; } = [];

    [JsonProperty("albums")]
    [JsonConverter(typeof(GuidKeyDictionaryConverter<Albums>))]
    public Dictionary<Guid, Albums> ArtistAlbum { get; set; } = [];

    [JsonProperty("artistbackground")]
    public Image[] Backgrounds { get; set; } = [];

    [JsonProperty("hdmusiclogo")]
    public Image[] HdLogos { get; set; } = [];

    [JsonProperty("musiclogo")]
    public Image[] Logos { get; set; } = [];

    [JsonProperty("musicbanner")]
    public Image[] Banners { get; set; } = [];
}
