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

namespace NoMercy.Providers.TMDB.Models.WatchProviders;

public class TmdbTvProviderResult
{
    [JsonProperty("display_priority")]
    public int DisplayPriority { get; set; }

    [JsonProperty("logo_path")]
    public string? LogoPath { get; set; }

    [JsonProperty("provider_name")]
    public string ProviderName { get; set; } = string.Empty;

    [JsonProperty("provider_id")]
    public int ProviderId { get; set; }
}
