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

namespace NoMercy.Api.DTOs.Dashboard;

public record TaskDto
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "value")]
    public int Value { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty(propertyName: "updated_at")]
    public DateTime UpdatedAt { get; set; }
}
