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

public record EncodingStartedDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("input_path")]
    public string InputPath { get; set; } = string.Empty;

    [JsonProperty("output_path")]
    public string OutputPath { get; set; } = string.Empty;

    [JsonProperty("profile_name")]
    public string ProfileName { get; set; } = string.Empty;

    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }
}
