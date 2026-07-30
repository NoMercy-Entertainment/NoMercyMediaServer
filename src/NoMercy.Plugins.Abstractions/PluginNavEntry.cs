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
/// Where a plugin appears in a client's navigation, and what route the client
/// asks for when the user goes there.
/// </summary>
public class PluginNavEntry
{
    /// <summary>
    /// One of <see cref="PluginUiSection"/>. An unknown value is not an error;
    /// a client that does not recognise it falls back to
    /// <see cref="PluginUiSection.Tools"/>.
    /// </summary>
    [JsonPropertyName("section")]
    public required string Section { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("route")]
    public required string Route { get; init; }
}
