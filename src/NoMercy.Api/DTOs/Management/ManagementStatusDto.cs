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
    [JsonProperty("status")]
    public string Status { get; set; } = "ok";

    [JsonProperty("server_name")]
    public string ServerName { get; set; } = string.Empty;

    [JsonProperty("version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonProperty("architecture")]
    public string Architecture { get; set; } = string.Empty;

    [JsonProperty("os")]
    public string Os { get; set; } = string.Empty;

    [JsonProperty("uptime_seconds")]
    public long UptimeSeconds { get; set; }

    [JsonProperty("start_time")]
    public DateTime StartTime { get; set; }

    [JsonProperty("is_dev")]
    public bool IsDev { get; set; }

    [JsonProperty("auto_start")]
    public bool AutoStart { get; set; }

    [JsonProperty("is_docker")]
    public bool IsDocker { get; set; }

    [JsonProperty("update_available")]
    public bool UpdateAvailable { get; set; }

    [JsonProperty("restart_needed")]
    public bool RestartNeeded { get; set; }

    [JsonProperty("latest_version")]
    public string? LatestVersion { get; set; }

    [JsonProperty("setup_phase")]
    public string? SetupPhase { get; set; }

    [JsonProperty("internal_address")]
    public string? InternalAddress { get; set; }

    [JsonProperty("external_address")]
    public string? ExternalAddress { get; set; }

    [JsonProperty("connectivity")]
    public ConnectivityStatusDto? Connectivity { get; set; }

    [JsonProperty("app_status")]
    public AppProcessStatusDto? AppStatus { get; set; }
}

public record ConnectivityStatusDto
{
    /// <summary>Starting, Evaluating, DirectAccess, HolePunched, Tunneled or LocalOnly.</summary>
    [JsonProperty("state")]
    public string? State { get; set; }

    /// <summary>Which transport is actually carrying remote traffic.</summary>
    [JsonProperty("transport")]
    public string? Transport { get; set; }

    /// <summary>Auto, or the transport this server has been pinned to.</summary>
    [JsonProperty("mode")]
    public string? Mode { get; set; }

    [JsonProperty("nat_status")]
    public string? NatStatus { get; set; }

    /// <summary>
    /// Whether a tunnel exists for this server, is still being provisioned, or could not be
    /// checked. "Could not be checked" used to be indistinguishable from "you do not have one".
    /// </summary>
    [JsonProperty("tunnel_availability")]
    public string? TunnelAvailability { get; set; }

    [JsonProperty("port_forwarded")]
    public bool PortForwarded { get; set; }
}

public record AppProcessStatusDto
{
    [JsonProperty("running")]
    public bool Running { get; set; }

    [JsonProperty("pid")]
    public int? Pid { get; set; }
}

public record ManagementConfigDto
{
    [JsonProperty("internal_port")]
    public int InternalPort { get; set; }

    [JsonProperty("external_port")]
    public int ExternalPort { get; set; }

    [JsonProperty("server_name")]
    public string? ServerName { get; set; }

    [JsonProperty("library_workers")]
    public int LibraryWorkers { get; set; }

    [JsonProperty("import_workers")]
    public int ImportWorkers { get; set; }

    [JsonProperty("extras_workers")]
    public int ExtrasWorkers { get; set; }

    [JsonProperty("encoder_workers")]
    public int EncoderWorkers { get; set; }

    [JsonProperty("cron_workers")]
    public int CronWorkers { get; set; }

    [JsonProperty("image_workers")]
    public int ImageWorkers { get; set; }

    [JsonProperty("file_workers")]
    public int FileWorkers { get; set; }

    [JsonProperty("music_workers")]
    public int MusicWorkers { get; set; }

    [JsonProperty("swagger")]
    public bool Swagger { get; set; }
}

public record ManagementConfigUpdateDto
{
    [JsonProperty("server_name")]
    public string? ServerName { get; set; }

    [JsonProperty("library_workers")]
    public int? LibraryWorkers { get; set; }

    [JsonProperty("import_workers")]
    public int? ImportWorkers { get; set; }

    [JsonProperty("extras_workers")]
    public int? ExtrasWorkers { get; set; }

    [JsonProperty("encoder_workers")]
    public int? EncoderWorkers { get; set; }

    [JsonProperty("cron_workers")]
    public int? CronWorkers { get; set; }

    [JsonProperty("image_workers")]
    public int? ImageWorkers { get; set; }

    [JsonProperty("file_workers")]
    public int? FileWorkers { get; set; }

    [JsonProperty("music_workers")]
    public int? MusicWorkers { get; set; }
}

public record ManagementQueueStatusDto
{
    [JsonProperty("workers")]
    public Dictionary<string, ManagementWorkerStatusDto> Workers { get; set; } = new();

    [JsonProperty("pending_jobs")]
    public int PendingJobs { get; set; }

    [JsonProperty("failed_jobs")]
    public int FailedJobs { get; set; }
}

public record ManagementWorkerStatusDto
{
    [JsonProperty("active_threads")]
    public int ActiveThreads { get; set; }
}

public record AutoStartDto
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }
}
