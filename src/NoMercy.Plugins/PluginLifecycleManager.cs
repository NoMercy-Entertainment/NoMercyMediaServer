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
        if (!_registry.TryGetValue(id: pluginId, plugin: out LoadedPlugin? loaded))
        {
            throw new InvalidOperationException(message: $"Plugin {pluginId} is not installed.");
        }

        if (loaded.Info.Status == PluginStatus.Active)
        {
            return;
        }

        if (loaded.Instance is null && loaded.Info.AssemblyPath is not null)
        {
            await _loader.LoadPluginAssemblyAsync(assemblyPath: loaded.Info.AssemblyPath, ct: ct);
            return;
        }

        if (loaded.Instance is not null)
        {
            try
            {
                string dataFolder = Path.Combine(path1: _pluginsPath, path2: "data", path3: pluginId.ToString(format: "N"));
                if (!_storage.Exists(path: dataFolder))
                {
                    _storage.CreateDirectory(path: dataFolder);
                }

                PluginContext context = new(
                    eventBus: _eventBus,
                    services: _serviceProvider,
                    logger: _logger,
                    dataFolderPath: dataFolder,
                    storage: _storage,
                    capabilities: loaded.Info.Capabilities
                );
                loaded.Instance.Initialize(context: context);
                PluginLifecycle.Transition(info: loaded.Info, newStatus: PluginStatus.Active);

                await _eventBus.PublishAsync(
                    @event: new PluginLoadedEvent
                    {
                        PluginId = pluginId.ToString(),
                        PluginName = loaded.Info.Name,
                        Version = loaded.Info.Version.ToString(),
                    },
                    ct: ct
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
                    @event: new PluginErrorOccurredEvent
                    {
                        PluginId = pluginId.ToString(),
                        PluginName = loaded.Info.Name,
                        ErrorMessage = ex.Message,
                        ExceptionType = ex.GetType().Name,
                    },
                    ct: ct
                );
            }
        }
    }

    public Task DisablePluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        if (!_registry.TryGetValue(id: pluginId, plugin: out LoadedPlugin? loaded))
        {
            throw new InvalidOperationException(message: $"Plugin {pluginId} is not installed.");
        }

        if (loaded.Info.Status == PluginStatus.Disabled)
        {
            return Task.CompletedTask;
        }

        loaded.Instance?.Dispose();
        PluginLifecycle.Transition(info: loaded.Info, newStatus: PluginStatus.Disabled);

        return Task.CompletedTask;
    }

    public Task UninstallPluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        if (!_registry.TryRemove(id: pluginId, plugin: out LoadedPlugin? loaded))
        {
            throw new InvalidOperationException(message: $"Plugin {pluginId} is not installed.");
        }

        loaded.Instance?.Dispose();
        loaded.LoadContext?.Unload();
        PluginLifecycle.Transition(info: loaded.Info, newStatus: PluginStatus.Deleted);

        if (loaded.Info.AssemblyPath is not null)
        {
            string? pluginDir = Path.GetDirectoryName(path: loaded.Info.AssemblyPath);
            if (pluginDir is not null && _storage.Exists(path: pluginDir))
            {
                try
                {
                    _storage.DeleteDirectory(path: pluginDir, recursive: true);
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
                        message: "Could not delete plugin directory {PluginDir}. Files may be locked.",
                        args: pluginDir
                    );
                }
            }
        }

        return Task.CompletedTask;
    }
}
