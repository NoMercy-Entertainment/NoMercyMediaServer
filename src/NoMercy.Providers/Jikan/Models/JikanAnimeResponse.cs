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

namespace NoMercy.Providers.Jikan.Models;

// Jikan's /anime/{id} shape: a single object under "data", unlike the search
// endpoint's array - kept as its own type rather than reusing
// JikanSearchResponse so the two response shapes can't be confused.
public record JikanAnimeResponse
{
    [JsonProperty("data")]
    public JikanAnime? Data { get; set; }
}
