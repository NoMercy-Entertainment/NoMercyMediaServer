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
using System.Reflection;
using Microsoft.Extensions.Logging;
using NoMercy.Events;
using NoMercy.Events.Plugins;
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Capabilities;
using NoMercy.Plugins.Verification;
using NoMercy.Storage;

namespace NoMercy.Plugins;

/// <summary>
/// Loads plugin assemblies into their own <see cref="PluginLoadContext"/>, creates
/// the plugin instances through DI, and records the result in the
/// <see cref="IPluginRegistry"/>. Isolating this from <see cref="PluginManager"/>
/// keeps assembly-loading and AssemblyLoadContext lifecycle in one place.
/// </summary>
internal sealed class PluginLoader(
    IEventBus eventBus,
    IServiceProvider serviceProvider,
    ILogger logger,
    string pluginsPath,
    IStorage storage,
    IPluginRegistry registry,
    IPluginVerifier verifier,
    IPluginConsentService consentService
)
{
    private readonly IEventBus _eventBus = eventBus;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger _logger = logger;
    private readonly string _pluginsPath = pluginsPath;
    private readonly IStorage _storage = storage;
    private readonly IPluginRegistry _registry = registry;
    private readonly IPluginVerifier _verifier = verifier;
    private readonly IPluginConsentService _consentService = consentService;

    internal async Task LoadPluginFromManifestAsync(
        string manifestPath,
        CancellationToken ct = default
    )
    {
        string pluginDir = Path.GetDirectoryName(path: manifestPath)!;

        try
        {
            PluginManifest manifest = await PluginManifestParser.ParseFileAsync(
                filePath: manifestPath,
                storage: _storage,
                ct: ct
            );
            string assemblyPath = Path.Combine(path1: pluginDir, path2: manifest.Assembly);

            if (!_storage.Exists(path: assemblyPath))
            {
                _logger.LogWarning(
                    message: "Plugin manifest {ManifestPath} references assembly '{Assembly}' which was not found.", args: [manifestPath, manifest.Assembly]
                );

                await _eventBus.PublishAsync(
                    @event: new PluginErrorOccurredEvent
                    {
                        PluginId = manifest.Id.ToString(),
                        PluginName = manifest.Name,
                        ErrorMessage =
                            $"Assembly '{manifest.Assembly}' not found in plugin directory.",
                        ExceptionType = nameof(FileNotFoundException),
                    },
                    ct: ct
                );

                return;
            }

            string absoluteAssemblyPath = ToLocalAssemblyPath(storagePath: assemblyPath);

            // Manual drops carry no repository checksum; only ABI is enforced here.
            PluginVerificationResult verification = _verifier.Verify(
                manifest: manifest,
                assemblyPath: absoluteAssemblyPath,
                expectedChecksum: null
            );

            if (!verification.Verified)
            {
                string failureMessage = string.Join(separator: "; ", values: verification.Failures);

                _logger.LogWarning(
                    message: "Plugin {PluginName} failed verification and was marked malfunctioned: {Failures}", args: [manifest.Name, failureMessage]
                );

                PluginInfo malfunctionedInfo = PluginManifestParser.ToPluginInfo(
                    manifest: manifest,
                    assemblyPath: assemblyPath,
                    status: PluginStatus.Malfunctioned,
                    manifestPath: manifestPath,
                    verified: verification.Verified,
                    trusted: verification.Trusted
                );

                _registry[id: manifest.Id] = new(info: malfunctionedInfo, instance: null, loadContext: null);

                await _eventBus.PublishAsync(
                    @event: new PluginErrorOccurredEvent
                    {
                        PluginId = manifest.Id.ToString(),
                        PluginName = manifest.Name,
                        ErrorMessage = failureMessage,
                        ExceptionType = nameof(PluginVerificationException),
                    },
                    ct: ct
                );

                return;
            }

            PluginLoadContext loadContext = new(pluginPath: absoluteAssemblyPath);

            try
            {
                Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath: absoluteAssemblyPath);
                List<Type> pluginTypes = assembly
                    .GetTypes()
                    .Where(predicate: t =>
                        typeof(IPlugin).IsAssignableFrom(c: t)
                        && t is { IsAbstract: false, IsInterface: false }
                    )
                    .ToList();

                bool foundPlugin = false;

                foreach (Type pluginType in pluginTypes)
                {
                    IPlugin? instance = PluginInstanceFactory.Create(services: _serviceProvider, pluginType: pluginType);
                    if (instance is null)
                    {
                        continue;
                    }

                    // An elevated plugin (declares network/rest/ws/auth capabilities)
                    // must not silently start reaching the network or claims pipeline
                    // on first install — it loads but stays Disabled until the owner
                    // grants consent from the dashboard (Phase 2).
                    bool mayAutoEnable =
                        manifest.AutoEnabled
                        && (
                            _consentService.IsBaseline(capabilities: manifest.Capabilities)
                            || _consentService.HasConsent(pluginId: manifest.Id)
                        );

                    PluginStatus initialStatus = mayAutoEnable
                        ? PluginStatus.Active
                        : PluginStatus.Disabled;

                    if (mayAutoEnable)
                    {
                        string dataFolder = Path.Combine(
                            path1: _pluginsPath,
                            path2: "data",
                            path3: instance.Id.ToString(format: "N")
                        );
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
                            capabilities: manifest.Capabilities
                        );

                        try
                        {
                            instance.Initialize(context: context);
                        }
                        catch (Exception ex)
                        {
                            initialStatus = PluginStatus.Malfunctioned;
                            instance.Dispose();

                            PluginInfo errorInfo = PluginManifestParser.ToPluginInfo(
                                manifest: manifest,
                                assemblyPath: assemblyPath,
                                status: initialStatus,
                                manifestPath: manifestPath,
                                verified: verification.Verified,
                                trusted: verification.Trusted
                            );

                            LoadedPlugin errorLoaded = new(info: errorInfo, instance: null, loadContext: loadContext);
                            _registry[id: manifest.Id] = errorLoaded;
                            foundPlugin = true;

                            await _eventBus.PublishAsync(
                                @event: new PluginErrorOccurredEvent
                                {
                                    PluginId = manifest.Id.ToString(),
                                    PluginName = manifest.Name,
                                    ErrorMessage = ex.Message,
                                    ExceptionType = ex.GetType().Name,
                                },
                                ct: ct
                            );

                            continue;
                        }
                    }

                    PluginInfo info = PluginManifestParser.ToPluginInfo(
                        manifest: manifest,
                        assemblyPath: assemblyPath,
                        status: initialStatus,
                        manifestPath: manifestPath,
                        verified: verification.Verified,
                        trusted: verification.Trusted
                    );

                    IPlugin? storedInstance =
                        initialStatus == PluginStatus.Active ? instance : null;

                    // Reaching here with no stored instance means the plugin
                    // was never auto-enabled — Initialize() was never called
                    // and `instance` is about to be discarded unreferenced.
                    // Dispose it now instead of leaking whatever its
                    // constructor allocated (the Malfunctioned/auto-enabled
                    // failure path above already disposes its own instance).
                    if (storedInstance is null)
                    {
                        instance.Dispose();
                    }

                    LoadedPlugin loaded = new(info: info, instance: storedInstance, loadContext: loadContext);
                    _registry[id: manifest.Id] = loaded;
                    foundPlugin = true;

                    if (initialStatus == PluginStatus.Active)
                    {
                        await _eventBus.PublishAsync(
                            @event: new PluginLoadedEvent
                            {
                                PluginId = manifest.Id.ToString(),
                                PluginName = manifest.Name,
                                Version = manifest.Version,
                            },
                            ct: ct
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
                    separator: "; ",
                    values: ex.LoaderExceptions.Where(predicate: e => e is not null).Select(selector: e => e!.Message)
                );

                _logger.LogWarning(
                    message: "Failed to load plugin assembly {AssemblyPath}: {Error}", args: [assemblyPath, errorMessage]
                );

                await _eventBus.PublishAsync(
                    @event: new PluginErrorOccurredEvent
                    {
                        PluginId = manifest.Id.ToString(),
                        PluginName = manifest.Name,
                        ErrorMessage = errorMessage,
                        ExceptionType = nameof(ReflectionTypeLoadException),
                    },
                    ct: ct
                );

                loadContext.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    message: "Failed to load plugin assembly {AssemblyPath}: {Error}", args: [assemblyPath, ex.Message]
                );

                await _eventBus.PublishAsync(
                    @event: new PluginErrorOccurredEvent
                    {
                        PluginId = manifest.Id.ToString(),
                        PluginName = manifest.Name,
                        ErrorMessage = ex.Message,
                        ExceptionType = ex.GetType().Name,
                    },
                    ct: ct
                );

                loadContext.Unload();
            }
        }
        catch (Exception ex)
        {
            string pluginName = Path.GetFileName(path: pluginDir);

            _logger.LogWarning(
                message: "Failed to parse plugin manifest {ManifestPath}: {Error}", args: [manifestPath, ex.Message]
            );

            await _eventBus.PublishAsync(
                @event: new PluginErrorOccurredEvent
                {
                    PluginId = Guid.Empty.ToString(),
                    PluginName = pluginName,
                    ErrorMessage = $"Invalid plugin manifest: {ex.Message}",
                    ExceptionType = ex.GetType().Name,
                },
                ct: ct
            );
        }
    }

    internal async Task LoadPluginAssemblyAsync(string assemblyPath, CancellationToken ct = default)
    {
        string absoluteAssemblyPath = ToLocalAssemblyPath(storagePath: assemblyPath);
        PluginLoadContext loadContext;
        try
        {
            // AssemblyDependencyResolver reads the assembly's .deps.json via the
            // native host. On Linux it throws for a DLL with no/invalid deps
            // manifest (a stray file, a plugin shipped without its deps);
            // Windows tolerates it. Constructing outside the try let that escape
            // and abort discovery of every other plugin — guard it so a bad
            // assembly is skipped and reported, not fatal.
            loadContext = new(pluginPath: absoluteAssemblyPath);
        }
        catch (Exception loadContextEx)
        {
            _logger.LogWarning(
                message: "Failed to initialize plugin load context for {AssemblyPath}: {Error}", args: [assemblyPath, loadContextEx.Message]
            );

            await _eventBus.PublishAsync(
                @event: new PluginErrorOccurredEvent
                {
                    PluginId = Guid.Empty.ToString(),
                    PluginName = Path.GetFileNameWithoutExtension(path: assemblyPath),
                    ErrorMessage =
                        $"Failed to initialize plugin load context: {loadContextEx.Message}",
                    ExceptionType = loadContextEx.GetType().Name,
                },
                ct: ct
            );
            return;
        }

        try
        {
            Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath: absoluteAssemblyPath);
            List<Type> pluginTypes = assembly
                .GetTypes()
                .Where(predicate: t =>
                    typeof(IPlugin).IsAssignableFrom(c: t)
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
                    instance = PluginInstanceFactory.Create(services: _serviceProvider, pluginType: pluginType);
                    if (instance is null)
                    {
                        continue;
                    }

                    string dataFolder = Path.Combine(
                        path1: _pluginsPath,
                        path2: "data",
                        path3: instance.Id.ToString(format: "N")
                    );
                    if (!_storage.Exists(path: dataFolder))
                    {
                        _storage.CreateDirectory(path: dataFolder);
                    }

                    PluginContext context = new(
                        eventBus: _eventBus,
                        services: _serviceProvider,
                        logger: _logger,
                        dataFolderPath: dataFolder,
                        storage: _storage
                    );

                    instance.Initialize(context: context);

                    PluginInfo info = new()
                    {
                        Id = instance.Id,
                        Name = instance.Name,
                        Description = instance.Description,
                        Version = instance.Version,
                        Status = PluginStatus.Active,
                        AssemblyPath = assemblyPath,
                    };

                    LoadedPlugin loaded = new(info: info, instance: instance, loadContext: loadContext);
                    _registry[id: instance.Id] = loaded;

                    await _eventBus.PublishAsync(
                        @event: new PluginLoadedEvent
                        {
                            PluginId = instance.Id.ToString(),
                            PluginName = instance.Name,
                            Version = instance.Version.ToString(),
                        },
                        ct: ct
                    );
                }
                catch (Exception ex)
                {
                    // The failure may itself be a throwing property getter, so
                    // read the plugin's identity defensively — building the error
                    // record must never re-enter a faulty getter and throw again.
                    SafePluginIdentity identity = SafePluginIdentity.Read(instance: instance, pluginType: pluginType);

                    _logger.LogError(
                        exception: ex,
                        message: "Plugin {PluginName} in assembly {AssemblyPath} failed to load and was marked malfunctioned: {Error}", args: [identity.Name, assemblyPath, ex.Message]
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
                                exception: disposeEx,
                                message: "Plugin {PluginName} threw while being disposed after a load failure.",
                                args: identity.Name
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

                    LoadedPlugin loaded = new(info: info, instance: null, loadContext: loadContext);
                    if (identity.Id != Guid.Empty)
                    {
                        _registry[id: identity.Id] = loaded;
                    }

                    await _eventBus.PublishAsync(
                        @event: new PluginErrorOccurredEvent
                        {
                            PluginId = identity.Id.ToString(),
                            PluginName = identity.Name,
                            ErrorMessage = ex.Message,
                            ExceptionType = ex.GetType().Name,
                        },
                        ct: ct
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
            string assemblyName = Path.GetFileNameWithoutExtension(path: assemblyPath);
            string errorMessage = string.Join(
                separator: "; ",
                values: ex.LoaderExceptions.Where(predicate: e => e is not null).Select(selector: e => e!.Message)
            );

            _logger.LogWarning(
                message: "Failed to load plugin assembly {AssemblyPath}: {Error}", args: [assemblyPath, errorMessage]
            );

            await _eventBus.PublishAsync(
                @event: new PluginErrorOccurredEvent
                {
                    PluginId = Guid.Empty.ToString(),
                    PluginName = assemblyName,
                    ErrorMessage = errorMessage,
                    ExceptionType = nameof(ReflectionTypeLoadException),
                },
                ct: ct
            );

            loadContext.Unload();
        }
        catch (Exception ex)
        {
            string assemblyName = Path.GetFileNameWithoutExtension(path: assemblyPath);

            _logger.LogWarning(
                message: "Failed to load plugin assembly {AssemblyPath}: {Error}", args: [assemblyPath, ex.Message]
            );

            await _eventBus.PublishAsync(
                @event: new PluginErrorOccurredEvent
                {
                    PluginId = Guid.Empty.ToString(),
                    PluginName = assemblyName,
                    ErrorMessage = ex.Message,
                    ExceptionType = ex.GetType().Name,
                },
                ct: ct
            );

            loadContext.Unload();
        }
    }

    // AssemblyLoadContext and AssemblyDependencyResolver are raw filesystem APIs and need a
    // real absolute path. IStorage hands back paths relative to the plugins root, so resolve
    // them against the absolute local root before loading. An already-absolute path is returned
    // unchanged. Plugin assemblies are always local — they cannot be loaded from a remote driver.
    private string ToLocalAssemblyPath(string storagePath)
    {
        return Path.GetFullPath(path: Path.Combine(path1: _pluginsPath, path2: storagePath));
    }
}
