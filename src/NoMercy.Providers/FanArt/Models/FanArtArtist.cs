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

public class FanArtArtist
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("mbid_id")]
    public string MbId { get; set; } = string.Empty;

    [JsonProperty("albums")]
    public Dictionary<Guid, FanArtArtists> Artists { get; set; } = [];
}
