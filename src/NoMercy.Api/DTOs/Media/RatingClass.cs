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

public record RatingClass
{
    [JsonProperty(propertyName: "rating")]
    public string? Rating { get; set; } = string.Empty;

    [JsonProperty(propertyName: "meaning")]
    public string Meaning { get; set; } = string.Empty;

    [JsonProperty(propertyName: "order")]
    public long Order { get; set; }

    [JsonProperty(propertyName: "iso_3166_1")]
    public string? Iso31661 { get; set; } = string.Empty;

    [JsonProperty(propertyName: "image")]
    public string Image { get; set; } = string.Empty;
}
