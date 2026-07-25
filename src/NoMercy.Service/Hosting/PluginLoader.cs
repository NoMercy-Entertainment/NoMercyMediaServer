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

using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Hooks;

namespace NoMercy.Service.Hosting;

public class PluginLoader : IPluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly IPluginManager _pluginManager;
    private readonly IPluginCronRegistrar _cronRegistrar;

    public PluginLoader(
        ILogger<PluginLoader> logger,
        IPluginManager pluginManager,
        IPluginCronRegistrar cronRegistrar
    )
    {
        _logger = logger;
        _pluginManager = pluginManager;
        _cronRegistrar = cronRegistrar;
    }

    public async Task<IReadOnlyList<PluginLoadResult>> LoadPlugins(CancellationToken ct)
    {
        _logger.LogInformation("Loading plugins...");

        IReadOnlyList<PluginLoadResult> loadedPlugins = await _pluginManager.LoadAllAsync(ct);

        foreach (PluginLoadResult pluginResult in loadedPlugins)
        {
            _logger.LogInformation(
                "Plugin loaded: {Name} {Version} ({PluginId})", [pluginResult.Name, pluginResult.Version, pluginResult.PluginId]
            );
        }

        _cronRegistrar.RegisterAll();

        return loadedPlugins;
    }
}
