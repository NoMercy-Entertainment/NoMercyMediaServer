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

/// <summary>Which of a plugin's routes a client is asking to render.</summary>
public class PluginViewRequest
{
    [JsonPropertyName("route")]
    public required string Route { get; init; }

    [JsonPropertyName("query")]
    public Dictionary<string, string> Query { get; init; } = new();

    /// <summary>
    /// Who is asking. A plugin that shows per-user state needs it, and reading
    /// it from the request is the only way it can get it — a view is served on
    /// the caller's behalf, not the server's.
    /// </summary>
    [JsonPropertyName("userId")]
    public string? UserId { get; init; }
}
