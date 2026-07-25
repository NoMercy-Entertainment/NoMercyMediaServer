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
using Microsoft.Extensions.Logging;
using NoMercy.Events;
using NoMercy.Events.Plugins;
using NoMercy.Plugins.Abstractions;
using NoMercy.Storage;

namespace NoMercy.Plugins;

/// <summary>
/// Handles plugin lifecycle state transitions (enable, disable, uninstall),
/// coordinating the registry, the loader, and lifecycle events. Splitting this
/// out of <see cref="PluginManager"/> keeps each lifecycle operation in one
/// focused place.
/// </summary>
internal sealed class PluginLifecycleManager(
    IEventBus eventBus,
    IServiceProvider serviceProvider,
    ILogger logger,
    string pluginsPath,
    IStorage storage,
    IPluginRegistry registry,
    PluginLoader loader
)
{
    private readonly IEventBus _eventBus = eventBus;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger _logger = logger;
    private readonly string _pluginsPath = pluginsPath;
    private readonly IStorage _storage = storage;
    private readonly IPluginRegistry _registry = registry;
    private readonly PluginLoader _loader = loader;

    public async Task EnablePluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        if (!_registry.TryGetValue(pluginId, out LoadedPlugin? loaded))
        {
            throw new InvalidOperationException($"Plugin {pluginId} is not installed.");
        }

        if (loaded.Info.Status == PluginStatus.Active)
        {
            return;
        }

        if (loaded.Instance is null && loaded.Info.AssemblyPath is not null)
        {
            await _loader.LoadPluginAssemblyAsync(loaded.Info.AssemblyPath, ct);
            return;
        }

        if (loaded.Instance is not null)
        {
            try
            {
                string dataFolder = Path.Combine(_pluginsPath, "data", pluginId.ToString("N"));
                if (!_storage.Exists(dataFolder))
                {
                    _storage.CreateDirectory(dataFolder);
                }

                PluginContext context = new(
                    _eventBus,
                    _serviceProvider,
                    _logger,
                    dataFolder,
                    _storage,
                    loaded.Info.Capabilities
                );
                loaded.Instance.Initialize(context);
                PluginLifecycle.Transition(loaded.Info, PluginStatus.Active);

                await _eventBus.PublishAsync(
                    new PluginLoadedEvent
                    {
                        PluginId = pluginId.ToString(),
                        PluginName = loaded.Info.Name,
                        Version = loaded.Info.Version.ToString(),
                    },
                    ct
                );
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Not PluginLifecycle.Transition: this recovery path runs from
                // whatever pre-Active status let us reach the Instance-not-null
                // branch above (Disabled or Malfunctioned — Active already
                // short-circuited at the top of this method), and neither of
                // those has Malfunctioned in its allowed-transitions set, so
                // routing through Transition here throws and replaces this
                // graceful failure record with an unhandled exception. Setting
                // Status directly matches how PluginLoader already records a
                // malfunction on a freshly built PluginInfo.
                loaded.Info.Status = PluginStatus.Malfunctioned;

                await _eventBus.PublishAsync(
                    new PluginErrorOccurredEvent
                    {
                        PluginId = pluginId.ToString(),
                        PluginName = loaded.Info.Name,
                        ErrorMessage = ex.Message,
                        ExceptionType = ex.GetType().Name,
                    },
                    ct
                );
            }
        }
    }

    public Task DisablePluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        if (!_registry.TryGetValue(pluginId, out LoadedPlugin? loaded))
        {
            throw new InvalidOperationException($"Plugin {pluginId} is not installed.");
        }

        if (loaded.Info.Status == PluginStatus.Disabled)
        {
            return Task.CompletedTask;
        }

        loaded.Instance?.Dispose();
        PluginLifecycle.Transition(loaded.Info, PluginStatus.Disabled);

        return Task.CompletedTask;
    }

    public Task UninstallPluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        if (!_registry.TryRemove(pluginId, out LoadedPlugin? loaded))
        {
            throw new InvalidOperationException($"Plugin {pluginId} is not installed.");
        }

        loaded.Instance?.Dispose();
        loaded.LoadContext?.Unload();
        PluginLifecycle.Transition(loaded.Info, PluginStatus.Deleted);

        if (loaded.Info.AssemblyPath is not null)
        {
            string? pluginDir = Path.GetDirectoryName(loaded.Info.AssemblyPath);
            if (pluginDir is not null && _storage.Exists(pluginDir))
            {
                try
                {
                    _storage.DeleteDirectory(pluginDir, true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // The plugin's own assembly was just Unload()ed a few lines
                    // above, but AssemblyLoadContext.Unload() is asynchronous —
                    // the file can still be resident until the GC actually
                    // collects it. Windows reports that exact "still resident"
                    // condition as UnauthorizedAccessException, not IOException,
                    // for a directory delete — catching only IOException let a
                    // routine, expected race during uninstall crash the caller
                    // instead of logging the same "files may be locked" warning
                    // this catch already exists to produce.
                    _logger.LogWarning(
                        "Could not delete plugin directory {PluginDir}. Files may be locked.",
                        pluginDir
                    );
                }
            }
        }

        return Task.CompletedTask;
    }
}
