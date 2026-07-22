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

public class TmdbLogo
{
    [JsonProperty(propertyName: "aspect_ratio")]
    public double AspectRatio { get; set; }

    [JsonProperty(propertyName: "file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonProperty(propertyName: "height")]
    public int Height { get; set; }

    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "file_type")]
    public string FileType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "vote_average")]
    public int VoteAverage { get; set; }

    [JsonProperty(propertyName: "vote_count")]
    public int VoteCount { get; set; }

    [JsonProperty(propertyName: "width")]
    public int Width { get; set; }
}
