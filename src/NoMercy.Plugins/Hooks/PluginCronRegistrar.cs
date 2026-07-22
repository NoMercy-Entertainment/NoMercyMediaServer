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
using NoMercy.Plugins.Capabilities;
using NoMercyQueue.Workers;

namespace NoMercy.Plugins.Hooks;

/// <summary>
/// Registers every active <see cref="IScheduledTaskPlugin"/> that declares the
/// <c>scheduledTask</c> capability as a cron executor instance on
/// <see cref="CronWorker"/>. Called once, post plugin-load.
/// </summary>
public class PluginCronRegistrar(IPluginManager pluginManager, CronWorker cronWorker)
    : IPluginCronRegistrar
{
    public void RegisterAll()
    {
        IReadOnlyList<PluginInfo> installed = pluginManager.GetInstalledPlugins();

        foreach (
            IScheduledTaskPlugin plugin in pluginManager.GetPluginsOfType<IScheduledTaskPlugin>()
        )
        {
            PluginCapabilities? capabilities = installed
                .FirstOrDefault(predicate: info => info.Id == plugin.Id)
                ?.Capabilities;

            if (
                !PluginCapabilityGuard.DeclaresHook(
                    capabilities: capabilities,
                    hook: PluginHookCapability.ScheduledTask
                )
            )
                continue;

            cronWorker.RegisterExecutor(executor: new PluginCronExecutor(plugin: plugin));
        }
    }
}
