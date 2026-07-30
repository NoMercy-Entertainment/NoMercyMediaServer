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

using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using NoMercy.Events;
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Capabilities;
using NoMercy.Plugins.Hub;
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
        IPluginConsentService? consentService = null,
        IPluginContextFactory? contextFactory = null,
        PluginHostOptions? hostOptions = null,
        IPluginAssemblyTracker? assemblyTracker = null,
        Action<Guid>? releaseScheduledWork = null
    )
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pluginsPath = pluginsPath ?? throw new ArgumentNullException(nameof(pluginsPath));
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _verifier = verifier ?? new PluginVerifier();
        _consentService =
            consentService
            ?? new PluginConsentService(
                new ConfigPluginConsentStore(
                    new PluginConfiguration(
                        Path.Combine(_pluginsPath, "data", "platform"),
                        _storage
                    )
                )
            );
        _registry = new PluginRegistry();

        // Built here only when DI did not supply one, which is the test and
        // direct-construction path. Its protector is ephemeral, so a secret
        // written through it does not survive a restart — that is the safe
        // failure, because the alternative default is a secret on disk in the
        // clear. The server's registration always passes the real factory.
        IPluginContextFactory factory =
            contextFactory
            ?? new PluginContextFactory(
                _eventBus,
                _serviceProvider,
                _storage,
                new ConfigPluginGrantStore(PlatformConfiguration()),
                new EphemeralDataProtectionProvider(),
                new NullPluginLibraryQuery(),
                new NullPluginLibraryWriterFactory(),
                PlatformConfiguration(),
                new NullPluginHubContextFactory()
            );

        _loader = new(
            _eventBus,
            _serviceProvider,
            _logger,
            _pluginsPath,
            _storage,
            _registry,
            _verifier,
            _consentService,
            factory,
            hostOptions
        );
        _lifecycle = new(
            _eventBus,
            _serviceProvider,
            _logger,
            _pluginsPath,
            _storage,
            _registry,
            _loader,
            factory,
            assemblyTracker,
            releaseScheduledWork
        );
    }

    private PluginConfiguration PlatformConfiguration() =>
        new(Path.Combine(_pluginsPath, "data", "platform"), _storage);

    public IReadOnlyList<PluginInfo> GetInstalledPlugins()
    {
        return _registry.Values.Select(lp => lp.Info).ToList().AsReadOnly();
    }

    public Task InstallPluginAsync(string packagePath, CancellationToken ct = default)
    {
        return InstallPluginAsync(packagePath, expectedChecksum: null, ct);
    }

    public async Task InstallPluginAsync(
        string packagePath,
        string? expectedChecksum,
        CancellationToken ct = default
    )
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

        if (!string.IsNullOrWhiteSpace(expectedChecksum))
        {
            // This install path receives a bare assembly with no plugin.json
            // alongside it, so ABI cannot be judged here (TargetAbi stays null
            // and that stage passes by design); only the checksum a repository
            // caller supplies is enforced, before anything is copied to disk.
            PluginManifest checksumManifest = new()
            {
                Id = Guid.Empty,
                Name = Path.GetFileNameWithoutExtension(fullPath),
                Description = string.Empty,
                Version = "0.0.0",
                Assembly = Path.GetFileName(fullPath),
            };

            PluginVerificationResult verification = _verifier.Verify(
                checksumManifest,
                fullPath,
                expectedChecksum
            );

            if (!verification.Verified)
            {
                throw new PluginVerificationException(
                    $"Plugin '{checksumManifest.Name}' failed verification: {string.Join("; ", verification.Failures)}"
                );
            }
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

    public async Task InstallPluginArchiveAsync(
        string archivePath,
        string? expectedChecksum = null,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        string fullPath = Path.GetFullPath(archivePath);

        if (!_driver.FileExists(fullPath))
        {
            throw new FileNotFoundException($"Plugin archive not found: {fullPath}", fullPath);
        }

        // Before a single byte is unpacked. An archive that fails here must never
        // have existed on disk anywhere the loader looks.
        if (!string.IsNullOrWhiteSpace(expectedChecksum))
        {
            string actual = await ComputeSha256Async(fullPath, ct);

            if (!actual.Equals(expectedChecksum.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new PluginVerificationException(
                    $"Plugin archive failed verification: expected checksum {expectedChecksum}, got {actual}"
                );
            }
        }

        using ZipArchive archive = ZipFile.OpenRead(fullPath);

        PluginManifestEntry manifest = FindManifest(archive, fullPath);
        string pluginDir = Path.Combine(_pluginsPath, manifest.FolderName);

        if (!_storage.Exists(pluginDir))
        {
            _storage.CreateDirectory(pluginDir);
        }

        string assemblyPath = await ExtractAsync(archive, manifest, pluginDir, ct);

        await LoadPluginAssemblyAsync(assemblyPath, ct);
    }

    /// <summary>
    /// The manifest decides what the archive is. Located rather than assumed:
    /// a plugin is published either as its folder or as the folder's contents,
    /// and both are the same plugin.
    /// </summary>
    private static PluginManifestEntry FindManifest(ZipArchive archive, string archivePath)
    {
        ZipArchiveEntry? entry = archive
            .Entries.Where(candidate =>
                Path.GetFileName(candidate.FullName)
                    .Equals("plugin.json", StringComparison.OrdinalIgnoreCase)
            )
            // Shallowest wins, so a plugin that ships its own docs folder
            // containing an example manifest cannot outrank the real one.
            .MinBy(candidate => candidate.FullName.Count(ArchiveSeparators.Contains));

        if (entry is null)
        {
            throw new PluginVerificationException(
                $"Plugin archive has no plugin.json: {Path.GetFileName(archivePath)}"
            );
        }

        string prefix = entry.FullName[..(entry.FullName.Length - "plugin.json".Length)];
        PluginManifest parsed;

        using (Stream stream = entry.Open())
        using (StreamReader reader = new(stream))
        {
            parsed =
                PluginManifestParser.Parse(reader.ReadToEnd())
                ?? throw new PluginVerificationException(
                    $"Plugin archive has an unreadable plugin.json: {Path.GetFileName(archivePath)}"
                );
        }

        if (string.IsNullOrWhiteSpace(parsed.Assembly))
        {
            throw new PluginVerificationException(
                "Plugin manifest does not name an assembly, so there is nothing to load."
            );
        }

        return new(prefix, parsed.Assembly, Path.GetFileNameWithoutExtension(parsed.Assembly));
    }

    private async Task<string> ExtractAsync(
        ZipArchive archive,
        PluginManifestEntry manifest,
        string pluginDir,
        CancellationToken ct
    )
    {
        string root = Path.GetFullPath(pluginDir);
        string? assemblyPath = null;

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (ArchiveSeparators.Contains(entry.FullName[^1]))
            {
                continue;
            }

            if (!entry.FullName.StartsWith(manifest.Prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relative = entry.FullName[manifest.Prefix.Length..];
            string destination = Path.GetFullPath(Path.Combine(root, relative));

            // The archive names its own entries, so an entry may name a path.
            // Resolve first and refuse anything that lands outside the plugin's
            // own folder, or a zip writes wherever it likes on this machine.
            if (
                !destination.StartsWith(
                    root + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal
                )
            )
            {
                throw new PluginVerificationException(
                    $"Plugin archive tried to write outside its folder: {entry.FullName}"
                );
            }

            string? parent = Path.GetDirectoryName(destination);
            if (parent is not null && !_storage.Exists(parent))
            {
                _storage.CreateDirectory(parent);
            }

            await using (Stream source = entry.Open())
            await using (Stream target = _driver.OpenWrite(destination, overwrite: true))
            {
                await source.CopyToAsync(target, ct);
            }

            if (
                Path.GetFileName(destination)
                    .Equals(manifest.AssemblyFileName, StringComparison.OrdinalIgnoreCase)
            )
            {
                assemblyPath = destination;
            }
        }

        return assemblyPath
            ?? throw new PluginVerificationException(
                $"Plugin archive does not contain the assembly its manifest names: {manifest.AssemblyFileName}"
            );
    }

    private async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using Stream stream = _driver.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, ct);

        return Convert.ToHexStringLower(hash);
    }

    // Zip entries name their own separator and a Windows-built archive uses the
    // other one, so both count regardless of the host this runs on.
    private static readonly char[] ArchiveSeparators = ['/', '\\'];

    private sealed record PluginManifestEntry(
        string Prefix,
        string AssemblyFileName,
        string FolderName
    );

    public Task EnablePluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        return _lifecycle.EnablePluginAsync(pluginId, ct);
    }

    public Task DisablePluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        return _lifecycle.DisablePluginAsync(pluginId, ct);
    }

    public Task UninstallPluginAsync(Guid pluginId, CancellationToken ct = default)
    {
        return _lifecycle.UninstallPluginAsync(pluginId, ct);
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

    internal Task LoadPluginFromManifestAsync(string manifestPath, CancellationToken ct = default)
    {
        return _loader.LoadPluginFromManifestAsync(manifestPath, ct);
    }

    internal Task LoadPluginAssemblyAsync(string assemblyPath, CancellationToken ct = default)
    {
        return _loader.LoadPluginAssemblyAsync(assemblyPath, ct);
    }

    public IPlugin? GetPluginInstance(Guid pluginId)
    {
        if (_registry.TryGetValue(pluginId, out LoadedPlugin? loaded))
        {
            return loaded.Instance;
        }

        return null;
    }

    public PluginInfo? GetPluginInfo(Guid pluginId) =>
        _registry.TryGetValue(pluginId, out LoadedPlugin? loaded) ? loaded.Info : null;

    public IEnumerable<T> GetPluginsOfType<T>()
        where T : IPlugin
    {
        return _registry
            .Values.Where(lp => lp is { Instance: T, Info.Status: PluginStatus.Active })
            .Select(lp => (T)lp.Instance!)
            .ToList();
    }

    public IEnumerable<IPluginServiceRegistrator> GetServiceRegistrators()
    {
        return _registry
            .Values.Where(lp =>
                lp is { Instance: IPluginServiceRegistrator, Info.Status: PluginStatus.Active }
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
