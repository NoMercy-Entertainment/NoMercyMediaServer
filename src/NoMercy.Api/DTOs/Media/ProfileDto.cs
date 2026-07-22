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

namespace NoMercy.Api.DTOs.Media;

public record ProfileDto
{
    [JsonProperty(propertyName: "aspect_ratio")]
    public double AspectRatio { get; set; }

    [JsonProperty(propertyName: "height")]
    public long Height { get; set; }

    [JsonProperty(propertyName: "iso_639_1")]
    public object Iso6391 { get; set; } = string.Empty;

    [JsonProperty(propertyName: "file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonProperty(propertyName: "vote_average")]
    public double VoteAverage { get; set; }

    [JsonProperty(propertyName: "vote_count")]
    public long VoteCount { get; set; }

    [JsonProperty(propertyName: "width")]
    public long Width { get; set; }
}
