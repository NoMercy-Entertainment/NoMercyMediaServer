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

/// <summary>
/// One node of a declarative view.
/// <para>
/// The node stays generic — a tag, a bag of props, children, an optional action
/// — so a client renders any view with one recursive walk and a lookup table.
/// The types live in <see cref="PluginComponentType"/> and the shape of each
/// tag's props is what <see cref="PluginViews"/> exists to get right.
/// </para>
/// </summary>
public class PluginComponent
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("component")]
    public required string Component { get; init; }

    [JsonPropertyName("props")]
    public Dictionary<string, object?> Props { get; init; } = new();

    [JsonPropertyName("items")]
    public List<PluginComponent> Items { get; init; } = [];

    [JsonPropertyName("action")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PluginActionIntent? Action { get; init; }
}
