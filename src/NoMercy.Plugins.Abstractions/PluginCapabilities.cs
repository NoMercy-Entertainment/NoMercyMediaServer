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

public class PluginCapabilities
{
    [JsonPropertyName(name: "hooks")]
    public List<string> Hooks { get; init; } = [];

    [JsonPropertyName(name: "network")]
    public PluginNetworkCapability? Network { get; init; }

    [JsonPropertyName(name: "ui")]
    public PluginUiCapability? Ui { get; init; }

    [JsonPropertyName(name: "rest")]
    public bool Rest { get; init; }

    [JsonPropertyName(name: "ws")]
    public bool Ws { get; init; }
}

public class PluginNetworkCapability
{
    [JsonPropertyName(name: "hosts")]
    public List<string> Hosts { get; init; } = [];
}

public class PluginUiCapability
{
    [JsonPropertyName(name: "mounts")]
    public List<PluginUiMount> Mounts { get; init; } = [];
}

public class PluginUiMount
{
    [JsonPropertyName(name: "section")]
    public required string Section { get; init; }

    [JsonPropertyName(name: "label")]
    public required string Label { get; init; }

    [JsonPropertyName(name: "icon")]
    public string? Icon { get; init; }

    [JsonPropertyName(name: "route")]
    public required string Route { get; init; }
}
