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

namespace NoMercy.Cli.Models;

internal class StatusResponse
{
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

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
}
