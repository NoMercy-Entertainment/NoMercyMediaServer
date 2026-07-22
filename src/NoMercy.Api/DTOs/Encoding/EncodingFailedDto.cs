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

public record EncodingFailedDto
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "input_path")]
    public string InputPath { get; set; } = string.Empty;

    [JsonProperty(propertyName: "error_message")]
    public string ErrorMessage { get; set; } = string.Empty;

    [JsonProperty(propertyName: "exception_type")]
    public string? ExceptionType { get; set; }

    [JsonProperty(propertyName: "timestamp")]
    public DateTime Timestamp { get; set; }
}
