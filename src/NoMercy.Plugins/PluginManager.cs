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
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Capabilities;
using NoMercy.Plugins.Verification;
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
    private readonly IPluginVerifier _verifier;
    private readonly IPluginConsentService _consentService;
    private readonly IPluginRegistry _registry;
    private readonly PluginLoader _loader;
    private readonly PluginLifecycleManager _lifecycle;

    public PluginManager(
        IEventBus eventBus,
        IServiceProvider serviceProvider,
        ILogger<PluginManager> logger,
        string pluginsPath,
        IStorage storage,
        IStorageDriver driver,
        IPluginVerifier? verifier = null,
        IPluginConsentService? consentService = null
    )
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(paramName: nameof(eventBus));
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(paramName: nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(paramName: nameof(logger));
        _pluginsPath = pluginsPath ?? throw new ArgumentNullException(paramName: nameof(pluginsPath));
        _driver = driver ?? throw new ArgumentNullException(paramName: nameof(driver));
        _storage = storage ?? throw new ArgumentNullException(paramName: nameof(storage));
        _verifier = verifier ?? new PluginVerifier();
        _consentService =
            consentService
            ?? new PluginConsentService(
                store: new ConfigPluginConsentStore(
                    configuration: new PluginConfiguration(
                        dataFolderPath: Path.Combine(path1: _pluginsPath, path2: "data", path3: "platform"),
                        storage: _storage
                    )
                )
            );
        _registry = new PluginRegistry();
        _loader = new(
            eventBus: _eventBus,
            serviceProvider: _serviceProvider,
            logger: _logger,
            pluginsPath: _pluginsPath,
            storage: _storage,
            registry: _registry,
            verifier: _verifier,
            consentService: _consentService
        );
        _lifecycle = new(
            eventBus: _eventBus,
            serviceProvider: _serviceProvider,
            logger: _logger,
            pluginsPath: _pluginsPath,
            storage: _storage,
            registry: _registry,
            loader: _loader
        );
    }

    public IReadOnlyList<PluginInfo> GetInstalledPlugins()
    {
        return _registry.Values.Select(selector: lp => lp.Info).ToList().AsReadOnly();
    }

    public Task InstallPluginAsync(string packagePath, CancellationToken ct = default)
    {
        return InstallPluginAsync(packagePath: packagePath, expectedChecksum: null, ct: ct);
    }

    public async Task InstallPluginAsync(
        string packagePath,
        string? expectedChecksum,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: packagePath);

        string fullPath = Path.GetFullPath(path: packagePath);

        // Source assembly may be anywhere on disk (user-supplied install path).
        // Use the raw backend for the existence check and copy-in; the destination
        // is always inside the allowlisted plugin root so _storage covers it.
        if (!_driver.FileExists(path: fullPath))
        {
            throw new FileNotFoundException(message: $"Plugin assembly not found: {fullPath}", fileName: fullPath);
        }

        if (!string.IsNullOrWhiteSpace(value: expectedChecksum))
        {
            // This install path receives a bare assembly with no plugin.json
            // alongside it, so ABI cannot be judged here (TargetAbi stays null
            // and that stage passes by design); only the checksum a repository
            // caller supplies is enforced, before anything is copied to disk.
            PluginManifest checksumManifest = new()
            {
                Id = Guid.Empty,
                Name = Path.GetFileNameWithoutExtension(path: fullPath),
                Description = string.Empty,
                Version = "0.0.0",
                Assembly = Path.GetFileName(path: fullPath),
            };

            PluginVerificationResult verification = _verifier.Verify(
                manifest: checksumManifest,
                assemblyPath: fullPath,
                expectedChecksum: expectedChecksum
            );

            if (!verification.Verified)
            {
                throw new PluginVerificationException(
                    message: $"Plugin '{checksumManifest.Name}' failed verification: {string.Join(separator: "; ", values: verification.Failures)}"
                );
            }
        }

        string pluginName = Path.GetFileNameWithoutExtension(path: fullPath);
        string pluginDir = Path.Combine(path1: _pluginsPath, path2: pluginName);

        if (!_storage.Exists(path: pluginDir))
        {
            _storage.CreateDirectory(path: pluginDir);
        }

        string destPath = Path.Combine(path1: pluginDir, path2: Path.GetFileName(path: fullPath));
        _driver.CopyFile(source: fullPath, destination: destPath, overwrite: true);

        await LoadPluginAssemblyAsync(assemblyPath: destPath, ct: ct);
    }

    public Task EnablePluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        return _lifecycle.EnablePluginAsync(pluginId: pluginId, ct: ct);
    }

    public Task DisablePluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        return _lifecycle.DisablePluginAsync(pluginId: pluginId, ct: ct);
    }

    public Task UninstallPluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        return _lifecycle.UninstallPluginAsync(pluginId: pluginId, ct: ct);
    }

    public async Task LoadPluginsFromDirectoryAsync(CancellationToken ct = default)
    {
        if (!_storage.Exists(path: _pluginsPath))
        {
            return;
        }

        IReadOnlyList<StorageEntry> entries = _storage.List(path: _pluginsPath, pattern: null, recursive: false);
        foreach (StorageEntry entry in entries)
        {
            if (!entry.IsDirectory)
            {
                continue;
            }

            string pluginDir = entry.Path;
            string dirName = Path.GetFileName(path: pluginDir);
            if (dirName is "configurations" or "data")
            {
                continue;
            }

            try
            {
                string manifestPath = Path.Combine(path1: pluginDir, path2: "plugin.json");
                if (_storage.Exists(path: manifestPath))
                {
                    await LoadPluginFromManifestAsync(manifestPath: manifestPath, ct: ct);
                    continue;
                }

                IReadOnlyList<StorageEntry> dllEntries = _storage.List(
                    path: pluginDir,
                    pattern: "*.dll",
                    recursive: false
                );
                foreach (StorageEntry dllEntry in dllEntries)
                {
                    if (!dllEntry.IsDirectory)
                    {
                        await LoadPluginAssemblyAsync(assemblyPath: dllEntry.Path, ct: ct);
                    }
                }
            }
            catch (Exception ex)
            {
                // Defense in depth: the load helpers already isolate their own
                // failures, but an unexpected throw must not stop the remaining
                // plugin directories from being scanned.
                _logger.LogError(
                    exception: ex,
                    message: "Unexpected failure while loading plugin directory {PluginDir}; skipping it.",
                    args: pluginDir
                );
            }
        }
    }

    public async Task<IReadOnlyList<PluginLoadResult>> LoadAllAsync(CancellationToken ct = default)
    {
        if (!_storage.Exists(path: _pluginsPath))
        {
            _logger.LogInformation(
                message: "Plugins directory missing: {Path}. No plugins loaded.",
                args: _pluginsPath
            );
            return [];
        }

        await LoadPluginsFromDirectoryAsync(ct: ct);

        List<PluginLoadResult> results = [];
        foreach (LoadedPlugin loaded in _registry.Values)
        {
            if (loaded.Instance is not null && loaded.Info.Status == PluginStatus.Active)
            {
                results.Add(
                    item: new(
                        PluginId: loaded.Info.Id,
                        Name: loaded.Info.Name,
                        Version: loaded.Info.Version.ToString(),
                        Instance: loaded.Instance
                    )
                );
            }
        }

        return results;
    }

    internal Task LoadPluginFromManifestAsync(string manifestPath, CancellationToken ct = default)
    {
        return _loader.LoadPluginFromManifestAsync(manifestPath: manifestPath, ct: ct);
    }

    internal Task LoadPluginAssemblyAsync(string assemblyPath, CancellationToken ct = default)
    {
        return _loader.LoadPluginAssemblyAsync(assemblyPath: assemblyPath, ct: ct);
    }

    public IPlugin? GetPluginInstance(Guid pluginId)
    {
        if (_registry.TryGetValue(id: pluginId, plugin: out LoadedPlugin? loaded))
        {
            return loaded.Instance;
        }

        return null;
    }

    public IEnumerable<T> GetPluginsOfType<T>()
        where T : IPlugin
    {
        return _registry
            .Values.Where(predicate: lp => lp is { Instance: T, Info.Status: PluginStatus.Active })
            .Select(selector: lp => (T)lp.Instance!)
            .ToList();
    }

    public IEnumerable<IPluginServiceRegistrator> GetServiceRegistrators()
    {
        return _registry
            .Values.Where(predicate: lp =>
                lp is { Instance: IPluginServiceRegistrator, Info.Status: PluginStatus.Active }
            )
            .Select(selector: lp => (IPluginServiceRegistrator)lp.Instance!)
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
