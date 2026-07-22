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
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "config")]
    public JObject? Config { get; set; }

    [JsonProperty(propertyName: "credentials_configured")]
    public bool CredentialsConfigured { get; set; }

    // True for the built-in system local driver — the web client uses this to
    // hide the driver from the manage-drivers UI while still resolving its id
    // for folder picking.
    [JsonProperty(propertyName: "is_system")]
    public bool IsSystem { get; set; }

    [JsonProperty(propertyName: "folder_count")]
    public int FolderCount { get; set; }

    [JsonProperty(propertyName: "created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty(propertyName: "updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}

public record CreateDriverRequestDto
{
    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "config")]
    public JObject? Config { get; set; }

    [JsonProperty(propertyName: "credentials")]
    public DriverCredentialsDto? Credentials { get; set; }
}

public record UpdateDriverRequestDto
{
    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    /// <summary>
    /// Optional. Allows switching driver type (e.g. local → nfs) on update.
    /// Validation runs against the new type. Existing folders attached to
    /// this driver will resolve via the new backend on next access.
    /// </summary>
    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }

    [JsonProperty(propertyName: "config")]
    public JObject? Config { get; set; }

    [JsonProperty(propertyName: "credentials")]
    public DriverCredentialsDto? Credentials { get; set; }
}

public record DriverCredentialsDto
{
    [JsonProperty(propertyName: "access_key")]
    public string AccessKey { get; set; } = string.Empty;

    [JsonProperty(propertyName: "secret_key")]
    public string SecretKey { get; set; } = string.Empty;
}

public record FolderDriverAssignDto
{
    [JsonProperty(propertyName: "driver_id")]
    public string? DriverId { get; set; }

    /// <summary>
    /// Optional sub-path within the driver root. When null the existing
    /// folder path is preserved; when non-null (including empty string)
    /// it replaces the folder path.
    /// </summary>
    [JsonProperty(propertyName: "path")]
    public string? Path { get; set; }
}

public record FolderDriverInfoDto
{
    [JsonProperty(propertyName: "driver_id")]
    public string? DriverId { get; set; }

    [JsonProperty(propertyName: "driver_name")]
    public string? DriverName { get; set; }

    [JsonProperty(propertyName: "driver_type")]
    public string? DriverType { get; set; }

    [JsonProperty(propertyName: "path")]
    public string Path { get; set; } = string.Empty;
}
