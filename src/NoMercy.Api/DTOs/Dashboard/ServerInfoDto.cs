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

public record ServerInfoDto
{
    [JsonProperty("server")]
    public string Server { get; set; } = string.Empty;

    [JsonProperty("cpu")]
    public List<string> Cpu { get; set; } = [];

    [JsonProperty("gpu")]
    public List<string> Gpu { get; set; } = [];

    [JsonProperty("os")]
    public string Os { get; set; } = string.Empty;

    [JsonProperty("arch")]
    public string Arch { get; set; } = string.Empty;

    [JsonProperty("version")]
    public string? Version { get; set; }

    [JsonProperty("bootTime")]
    public DateTime BootTime { get; set; }

    [JsonProperty("os_version")]
    public string? OsVersion { get; set; }

    [JsonProperty("setup_complete")]
    public bool SetupComplete { get; set; }
}
