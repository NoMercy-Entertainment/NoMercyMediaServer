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

using System.Text.Json.Serialization;

namespace NoMercy.NmSystem.Logging;

/// <summary>
/// One serialised log line in a per-run <c>.jsonl</c> file. Field names are chosen so the
/// line is both (a) directly deserialisable into <see cref="Dto.LogEntry"/> (the dashboard
/// reader) and (b) rich enough for ad-hoc search/filter/query with grep/jq: ISO-8601
/// timestamp, category key + display name + group, level name + numeric value, scope and
/// source context.
/// </summary>
internal sealed class LogFileLine
{
    [JsonPropertyName(name: "@t")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName(name: "Type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName(name: "Category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName(name: "Group")]
    public string Group { get; set; } = string.Empty;

    [JsonPropertyName(name: "Level")]
    public string Level { get; set; } = string.Empty;

    [JsonPropertyName(name: "LevelValue")]
    public int LevelValue { get; set; }

    [JsonPropertyName(name: "Color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName(name: "Message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName(name: "Scope")]
    public string? Scope { get; set; }

    [JsonPropertyName(name: "Source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName(name: "ThreadId")]
    public int ThreadId { get; set; }

    [JsonPropertyName(name: "Exception")]
    public string? Exception { get; set; }
}
