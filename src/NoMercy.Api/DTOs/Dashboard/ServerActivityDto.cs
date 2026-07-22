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
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.DTOs.Dashboard;

public record ServerActivityDto
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "category")]
    public ActivityCategory Category { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "time")]
    public DateTime Time { get; set; }

    [JsonProperty(propertyName: "created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty(propertyName: "updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonProperty(propertyName: "user_id")]
    public Guid UserId { get; set; }

    [JsonProperty(propertyName: "device_id")]
    public Ulid DeviceId { get; set; }

    [JsonProperty(propertyName: "media_id")]
    public Ulid? MediaId { get; set; }

    [JsonProperty(propertyName: "success")]
    public bool Success { get; set; }

    [JsonProperty(propertyName: "error_code")]
    public string? ErrorCode { get; set; }

    [JsonProperty(propertyName: "metadata")]
    public string? Metadata { get; set; }

    [JsonProperty(propertyName: "device")]
    public string Device { get; set; } = string.Empty;

    [JsonProperty(propertyName: "user")]
    public string User { get; set; } = string.Empty;
}
