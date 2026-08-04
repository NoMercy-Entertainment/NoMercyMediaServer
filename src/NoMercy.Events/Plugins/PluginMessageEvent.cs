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

using System.Text.Json;
using System.Text.Json.Nodes;

namespace NoMercy.Events.Plugins;

/// <summary>
/// An event raised by a plugin, in a type the host can actually bind to.
/// <para>
/// The bus was never the problem — a plugin can publish today. The problem is
/// identity: a plugin-defined event class lives in that plugin's collectible
/// load context, so its type is invisible to the host and to every other
/// plugin. A handler in the server cannot name the type, so it cannot
/// subscribe, and publishing one is decoration.
/// </para>
/// <para>
/// This is the outer type both sides already share. Host and plugin agree on
/// the envelope rather than on the payload class, which is the same trade the
/// wire format makes and the reason it works across every client. A subscriber
/// binds <c>PluginMessageEvent</c>, filters on
/// <see cref="PluginId"/> and <see cref="Name"/>, and reads the payload it
/// expects.
/// </para>
/// <para>
/// <see cref="Payload"/> is <see cref="JsonNode"/> rather than a JSON library's
/// own token type: it ships in the shared framework, so it resolves from the
/// default load context automatically and needs no allowlist entry. A payload
/// typed as a NuGet package's node would be a type the plugin cannot bind to —
/// the identical bug one layer down.
/// </para>
/// </summary>
public sealed class PluginMessageEvent : EventBase
{
    public override string Source => "Plugin";

    /// <summary>Which plugin raised it. Subscribers filter on this first.</summary>
    public required Ulid PluginId { get; init; }

    /// <summary>
    /// The plugin's own name for the event, e.g. <c>download.completed</c>.
    /// Namespaced by the plugin, so two plugins using the same name do not
    /// collide once <see cref="PluginId"/> is taken into account.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The payload, or null when the name alone says everything. Untrusted: it
    /// came from a plugin, so a subscriber validates it rather than assuming a
    /// shape.
    /// </summary>
    public JsonNode? Payload { get; init; }

    /// <summary>
    /// Builds an event from any serialisable payload, so a plugin does not
    /// hand-assemble a node to raise one.
    /// </summary>
    public static PluginMessageEvent From<T>(
        Ulid pluginId,
        string name,
        T payload,
        JsonSerializerOptions? options = null
    ) =>
        new()
        {
            PluginId = pluginId,
            Name = name,
            Payload = JsonSerializer.SerializeToNode(payload, options),
        };

    /// <summary>
    /// Reads the payload back as <typeparamref name="T"/>, or null when it is
    /// absent or does not fit. Returning null rather than throwing keeps one
    /// malformed plugin payload from taking down a host subscriber.
    /// </summary>
    public T? PayloadAs<T>(JsonSerializerOptions? options = null)
        where T : class
    {
        if (Payload is null)
            return null;

        try
        {
            return Payload.Deserialize<T>(options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
