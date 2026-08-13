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
using System.Text.Json;
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

    // Held because an install has to know whether the copy it would replace is
    // still resident, and the answer decides whether the update lands now or on
    // the next start.
    private readonly IPluginAssemblyTracker? _assemblyTracker;

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
        Action<Ulid>? releaseScheduledWork = null
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
        _assemblyTracker = assemblyTracker;

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
                Id = Ulid.Empty,
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
        string staging = Path.Combine(_pluginsPath, PendingUpdatesFolder, manifest.FolderName);

        // Unpacked beside the installed copy, never over it. Extraction writes
        // one entry at a time, so anything that fails part way through used to
        // leave the folder holding some of the new plugin and some of the old.
        // That state is worse than a refusal: the manifest would name one
        // version while the assembly actually running is another, and a
        // catalogue comparing versions would call it up to date and never offer
        // the update again.
        if (_driver.DirectoryExists(staging))
        {
            _driver.DeleteDirectory(staging, recursive: true);
        }

        _driver.CreateDirectory(staging);

        await ExtractAsync(archive, manifest, staging, ct);

        // Replacing a loaded plugin's own assembly cannot work: unloading a
        // collectible context is best-effort, one live reference anywhere keeps
        // it, and on Windows the file stays locked for as long as it does. So
        // the staged copy is left where the next start finds it, before a single
        // assembly is loaded and while nothing holds the file.
        if (IsResident(manifest.Id))
        {
            _logger.LogInformation(
                "Plugin update for {Folder} is staged: its assembly is still loaded, so it is applied on the next start.",
                manifest.FolderName
            );

            throw new PluginUpdatePendingRestartException(manifest.FolderName);
        }

        ApplyStaged(staging, pluginDir);

        await LoadPluginAssemblyAsync(Path.Combine(pluginDir, manifest.AssemblyFileName), ct);
    }

    /// <summary>
    /// Where an update waits when the copy it replaces is still loaded.
    ///
    /// Inside the plugins folder rather than in temp, because a pending update
    /// has to survive the shutdown it is waiting for, and temp does not have to.
    /// The boot scan skips it by name for the same reason it skips
    /// <c>configurations</c> and <c>data</c>: it holds plugins that are not
    /// installed yet, and loading one from here would run a version the rest of
    /// the server does not know about.
    /// </summary>
    internal const string PendingUpdatesFolder = ".pending-updates";

    /// <summary>
    /// Whether this plugin's assembly is loaded in this process right now.
    ///
    /// Two questions, because they fail the same way and neither alone covers
    /// it: the registry answers for a plugin that is loaded, and the tracker
    /// answers for one that has been unloaded and whose files are still held —
    /// which is the case a best-effort unload leaves behind.
    /// </summary>
    private bool IsResident(Ulid pluginId) =>
        _registry.TryGetValue(pluginId, out _) || (_assemblyTracker?.IsStillLoaded(pluginId) ?? false);

    /// <summary>
    /// Moves a staged plugin into place, replacing what is there.
    ///
    /// Only ever called once the assembly is known to be replaceable, so this
    /// is the point where the update becomes visible and there is nothing to
    /// roll back.
    /// </summary>
    private void ApplyStaged(string staging, string pluginDir)
    {
        if (!_driver.DirectoryExists(pluginDir))
        {
            _driver.CreateDirectory(pluginDir);
        }

        // Through the driver rather than IStorage: a StorageEntry's path is
        // relative to the storage scope, and the two roots here are real paths.
        // Mixing the two produced a destination full of `..` segments that the
        // path guard refused - correctly.
        foreach (
            StorageEntryInfo info in _driver.EnumerateEntries(
                staging,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            if (info.IsDirectory)
            {
                continue;
            }

            string relative = Path.GetRelativePath(staging, info.Path);
            string destination = Path.Combine(pluginDir, relative);
            string? parent = Path.GetDirectoryName(destination);

            if (parent is not null && !_driver.DirectoryExists(parent))
            {
                _driver.CreateDirectory(parent);
            }

            using (Stream source = _driver.OpenRead(info.Path))
            using (Stream target = _driver.OpenWrite(destination, overwrite: true))
            {
                source.CopyTo(target);
            }
        }

        _driver.DeleteDirectory(staging, recursive: true);

        // And the folder they wait in, once the last one has gone. An empty
        // .pending-updates sitting in the plugins directory reads like something
        // is still queued when nothing is.
        string pending = Path.Combine(_pluginsPath, PendingUpdatesFolder);

        if (
            _driver.DirectoryExists(pending)
            && !_driver.EnumerateEntries(pending, "*", SearchOption.TopDirectoryOnly).Any()
        )
        {
            _driver.DeleteDirectory(pending, recursive: false);
        }
    }

    /// <summary>
    /// Applies every update that was waiting on this restart.
    ///
    /// Before anything is loaded, which is the whole point: this is the one
    /// moment in the process's life when no plugin assembly is held and the
    /// files can be replaced. A single failure is logged and skipped rather
    /// than thrown, because one plugin that cannot be updated must not stop the
    /// server from starting the others.
    /// </summary>
    private void ApplyPendingUpdates()
    {
        string pending = Path.Combine(_pluginsPath, PendingUpdatesFolder);

        if (!_driver.DirectoryExists(pending))
        {
            return;
        }

        foreach (
            StorageEntryInfo entry in _driver
                .EnumerateEntries(pending, "*", SearchOption.TopDirectoryOnly)
                .ToList()
        )
        {
            if (!entry.IsDirectory)
            {
                continue;
            }

            string folderName = Path.GetFileName(entry.Path);

            try
            {
                ApplyStaged(entry.Path, Path.Combine(_pluginsPath, folderName));

                _logger.LogInformation(
                    "Applied the staged update for {Folder}.",
                    folderName
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Could not apply the staged update for {Folder}. The installed version is untouched and the update stays staged.",
                    folderName
                );
            }
        }
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

        return new(
            prefix,
            parsed.Assembly,
            Path.GetFileNameWithoutExtension(parsed.Assembly),
            parsed.Id
        );
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
        string FolderName,
        Ulid Id
    );

    public Task EnablePluginAsync(Ulid pluginId, CancellationToken ct = default)
    {
        return _lifecycle.EnablePluginAsync(pluginId, ct);
    }

    public Task DisablePluginAsync(Ulid pluginId, CancellationToken ct = default)
    {
        return _lifecycle.DisablePluginAsync(pluginId, ct);
    }

    public Task UninstallPluginAsync(Ulid pluginId, CancellationToken ct = default)
    {
        return _lifecycle.UninstallPluginAsync(pluginId, ct);
    }

    public async Task LoadPluginsFromDirectoryAsync(CancellationToken ct = default)
    {
        if (!_storage.Exists(_pluginsPath))
        {
            return;
        }

        // Before the scan below, because this is the one moment no plugin
        // assembly is held and a staged update can replace the files it needs to.
        ApplyPendingUpdates();

        IReadOnlyList<StorageEntry> entries = _storage.List(_pluginsPath, null, recursive: false);
        foreach (StorageEntry entry in entries)
        {
            if (!entry.IsDirectory)
            {
                continue;
            }

            string pluginDir = entry.Path;
            string dirName = Path.GetFileName(pluginDir);
            if (dirName is "configurations" or "data" or PendingUpdatesFolder)
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

    public IPlugin? GetPluginInstance(Ulid pluginId)
    {
        if (_registry.TryGetValue(pluginId, out LoadedPlugin? loaded))
        {
            return loaded.Instance;
        }

        return null;
    }

    public PluginInfo? GetPluginInfo(Ulid pluginId) =>
        _registry.TryGetValue(pluginId, out LoadedPlugin? loaded) ? loaded.Info : null;

    public async Task<Dictionary<string, string>?> ReadTranslationsAsync(
        Ulid pluginId,
        string locale,
        CancellationToken ct
    )
    {
        if (!_registry.TryGetValue(pluginId, out LoadedPlugin? loaded))
            return null;

        string? manifestPath = loaded.Info.ManifestPath;
        if (string.IsNullOrWhiteSpace(manifestPath) || !_storage.Exists(manifestPath))
            return null;

        PluginTranslations? declared;
        try
        {
            declared = JsonSerializer
                .Deserialize<PluginManifest>(
                    await _storage.ReadAllTextAsync(manifestPath, ct),
                    TranslationJson
                )
                ?.Translations;
        }
        catch (JsonException)
        {
            return null;
        }

        if (declared is null)
            return null;

        string? root = Path.GetDirectoryName(manifestPath);
        if (root is null)
            return null;

        // Falls back to the locale the plugin was authored in. A viewer whose
        // language a plugin does not ship should read it in the language it was
        // written in, never in empty labels.
        string wanted = declared.Locales.Contains(locale) ? locale : declared.Source;

        return await ReadLocaleFileAsync(Path.Combine(root, declared.Path), wanted, ct);
    }

    private static readonly JsonSerializerOptions TranslationJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private async Task<Dictionary<string, string>?> ReadLocaleFileAsync(
        string directory,
        string locale,
        CancellationToken ct
    )
    {
        string path = Path.Combine(directory, $"{locale}.json");

        if (!_storage.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(
                await _storage.ReadAllTextAsync(path, ct)
            );
        }
        catch (JsonException)
        {
            return null;
        }
    }

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
