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
/// What the client should do, expressed as intent rather than as a call. A
/// plugin says "play this stream"; whether that means the web player, the
/// Compose player or a cast session is the client's business.
/// </summary>
public class PluginActionIntent
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("payload")]
    public Dictionary<string, object?> Payload { get; init; } = new();

    /// <summary>
    /// Shown before the action runs. Set for anything that destroys data, so
    /// the confirmation is part of the contract instead of something every
    /// plugin reimplements and one of them forgets.
    /// </summary>
    [JsonPropertyName("confirm")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PluginConfirmation? Confirm { get; init; }

    public static PluginActionIntent PlayMedia(
        string streamUrl,
        string title,
        string? artist = null,
        string? cover = null
    ) =>
        new()
        {
            Type = PluginActionType.PlayMedia,
            Payload = new()
            {
                ["streamUrl"] = streamUrl,
                ["title"] = title,
                ["artist"] = artist,
                ["cover"] = cover,
            },
        };

    public static PluginActionIntent Enqueue(
        string streamUrl,
        string title,
        string? artist = null,
        string? cover = null
    ) =>
        new()
        {
            Type = PluginActionType.Enqueue,
            Payload = new()
            {
                ["streamUrl"] = streamUrl,
                ["title"] = title,
                ["artist"] = artist,
                ["cover"] = cover,
            },
        };

    public static PluginActionIntent Navigate(string route) =>
        new()
        {
            Type = PluginActionType.Navigate,
            Payload = new() { ["route"] = route },
        };

    public static PluginActionIntent CallPlugin(
        string method,
        object? payload = null,
        string transport = PluginActionTransport.Rest,
        PluginConfirmation? confirm = null
    ) =>
        new()
        {
            Type = PluginActionType.CallPlugin,
            Payload = new()
            {
                ["method"] = method,
                ["payload"] = payload,
                ["transport"] = transport,
            },
            Confirm = confirm,
        };

    public static PluginActionIntent OpenWebView(string entryUrl) =>
        new()
        {
            Type = PluginActionType.OpenWebView,
            Payload = new() { ["entryUrl"] = entryUrl },
        };

    public static PluginActionIntent RefreshView() => new() { Type = PluginActionType.RefreshView };
}
