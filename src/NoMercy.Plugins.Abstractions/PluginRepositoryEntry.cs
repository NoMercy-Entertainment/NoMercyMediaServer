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
    [JsonPropertyName(name: "id")]
    public required Guid Id { get; init; }

    [JsonPropertyName(name: "name")]
    public required string Name { get; init; }

    [JsonPropertyName(name: "description")]
    public required string Description { get; init; }

    [JsonPropertyName(name: "author")]
    public string? Author { get; init; }

    [JsonPropertyName(name: "projectUrl")]
    public string? ProjectUrl { get; init; }

    [JsonPropertyName(name: "versions")]
    public required List<PluginVersionEntry> Versions { get; init; }
}

public class PluginVersionEntry
{
    [JsonPropertyName(name: "version")]
    public required string Version { get; init; }

    [JsonPropertyName(name: "targetAbi")]
    public string? TargetAbi { get; init; }

    [JsonPropertyName(name: "downloadUrl")]
    public required string DownloadUrl { get; init; }

    [JsonPropertyName(name: "checksum")]
    public string? Checksum { get; init; }

    [JsonPropertyName(name: "changelog")]
    public string? Changelog { get; init; }

    [JsonPropertyName(name: "timestamp")]
    public DateTime? Timestamp { get; init; }
}
