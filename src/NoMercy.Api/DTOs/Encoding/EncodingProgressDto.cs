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

namespace NoMercy.Api.DTOs.Encoding;

public record EncodingProgressDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("percentage")]
    public double Percentage { get; set; }

    [JsonProperty("elapsed")]
    public double Elapsed { get; set; }

    [JsonProperty("estimated")]
    public double? Estimated { get; set; }

    [JsonProperty("fps")]
    public double? Fps { get; set; }

    [JsonProperty("speed")]
    public double? Speed { get; set; }

    [JsonProperty("bitrate_kbps")]
    public int? BitrateKbps { get; set; }
}
