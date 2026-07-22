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

namespace NoMercy.Api.DTOs.Management;

public record ManagementStatusDto
{
    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = "ok";

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
    public AppProcessStatusDto? AppStatus { get; set; }
}

public record AppProcessStatusDto
{
    [JsonProperty(propertyName: "running")]
    public bool Running { get; set; }

    [JsonProperty(propertyName: "pid")]
    public int? Pid { get; set; }
}

public record ManagementConfigDto
{
    [JsonProperty(propertyName: "internal_port")]
    public int InternalPort { get; set; }

    [JsonProperty(propertyName: "external_port")]
    public int ExternalPort { get; set; }

    [JsonProperty(propertyName: "server_name")]
    public string? ServerName { get; set; }

    [JsonProperty(propertyName: "library_workers")]
    public int LibraryWorkers { get; set; }

    [JsonProperty(propertyName: "import_workers")]
    public int ImportWorkers { get; set; }

    [JsonProperty(propertyName: "extras_workers")]
    public int ExtrasWorkers { get; set; }

    [JsonProperty(propertyName: "encoder_workers")]
    public int EncoderWorkers { get; set; }

    [JsonProperty(propertyName: "cron_workers")]
    public int CronWorkers { get; set; }

    [JsonProperty(propertyName: "image_workers")]
    public int ImageWorkers { get; set; }

    [JsonProperty(propertyName: "file_workers")]
    public int FileWorkers { get; set; }

    [JsonProperty(propertyName: "music_workers")]
    public int MusicWorkers { get; set; }

    [JsonProperty(propertyName: "swagger")]
    public bool Swagger { get; set; }
}

public record ManagementConfigUpdateDto
{
    [JsonProperty(propertyName: "server_name")]
    public string? ServerName { get; set; }

    [JsonProperty(propertyName: "library_workers")]
    public int? LibraryWorkers { get; set; }

    [JsonProperty(propertyName: "import_workers")]
    public int? ImportWorkers { get; set; }

    [JsonProperty(propertyName: "extras_workers")]
    public int? ExtrasWorkers { get; set; }

    [JsonProperty(propertyName: "encoder_workers")]
    public int? EncoderWorkers { get; set; }

    [JsonProperty(propertyName: "cron_workers")]
    public int? CronWorkers { get; set; }

    [JsonProperty(propertyName: "image_workers")]
    public int? ImageWorkers { get; set; }

    [JsonProperty(propertyName: "file_workers")]
    public int? FileWorkers { get; set; }

    [JsonProperty(propertyName: "music_workers")]
    public int? MusicWorkers { get; set; }
}

public record ManagementQueueStatusDto
{
    [JsonProperty(propertyName: "workers")]
    public Dictionary<string, ManagementWorkerStatusDto> Workers { get; set; } = new();

    [JsonProperty(propertyName: "pending_jobs")]
    public int PendingJobs { get; set; }

    [JsonProperty(propertyName: "failed_jobs")]
    public int FailedJobs { get; set; }
}

public record ManagementWorkerStatusDto
{
    [JsonProperty(propertyName: "active_threads")]
    public int ActiveThreads { get; set; }
}

public record AutoStartDto
{
    [JsonProperty(propertyName: "enabled")]
    public bool Enabled { get; set; }
}
