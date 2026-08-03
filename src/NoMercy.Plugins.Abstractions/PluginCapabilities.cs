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
    [JsonPropertyName("hooks")]
    public List<string> Hooks { get; init; } = [];

    [JsonPropertyName("network")]
    public PluginNetworkCapability? Network { get; init; }

    [JsonPropertyName("ui")]
    public PluginUiCapability? Ui { get; init; }

    [JsonPropertyName("rest")]
    public bool Rest { get; init; }

    [JsonPropertyName("ws")]
    public bool Ws { get; init; }
}

public class PluginNetworkCapability
{
    [JsonPropertyName("hosts")]
    public List<string> Hosts { get; init; } = [];
}

public class PluginUiCapability
{
    [JsonPropertyName("mounts")]
    public List<PluginUiMount> Mounts { get; init; } = [];
}

public class PluginUiMount
{
    [JsonPropertyName("section")]
    public required string Section { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("route")]
    public required string Route { get; init; }

    /// <summary>
    /// Which area of the app this mount lands in, from <see cref="PluginKind" />.
    ///
    /// On the mount rather than on the plugin, because a plugin is rarely one
    /// thing: a subtitle plugin belongs with video and with the library, and a
    /// scrobbler wants a page in music and its settings in the dashboard. One
    /// kind for the whole plugin would force a choice that has no right answer.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = PluginKind.Dashboard;

    /// <summary>
    /// Asks for a place in the main navigation beside the app's own sections.
    ///
    /// A request rather than a setting. If every plugin could take a top-level
    /// slot the navigation becomes a junk drawer and the plugin installed last
    /// wins the most prominent place, so the viewer decides which ones are
    /// promoted and none are by default.
    /// </summary>
    [JsonPropertyName("requestsTopLevel")]
    public bool RequestsTopLevel { get; init; }
}
