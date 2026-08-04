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
