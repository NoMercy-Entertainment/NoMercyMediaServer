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

public record DevicesDto
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "browser")]
    public string Browser { get; set; } = string.Empty;

    [JsonProperty(propertyName: "os")]
    public string Os { get; set; } = string.Empty;

    [JsonProperty(propertyName: "device")]
    public string? Device { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "online")]
    public bool Online { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "custom_name")]
    public object? CustomName { get; set; }

    [JsonProperty(propertyName: "version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty(propertyName: "ip")]
    public string Ip { get; set; } = string.Empty;

    [JsonProperty(propertyName: "created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty(propertyName: "updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonProperty(propertyName: "activity_logs")]
    public IEnumerable<ActivityLogDto> ActivityLogs { get; set; } = [];
}
