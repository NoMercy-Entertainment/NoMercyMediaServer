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
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Api.DTOs.Dashboard;

public record PluginNavEntryDto
{
    /// <summary>
    /// Always one a client knows: an unrecognised section is resolved to
    /// <see cref="PluginUiSection.Tools"/> here rather than shipped through and
    /// dropped by whichever client happens not to know it.
    /// </summary>
    [JsonProperty("section")]
    public required string Section { get; init; }

    [JsonProperty("label")]
    public required string Label { get; init; }

    [JsonProperty("icon")]
    public string? Icon { get; init; }

    [JsonProperty("route")]
    public required string Route { get; init; }

    public static PluginNavEntryDto From(PluginNavEntry entry) =>
        new()
        {
            Section = PluginUiSection.OrFallback(entry.Section),
            Label = entry.Label,
            Icon = entry.Icon,
            Route = entry.Route,
        };

    public static PluginNavEntryDto From(PluginUiMount mount) =>
        new()
        {
            Section = PluginUiSection.OrFallback(mount.Section),
            Label = mount.Label,
            Icon = mount.Icon,
            Route = mount.Route,
        };
}
