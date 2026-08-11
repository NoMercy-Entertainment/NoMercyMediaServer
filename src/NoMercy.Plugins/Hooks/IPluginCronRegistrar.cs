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

namespace NoMercy.Plugins.Hooks;

public interface IPluginCronRegistrar
{
    void RegisterAll();

    /// <summary>
    /// Registers one plugin's scheduled jobs, whenever it turns up.
    /// <para>
    /// <see cref="RegisterAll"/> runs once, at the end of start-up, over whatever
    /// finished loading by then. On a server where loading takes longer than that
    /// — a large library, a slow disk — the pass finds nothing and every plugin
    /// that surfaces afterwards is left with no cron executors at all. The plugin
    /// then looks completely alive: its pages render, its endpoints answer, and
    /// none of its scheduled work ever runs.
    /// </para>
    /// <para>
    /// So registration reacts to <c>PluginLoadedEvent</c> as well, the way route
    /// attachment already does. Calling this for a plugin already registered
    /// replaces its executors rather than adding a second set.
    /// </para>
    /// </summary>
    void RegisterPlugin(Ulid pluginId);

    /// <summary>
    /// Stops and releases every executor registered for one plugin.
    /// <para>
    /// The counterpart to registration, and load-bearing: an executor holds the
    /// plugin instance, so leaving one behind keeps the plugin's collectible
    /// load context alive after it is disabled and its files locked on Windows.
    /// A plugin declaring several jobs leaves several.
    /// </para>
    /// </summary>
    void UnregisterPlugin(Ulid pluginId);
}
