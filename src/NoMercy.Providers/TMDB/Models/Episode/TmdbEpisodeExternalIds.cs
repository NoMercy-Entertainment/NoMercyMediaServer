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

namespace NoMercy.Providers.TMDB.Models.Episode;

public class TmdbEpisodeExternalIds
{
    [JsonProperty("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonProperty("freebase_mid")]
    public string? FreebaseMid { get; set; }

    [JsonProperty("freebase_id")]
    public string? FreebaseId { get; set; }

    [JsonProperty("tvrage_id")]
    public int? TvRageId { get; set; }

    [JsonProperty("tvdb_id")]
    public int? TvdbId { get; set; }

    [JsonProperty("id")]
    public int Id { get; set; }
}
