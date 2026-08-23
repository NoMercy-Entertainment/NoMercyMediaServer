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

public record JikanAnime
{
    [JsonProperty("mal_id")]
    public int MalId { get; set; }

    [JsonProperty("titles")]
    public JikanTitle[] Titles { get; set; } = [];

    [JsonProperty("genres")]
    public JikanGenre[] Genres { get; set; } = [];

    [JsonProperty("themes")]
    public JikanGenre[] Themes { get; set; } = [];

    [JsonProperty("demographics")]
    public JikanGenre[] Demographics { get; set; } = [];

    [JsonProperty("year")]
    public int? Year { get; set; }
}
