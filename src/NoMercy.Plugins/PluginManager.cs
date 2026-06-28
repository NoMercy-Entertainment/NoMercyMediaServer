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

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using NoMercy.Events;
using NoMercy.Events.Plugins;
using NoMercy.Plugins.Abstractions;
using NoMercy.Storage;

namespace NoMercy.Plugins;

public class PluginManager : IPluginManager, IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginManager> _logger;
    private readonly string _pluginsPath;
    private readonly IStorage _storage;
    private readonly IStorageDriver _driver;
    private readonly IPluginRegistry _registry;

    public PluginManager(
        IEventBus eventBus,
        IServiceProvider serviceProvider,
        ILogger<PluginManager> logger,
        string pluginsPath,
        IStorage storage,
        IStorageDriver driver
    )
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pluginsPath = pluginsPath ?? throw new ArgumentNullException(nameof(pluginsPath));
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _registry = new PluginRegistry();
    }

    // AssemblyLoadContext and AssemblyDependencyResolver are raw filesystem APIs and need a
    // real absolute path. IStorage hands back paths relative to the plugins root, so resolve
    // them against the absolute local root before loading. An already-absolute path is returned
    // unchanged. Plugin assemblies are always local — they cannot be loaded from a remote driver.
    private string ToLocalAssemblyPath(string storagePath)
    {
        return Path.GetFullPath(Path.Combine(_pluginsPath, storagePath));
    }

    public IReadOnlyList<PluginInfo> GetInstalledPlugins()
    {
        return _registry.Values.Select(lp => lp.Info).ToList().AsReadOnly();
    }

    public async Task InstallPluginAsync(string packagePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        string fullPath = Path.GetFullPath(packagePath);

        // Source assembly may be anywhere on disk (user-supplied install path).
        // Use the raw backend for the existence check and copy-in; the destination
        // is always inside the allowlisted plugin root so _storage covers it.
        if (!_driver.FileExists(fullPath))
        {
            throw new FileNotFoundException($"Plugin assembly not found: {fullPath}", fullPath);
        }

        string pluginName = Path.GetFileNameWithoutExtension(fullPath);
        string pluginDir = Path.Combine(_pluginsPath, pluginName);

        if (!_storage.Exists(pluginDir))
        {
            _storage.CreateDirectory(pluginDir);
        }

        string destPath = Path.Combine(pluginDir, Path.GetFileName(fullPath));
        _driver.CopyFile(fullPath, destPath, overwrite: true);

        await LoadPluginAssemblyAsync(destPath, ct);
    }

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
            await LoadPluginAssemblyAsync(loaded.Info.AssemblyPath, ct);
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
                    _storage
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
                PluginLifecycle.Transition(loaded.Info, PluginStatus.Malfunctioned);

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
                    _storage.DeleteDirectory(pluginDir, recursive: true);
                }
                catch (IOException)
                {
                    _logger.LogWarning(
                        "Could not delete plugin directory {PluginDir}. Files may be locked.",
                        pluginDir
                    );
                }
            }
        }

        return Task.CompletedTask;
    }

    public async Task LoadPluginsFromDirectoryAsync(CancellationToken ct = default)
    {
        if (!_storage.Exists(_pluginsPath))
        {
            return;
        }

        IReadOnlyList<StorageEntry> entries = _storage.List(_pluginsPath, null, recursive: false);
        foreach (StorageEntry entry in entries)
        {
            if (!entry.IsDirectory)
            {
                continue;
            }

            string pluginDir = entry.Path;
            string dirName = Path.GetFileName(pluginDir);
            if (dirName is "configurations" or "data")
            {
                continue;
            }

            try
            {
                string manifestPath = Path.Combine(pluginDir, "plugin.json");
                if (_storage.Exists(manifestPath))
                {
                    await LoadPluginFromManifestAsync(manifestPath, ct);
                    continue;
                }

                IReadOnlyList<StorageEntry> dllEntries = _storage.List(
                    pluginDir,
                    "*.dll",
                    recursive: false
                );
                foreach (StorageEntry dllEntry in dllEntries)
                {
                    if (!dllEntry.IsDirectory)
                    {
                        await LoadPluginAssemblyAsync(dllEntry.Path, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                // Defense in depth: the load helpers already isolate their own
                // failures, but an unexpected throw must not stop the remaining
                // plugin directories from being scanned.
                _logger.LogError(
                    ex,
                    "Unexpected failure while loading plugin directory {PluginDir}; skipping it.",
                    pluginDir
                );
            }
        }
    }

    public async Task<IReadOnlyList<PluginLoadResult>> LoadAllAsync(CancellationToken ct = default)
    {
        if (!_storage.Exists(_pluginsPath))
        {
            _logger.LogInformation(
                "Plugins directory missing: {Path}. No plugins loaded.",
                _pluginsPath
            );
            return [];
        }

        await LoadPluginsFromDirectoryAsync(ct);

        List<PluginLoadResult> results = [];
        foreach (LoadedPlugin loaded in _registry.Values)
        {
            if (loaded.Instance is not null && loaded.Info.Status == PluginStatus.Active)
            {
                results.Add(
                    new(
                        loaded.Info.Id,
                        loaded.Info.Name,
                        loaded.Info.Version.ToString(),
                        loaded.Instance
                    )
                );
            }
        }

        return results;
    }

    internal async Task LoadPluginFromManifestAsync(
        string manifestPath,
        CancellationToken ct = default
    )
    {
        string pluginDir = Path.GetDirectoryName(manifestPath)!;

        try
        {
            PluginManifest manifest = await PluginManifestParser.ParseFileAsync(
                manifestPath,
                _storage,
                ct
            );
            string assemblyPath = Path.Combine(pluginDir, manifest.Assembly);

            if (!_storage.Exists(assemblyPath))
            {
                _logger.LogWarning(
                    "Plugin manifest {ManifestPath} references assembly '{Assembly}' which was not found.",
                    manifestPath,
                    manifest.Assembly
                );

                await _eventBus.PublishAsync(
                    new PluginErrorOccurredEvent
                    {
                        PluginId = manifest.Id.ToString(),
                        PluginName = manifest.Name,
                        ErrorMessage =
                            $"Assembly '{manifest.Assembly}' not found in plugin directory.",
                        ExceptionType = nameof(FileNotFoundException),
                    },
                    ct
                );

                return;
            }

            string absoluteAssemblyPath = ToLocalAssemblyPath(assemblyPath);
            PluginLoadContext loadContext = new(absoluteAssemblyPath);

            try
            {
                Assembly assembly = loadContext.LoadFromAssemblyPath(absoluteAssemblyPath);
                List<Type> pluginTypes = assembly
                    .GetTypes()
                    .Where(t =>
                        typeof(IPlugin).IsAssignableFrom(t)
                        && t is { IsAbstract: false, IsInterface: false }
                    )
                    .ToList();

                bool foundPlugin = false;

                foreach (Type pluginType in pluginTypes)
                {
                    IPlugin? instance = PluginInstanceFactory.Create(_serviceProvider, pluginType);
                    if (instance is null)
                    {
                        continue;
                    }

                    PluginStatus initialStatus = manifest.AutoEnabled
                        ? PluginStatus.Active
                        : PluginStatus.Disabled;

                    if (manifest.AutoEnabled)
                    {
                        string dataFolder = Path.Combine(
                            _pluginsPath,
                            "data",
                            instance.Id.ToString("N")
                        );
                        if (!_storage.Exists(dataFolder))
                        {
                            _storage.CreateDirectory(dataFolder);
                        }

                        PluginContext context = new(
                            _eventBus,
                            _serviceProvider,
                            _logger,
                            dataFolder,
                            _storage
                        );

                        try
                        {
                            instance.Initialize(context);
                        }
                        catch (Exception ex)
                        {
                            initialStatus = PluginStatus.Malfunctioned;
                            instance.Dispose();

                            PluginInfo errorInfo = PluginManifestParser.ToPluginInfo(
                                manifest,
                                assemblyPath,
                                initialStatus,
                                manifestPath
                            );

                            LoadedPlugin errorLoaded = new(errorInfo, null, loadContext);
                            _registry[manifest.Id] = errorLoaded;
                            foundPlugin = true;

                            await _eventBus.PublishAsync(
                                new PluginErrorOccurredEvent
                                {
                                    PluginId = manifest.Id.ToString(),
                                    PluginName = manifest.Name,
                                    ErrorMessage = ex.Message,
                                    ExceptionType = ex.GetType().Name,
                                },
                                ct
                            );

                            continue;
                        }
                    }

                    PluginInfo info = PluginManifestParser.ToPluginInfo(
                        manifest,
                        assemblyPath,
                        initialStatus,
                        manifestPath
                    );

                    IPlugin? storedInstance =
                        initialStatus == PluginStatus.Active ? instance : null;
                    LoadedPlugin loaded = new(info, storedInstance, loadContext);
                    _registry[manifest.Id] = loaded;
                    foundPlugin = true;

                    if (initialStatus == PluginStatus.Active)
                    {
                        await _eventBus.PublishAsync(
                            new PluginLoadedEvent
                            {
                                PluginId = manifest.Id.ToString(),
                                PluginName = manifest.Name,
                                Version = manifest.Version,
                            },
                            ct
                        );
                    }
                }

                if (!foundPlugin)
                {
                    loadContext.Unload();
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                string errorMessage = string.Join(
                    "; ",
                    ex.LoaderExceptions.Where(e => e is not null).Select(e => e!.Message)
                );

                _logger.LogWarning(
                    "Failed to load plugin assembly {AssemblyPath}: {Error}",
                    assemblyPath,
                    errorMessage
                );

                await _eventBus.PublishAsync(
                    new PluginErrorOccurredEvent
                    {
                        PluginId = manifest.Id.ToString(),
                        PluginName = manifest.Name,
                        ErrorMessage = errorMessage,
                        ExceptionType = nameof(ReflectionTypeLoadException),
                    },
                    ct
                );

                loadContext.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Failed to load plugin assembly {AssemblyPath}: {Error}",
                    assemblyPath,
                    ex.Message
                );

                await _eventBus.PublishAsync(
                    new PluginErrorOccurredEvent
                    {
                        PluginId = manifest.Id.ToString(),
                        PluginName = manifest.Name,
                        ErrorMessage = ex.Message,
                        ExceptionType = ex.GetType().Name,
                    },
                    ct
                );

                loadContext.Unload();
            }
        }
        catch (Exception ex)
        {
            string pluginName = Path.GetFileName(pluginDir);

            _logger.LogWarning(
                "Failed to parse plugin manifest {ManifestPath}: {Error}",
                manifestPath,
                ex.Message
            );

            await _eventBus.PublishAsync(
                new PluginErrorOccurredEvent
                {
                    PluginId = Guid.Empty.ToString(),
                    PluginName = pluginName,
                    ErrorMessage = $"Invalid plugin manifest: {ex.Message}",
                    ExceptionType = ex.GetType().Name,
                },
                ct
            );
        }
    }

    internal async Task LoadPluginAssemblyAsync(string assemblyPath, CancellationToken ct = default)
    {
        string absoluteAssemblyPath = ToLocalAssemblyPath(assemblyPath);
        PluginLoadContext loadContext;
        try
        {
            // AssemblyDependencyResolver reads the assembly's .deps.json via the
            // native host. On Linux it throws for a DLL with no/invalid deps
            // manifest (a stray file, a plugin shipped without its deps);
            // Windows tolerates it. Constructing outside the try let that escape
            // and abort discovery of every other plugin — guard it so a bad
            // assembly is skipped and reported, not fatal.
            loadContext = new(absoluteAssemblyPath);
        }
        catch (Exception loadContextEx)
        {
            _logger.LogWarning(
                "Failed to initialize plugin load context for {AssemblyPath}: {Error}",
                assemblyPath,
                loadContextEx.Message
            );

            await _eventBus.PublishAsync(
                new PluginErrorOccurredEvent
                {
                    PluginId = Guid.Empty.ToString(),
                    PluginName = Path.GetFileNameWithoutExtension(assemblyPath),
                    ErrorMessage =
                        $"Failed to initialize plugin load context: {loadContextEx.Message}",
                    ExceptionType = loadContextEx.GetType().Name,
                },
                ct
            );
            return;
        }

        try
        {
            Assembly assembly = loadContext.LoadFromAssemblyPath(absoluteAssemblyPath);
            List<Type> pluginTypes = assembly
                .GetTypes()
                .Where(t =>
                    typeof(IPlugin).IsAssignableFrom(t)
                    && t is { IsAbstract: false, IsInterface: false }
                )
                .ToList();

            foreach (Type pluginType in pluginTypes)
            {
                // Isolate each plugin type: a single malfunctioning plugin —
                // including one whose constructor or Id/Name/etc. getters throw —
                // must never abort loading of the other plugin types discovered
                // in this assembly.
                IPlugin? instance = null;
                try
                {
                    instance = PluginInstanceFactory.Create(_serviceProvider, pluginType);
                    if (instance is null)
                    {
                        continue;
                    }

                    string dataFolder = Path.Combine(
                        _pluginsPath,
                        "data",
                        instance.Id.ToString("N")
                    );
                    if (!_storage.Exists(dataFolder))
                    {
                        _storage.CreateDirectory(dataFolder);
                    }

                    PluginContext context = new(
                        _eventBus,
                        _serviceProvider,
                        _logger,
                        dataFolder,
                        _storage
                    );

                    instance.Initialize(context);

                    PluginInfo info = new()
                    {
                        Id = instance.Id,
                        Name = instance.Name,
                        Description = instance.Description,
                        Version = instance.Version,
                        Status = PluginStatus.Active,
                        AssemblyPath = assemblyPath,
                    };

                    LoadedPlugin loaded = new(info, instance, loadContext);
                    _registry[instance.Id] = loaded;

                    await _eventBus.PublishAsync(
                        new PluginLoadedEvent
                        {
                            PluginId = instance.Id.ToString(),
                            PluginName = instance.Name,
                            Version = instance.Version.ToString(),
                        },
                        ct
                    );
                }
                catch (Exception ex)
                {
                    // The failure may itself be a throwing property getter, so
                    // read the plugin's identity defensively — building the error
                    // record must never re-enter a faulty getter and throw again.
                    SafePluginIdentity identity = SafePluginIdentity.Read(
                        instance,
                        pluginType
                    );

                    _logger.LogError(
                        ex,
                        "Plugin {PluginName} in assembly {AssemblyPath} failed to load and was marked malfunctioned: {Error}",
                        identity.Name,
                        assemblyPath,
                        ex.Message
                    );

                    if (instance is not null)
                    {
                        try
                        {
                            instance.Dispose();
                        }
                        catch (Exception disposeEx)
                        {
                            _logger.LogWarning(
                                disposeEx,
                                "Plugin {PluginName} threw while being disposed after a load failure.",
                                identity.Name
                            );
                        }
                    }

                    PluginInfo info = new()
                    {
                        Id = identity.Id,
                        Name = identity.Name,
                        Description = identity.Description,
                        Version = identity.Version,
                        Status = PluginStatus.Malfunctioned,
                        AssemblyPath = assemblyPath,
                    };

                    LoadedPlugin loaded = new(info, null, loadContext);
                    if (identity.Id != Guid.Empty)
                    {
                        _registry[identity.Id] = loaded;
                    }

                    await _eventBus.PublishAsync(
                        new PluginErrorOccurredEvent
                        {
                            PluginId = identity.Id.ToString(),
                            PluginName = identity.Name,
                            ErrorMessage = ex.Message,
                            ExceptionType = ex.GetType().Name,
                        },
                        ct
                    );
                }
            }

            if (pluginTypes.Count == 0)
            {
                loadContext.Unload();
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            string errorMessage = string.Join(
                "; ",
                ex.LoaderExceptions.Where(e => e is not null).Select(e => e!.Message)
            );

            _logger.LogWarning(
                "Failed to load plugin assembly {AssemblyPath}: {Error}",
                assemblyPath,
                errorMessage
            );

            await _eventBus.PublishAsync(
                new PluginErrorOccurredEvent
                {
                    PluginId = Guid.Empty.ToString(),
                    PluginName = assemblyName,
                    ErrorMessage = errorMessage,
                    ExceptionType = nameof(ReflectionTypeLoadException),
                },
                ct
            );

            loadContext.Unload();
        }
        catch (Exception ex)
        {
            string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);

            _logger.LogWarning(
                "Failed to load plugin assembly {AssemblyPath}: {Error}",
                assemblyPath,
                ex.Message
            );

            await _eventBus.PublishAsync(
                new PluginErrorOccurredEvent
                {
                    PluginId = Guid.Empty.ToString(),
                    PluginName = assemblyName,
                    ErrorMessage = ex.Message,
                    ExceptionType = ex.GetType().Name,
                },
                ct
            );

            loadContext.Unload();
        }
    }

    public IPlugin? GetPluginInstance(Guid pluginId)
    {
        if (_registry.TryGetValue(pluginId, out LoadedPlugin? loaded))
        {
            return loaded.Instance;
        }

        return null;
    }

    public IEnumerable<T> GetPluginsOfType<T>()
        where T : IPlugin
    {
        return _registry
            .Values.Where(lp => lp.Instance is T && lp.Info.Status == PluginStatus.Active)
            .Select(lp => (T)lp.Instance!)
            .ToList();
    }

    public IEnumerable<IPluginServiceRegistrator> GetServiceRegistrators()
    {
        return _registry
            .Values.Where(lp =>
                lp.Instance is IPluginServiceRegistrator && lp.Info.Status == PluginStatus.Active
            )
            .Select(lp => (IPluginServiceRegistrator)lp.Instance!)
            .ToList();
    }

    public void Dispose()
    {
        foreach (LoadedPlugin loaded in _registry.Values)
        {
            loaded.Instance?.Dispose();
            loaded.LoadContext?.Unload();
        }

        _registry.Clear();
    }
}
