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

using NoMercy.Events;
using NoMercy.Events.Plugins;
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Hooks;

namespace NoMercy.Service.Workers;

/// <summary>
/// Keeps the cron worker's job list matching which plugins are actually running.
/// <para>
/// <c>PluginLoader</c> calls <see cref="IPluginCronRegistrar.RegisterAll"/> once, right
/// after start-up's load pass, over whatever finished loading by then. That is the whole
/// registration path, and on a real server it registered nothing at all: the pass logged
/// zero <c>Plugin loaded:</c> lines, and both installed plugins surfaced about two minutes
/// later as a <see cref="PluginLoadedEvent"/> instead. The only subscriber to that event
/// was <see cref="PluginRouteSubscriber"/>, which attaches controllers.
/// </para>
/// <para>
/// So the plugins looked completely alive and were inert. Their pages rendered, their
/// endpoints answered, and no scheduled tick ever fired — for two months, on a plugin whose
/// entire job is scheduled work. A restart did not help, because the race is between
/// loading and start-up finishing rather than anything about order.
/// </para>
/// <para>
/// Registration now reacts to the event as well, which is what route attachment has always
/// done. Both paths still run; registering a plugin twice replaces its executors rather
/// than adding a second set, so the plugins that <em>do</em> make the start-up pass are
/// unaffected.
/// </para>
/// </summary>
public class PluginCronSubscriber(
    IEventBus eventBus,
    IPluginCronRegistrar cronRegistrar,
    IPluginManager pluginManager,
    ILogger<PluginCronSubscriber> logger
) : IHostedService
{
    private readonly List<IDisposable> _subscriptions = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriptions.Add(eventBus.Subscribe<PluginLoadedEvent>(OnLoaded));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (IDisposable subscription in _subscriptions)
            subscription.Dispose();

        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    // Unregistering on disable is wired where the manager is built, not here: the
    // registrar is handed to it as a callback so the manager needs no reference back.
    private Task OnLoaded(PluginLoadedEvent loaded, CancellationToken ct)
    {
        if (!Ulid.TryParse(loaded.PluginId, out Ulid pluginId))
            return Task.CompletedTask;

        PluginInfo? info = pluginManager.GetPluginInfo(pluginId);

        if (info is null)
            return Task.CompletedTask;

        try
        {
            cronRegistrar.RegisterPlugin(pluginId);
        }
        catch (Exception failure)
        {
            // One plugin's bad schedule must not take the event handler down with
            // it — the next plugin to load would then never be registered either.
            logger.LogError(
                failure,
                "Plugin {PluginName} could not have its scheduled work registered.",
                info.Name
            );

            return Task.CompletedTask;
        }

        logger.LogInformation("Plugin {PluginName} now has its scheduled work running.", info.Name);

        return Task.CompletedTask;
    }
}
