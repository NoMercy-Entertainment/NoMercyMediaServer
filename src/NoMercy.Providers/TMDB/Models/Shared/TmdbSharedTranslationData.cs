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

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbSharedTranslationData
{
    [JsonProperty("overview")]
    public string Overview { get; set; } = string.Empty;

    [JsonProperty("homepage")]
    public Uri? Homepage { get; set; }

    [JsonProperty("tagline")]
    public string Tagline { get; set; } = string.Empty;
}
