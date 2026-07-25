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

namespace NoMercy.Providers.TMDB.Models.Networks;

public class TmdbNetwork
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("logo_path")]
    public string LogoPath { get; set; } = string.Empty;

    [JsonProperty("origin_country")]
    public string OriginCountry { get; set; } = string.Empty;
}
