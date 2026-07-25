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

public class TmdbImage
{
    [JsonProperty("aspect_ratio")]
    public double AspectRatio { get; set; }

    [JsonProperty("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("iso_639_1")]
    public string? Iso6391 { get; set; }

    [JsonProperty("vote_average")]
    public float VoteAverage { get; set; }

    [JsonProperty("vote_count")]
    public int VoteCount { get; set; }

    [JsonProperty("width")]
    public int Width { get; set; }
}
