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

public class PluginRepositoryManifest
{
    [JsonPropertyName(name: "name")]
    public required string Name { get; init; }

    [JsonPropertyName(name: "url")]
    public string? Url { get; init; }

    [JsonPropertyName(name: "plugins")]
    public required List<PluginRepositoryEntry> Plugins { get; init; }
}
