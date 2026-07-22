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

public class PluginManifest
{
    [JsonPropertyName(name: "id")]
    public required Guid Id { get; init; }

    [JsonPropertyName(name: "name")]
    public required string Name { get; init; }

    [JsonPropertyName(name: "description")]
    public required string Description { get; init; }

    [JsonPropertyName(name: "version")]
    public required string Version { get; init; }

    [JsonPropertyName(name: "targetAbi")]
    public string? TargetAbi { get; init; }

    [JsonPropertyName(name: "author")]
    public string? Author { get; init; }

    [JsonPropertyName(name: "projectUrl")]
    public string? ProjectUrl { get; init; }

    [JsonPropertyName(name: "assembly")]
    public required string Assembly { get; init; }

    [JsonPropertyName(name: "autoEnabled")]
    public bool AutoEnabled { get; init; } = true;

    [JsonPropertyName(name: "capabilities")]
    public PluginCapabilities? Capabilities { get; init; }

    [JsonPropertyName(name: "signature")]
    public string? Signature { get; init; }
}
