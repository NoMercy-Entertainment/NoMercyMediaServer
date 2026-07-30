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
/// What a plugin puts on screen for one route: either a declarative tree the
/// client renders natively, or a webview it does not.
/// <para>
/// Exactly one of the two is set. The webview is the escape hatch and is meant
/// to stay one — a table with progress bars is ordinary UI, and
/// <see cref="PluginComponentType"/> can express it.
/// </para>
/// </summary>
public class PluginView
{
    [JsonPropertyName("components")]
    public List<PluginComponent>? Components { get; init; }

    [JsonPropertyName("webView")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PluginWebViewRef? WebView { get; init; }

    /// <summary>
    /// How often the client should re-fetch this route, in seconds. Zero means
    /// never — a view that changes on its own says so here instead of every
    /// client guessing a poll interval, and a plugin with a hub connection
    /// pushes instead.
    /// </summary>
    [JsonPropertyName("refreshInterval")]
    public int RefreshInterval { get; init; }
}
