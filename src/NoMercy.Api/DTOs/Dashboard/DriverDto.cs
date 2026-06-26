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
using Newtonsoft.Json.Linq;

namespace NoMercy.Api.DTOs.Dashboard;

public record DriverDto
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("config")]
    public JObject? Config { get; set; }

    [JsonProperty("credentials_configured")]
    public bool CredentialsConfigured { get; set; }

    // True for the built-in system local driver — the web client uses this to
    // hide the driver from the manage-drivers UI while still resolving its id
    // for folder picking.
    [JsonProperty("is_system")]
    public bool IsSystem { get; set; }

    [JsonProperty("folder_count")]
    public int FolderCount { get; set; }

    [JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}

public record CreateDriverRequestDto
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("config")]
    public JObject? Config { get; set; }

    [JsonProperty("credentials")]
    public DriverCredentialsDto? Credentials { get; set; }
}

public record UpdateDriverRequestDto
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Optional. Allows switching driver type (e.g. local → nfs) on update.
    /// Validation runs against the new type. Existing folders attached to
    /// this driver will resolve via the new backend on next access.
    /// </summary>
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("config")]
    public JObject? Config { get; set; }

    [JsonProperty("credentials")]
    public DriverCredentialsDto? Credentials { get; set; }
}

public record DriverCredentialsDto
{
    [JsonProperty("access_key")]
    public string AccessKey { get; set; } = string.Empty;

    [JsonProperty("secret_key")]
    public string SecretKey { get; set; } = string.Empty;
}

public record FolderDriverAssignDto
{
    [JsonProperty("driver_id")]
    public string? DriverId { get; set; }

    /// <summary>
    /// Optional sub-path within the driver root. When null the existing
    /// folder path is preserved; when non-null (including empty string)
    /// it replaces the folder path.
    /// </summary>
    [JsonProperty("path")]
    public string? Path { get; set; }
}

public record FolderDriverInfoDto
{
    [JsonProperty("driver_id")]
    public string? DriverId { get; set; }

    [JsonProperty("driver_name")]
    public string? DriverName { get; set; }

    [JsonProperty("driver_type")]
    public string? DriverType { get; set; }

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;
}
