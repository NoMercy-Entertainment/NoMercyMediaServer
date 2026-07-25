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
    [JsonProperty("rating")]
    public string? Rating { get; set; } = string.Empty;

    [JsonProperty("meaning")]
    public string Meaning { get; set; } = string.Empty;

    [JsonProperty("order")]
    public long Order { get; set; }

    [JsonProperty("iso_3166_1")]
    public string? Iso31661 { get; set; } = string.Empty;

    [JsonProperty("image")]
    public string Image { get; set; } = string.Empty;
}
