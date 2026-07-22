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

public class TmdbProfile
{
    [JsonProperty(propertyName: "aspect_ratio")]
    public double AspectRatio { get; set; }

    [JsonProperty(propertyName: "file_path")]
    public string? FilePath { get; set; }

    [JsonProperty(propertyName: "height")]
    public int Height { get; set; }

    [JsonProperty(propertyName: "iso_639_1")]
    public string? Iso6391 { get; set; }

    [JsonProperty(propertyName: "vote_average")]
    public double VoteAverage { get; set; }

    [JsonProperty(propertyName: "vote_count")]
    public int VoteCount { get; set; }

    [JsonProperty(propertyName: "width")]
    public int Width { get; set; }
}
