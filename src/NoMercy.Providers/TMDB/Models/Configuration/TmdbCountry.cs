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

namespace NoMercy.Providers.TMDB.Models.Configuration;

public class TmdbCountry
{
    [JsonProperty("iso_3166_1")]
    public string Iso31661 { get; set; } = string.Empty;

    [JsonProperty("native_name")]
    public string NativeName { get; set; } = string.Empty;

    [JsonProperty("english_name")]
    public string EnglishName { get; set; } = string.Empty;
}
