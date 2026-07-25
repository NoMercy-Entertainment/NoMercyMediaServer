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

public record EncodingCompletedDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("output_path")]
    public string OutputPath { get; set; } = string.Empty;

    [JsonProperty("duration")]
    public double Duration { get; set; }

    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }
}
