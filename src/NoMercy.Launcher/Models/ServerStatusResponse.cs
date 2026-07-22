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

namespace NoMercy.Launcher.Models;

public class ServerStatusResponse
{
    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty(propertyName: "server_name")]
    public string ServerName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty(propertyName: "platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonProperty(propertyName: "architecture")]
    public string Architecture { get; set; } = string.Empty;

    [JsonProperty(propertyName: "os")]
    public string Os { get; set; } = string.Empty;

    [JsonProperty(propertyName: "uptime_seconds")]
    public long UptimeSeconds { get; set; }

    [JsonProperty(propertyName: "start_time")]
    public DateTime StartTime { get; set; }

    [JsonProperty(propertyName: "is_dev")]
    public bool IsDev { get; set; }

    [JsonProperty(propertyName: "auto_start")]
    public bool AutoStart { get; set; }

    [JsonProperty(propertyName: "is_docker")]
    public bool IsDocker { get; set; }

    [JsonProperty(propertyName: "update_available")]
    public bool UpdateAvailable { get; set; }

    [JsonProperty(propertyName: "restart_needed")]
    public bool RestartNeeded { get; set; }

    [JsonProperty(propertyName: "latest_version")]
    public string? LatestVersion { get; set; }

    [JsonProperty(propertyName: "setup_phase")]
    public string? SetupPhase { get; set; }

    [JsonProperty(propertyName: "internal_address")]
    public string? InternalAddress { get; set; }

    [JsonProperty(propertyName: "external_address")]
    public string? ExternalAddress { get; set; }

    [JsonProperty(propertyName: "app_status")]
    public AppStatusInfo? AppStatus { get; set; }
}

public class AppStatusInfo
{
    [JsonProperty(propertyName: "running")]
    public bool Running { get; set; }

    [JsonProperty(propertyName: "pid")]
    public int? Pid { get; set; }
}
