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

public class UpdateCheckResult
{
    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty(propertyName: "message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty(propertyName: "use_installer")]
    public bool UseInstaller { get; set; }

    [JsonProperty(propertyName: "latest_version")]
    public string? LatestVersion { get; set; }

    [JsonProperty(propertyName: "path")]
    public string? Path { get; set; }
}

public class ActivityInfo
{
    [JsonProperty(propertyName: "active_streams")]
    public int ActiveStreams { get; set; }

    [JsonProperty(propertyName: "active_encodes")]
    public int ActiveEncodes { get; set; }

    [JsonProperty(propertyName: "can_interrupt_safely")]
    public bool CanInterruptSafely { get; set; }
}
