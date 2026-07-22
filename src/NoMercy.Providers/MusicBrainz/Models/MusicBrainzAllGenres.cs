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

namespace NoMercy.Providers.MusicBrainz.Models;

public class MusicBrainzAllGenres
{
    [JsonProperty(propertyName: "genres")]
    public MusicBrainzGenre[] Genres { get; set; } = [];

    [JsonProperty(propertyName: "genre-offset")]
    public long GenreOffset { get; set; }

    [JsonProperty(propertyName: "genre-count")]
    public long GenreCount { get; set; }
}
