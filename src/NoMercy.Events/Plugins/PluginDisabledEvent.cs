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

namespace NoMercy.Events.Plugins;

/// <summary>
/// A plugin stopped. Enabling already announced itself with
/// <see cref="PluginLoadedEvent"/> and nothing announced the other direction,
/// so anything holding a plugin's registration — its routes, its hub handler —
/// had no moment to let go of it.
/// </summary>
public sealed class PluginDisabledEvent : EventBase
{
    public override string Source => "PluginManager";

    public required string PluginId { get; init; }
    public required string PluginName { get; init; }

    /// <summary>Whether the plugin is gone rather than merely stopped.</summary>
    public bool Uninstalled { get; init; }
}
