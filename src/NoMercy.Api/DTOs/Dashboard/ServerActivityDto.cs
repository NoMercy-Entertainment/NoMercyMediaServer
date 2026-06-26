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
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("category")]
    public ActivityCategory Category { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("time")]
    public DateTime Time { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonProperty("user_id")]
    public Guid UserId { get; set; }

    [JsonProperty("device_id")]
    public Ulid DeviceId { get; set; }

    [JsonProperty("media_id")]
    public Ulid? MediaId { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("error_code")]
    public string? ErrorCode { get; set; }

    [JsonProperty("metadata")]
    public string? Metadata { get; set; }

    [JsonProperty("device")]
    public string Device { get; set; } = string.Empty;

    [JsonProperty("user")]
    public string User { get; set; } = string.Empty;
}
