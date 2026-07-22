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

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serilog.Events;

namespace NoMercy.NmSystem.Dto;

public class LogEntry
{
    [JsonProperty(propertyName: "type")]
    [JsonPropertyName(name: "Type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "color")]
    [JsonPropertyName(name: "Color")]
    public string Color { get; set; } = string.Empty;

    [JsonProperty(propertyName: "threadId")]
    [JsonPropertyName(name: "ThreadId")]
    public int ThreadId { get; set; }

    [JsonProperty(propertyName: "time")]
    [JsonPropertyName(name: "@t")]
    public DateTime Time { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public dynamic LogMessage { get; set; } = default!;

    [NotMapped]
    [JsonProperty(propertyName: "message")]
    [JsonPropertyName(name: "Message")]
    public string Message
    {
        get => LogMessage;
        set => LogMessage = value;
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public LogEventLevel LogLevel { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "level")]
    [JsonPropertyName(name: "Level")]
    public string Level
    {
        get => LogLevel.ToString();
        set => LogLevel = Enum.Parse<LogEventLevel>(value: value);
    }
}
