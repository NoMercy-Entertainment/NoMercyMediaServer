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

namespace NoMercy.Plugins.Abstractions;

public class PluginRepositoryEntry
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(PluginIdJsonConverter))]
    public required Ulid Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("projectUrl")]
    public string? ProjectUrl { get; init; }

    [JsonPropertyName("versions")]
    public required List<PluginVersionEntry> Versions { get; init; }
}

public class PluginVersionEntry
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("targetAbi")]
    public string? TargetAbi { get; init; }

    [JsonPropertyName("downloadUrl")]
    public required string DownloadUrl { get; init; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; init; }

    [JsonPropertyName("changelog")]
    public string? Changelog { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; init; }
}
