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
    [JsonProperty(propertyName: "server")]
    public string Server { get; set; } = string.Empty;

    [JsonProperty(propertyName: "cpu")]
    public List<string> Cpu { get; set; } = [];

    [JsonProperty(propertyName: "gpu")]
    public List<string> Gpu { get; set; } = [];

    [JsonProperty(propertyName: "os")]
    public string Os { get; set; } = string.Empty;

    [JsonProperty(propertyName: "arch")]
    public string Arch { get; set; } = string.Empty;

    [JsonProperty(propertyName: "version")]
    public string? Version { get; set; }

    [JsonProperty(propertyName: "bootTime")]
    public DateTime BootTime { get; set; }

    [JsonProperty(propertyName: "os_version")]
    public string? OsVersion { get; set; }

    [JsonProperty(propertyName: "setup_complete")]
    public bool SetupComplete { get; set; }
}
