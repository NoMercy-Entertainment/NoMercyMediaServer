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

public class LogEntryResponse
{
    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty(propertyName: "color")]
    public string Color { get; set; } = string.Empty;

    [JsonProperty(propertyName: "threadId")]
    public int ThreadId { get; set; }

    [JsonProperty(propertyName: "time")]
    public DateTime Time { get; set; }

    [JsonProperty(propertyName: "level")]
    public string Level { get; set; } = string.Empty;
}
