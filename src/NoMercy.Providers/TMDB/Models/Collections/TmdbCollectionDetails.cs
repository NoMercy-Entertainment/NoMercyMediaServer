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
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.Providers.TMDB.Models.Collections;

public class TmdbCollectionDetails : TmdbCollection
{
    [JsonProperty("overview")]
    public string Overview { get; set; } = string.Empty;

    [JsonProperty("parts")]
    public TmdbMovie[] Parts { get; set; } = [];
}
