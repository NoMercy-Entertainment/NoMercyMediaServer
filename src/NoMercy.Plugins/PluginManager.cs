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
using System.Reflection;
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

        await using ZipArchive archive = await ZipFile.OpenReadAsync(fullPath, ct);

        PluginManifestEntry manifest = FindManifest(archive, fullPath);
        string pluginDir = Path.Combine(_pluginsPath, manifest.FolderName);
        string staging = Path.Combine(_pluginsPath, StagingFolder, manifest.FolderName);
        string backup = Path.Combine(_pluginsPath, RollbackFolder, manifest.FolderName);

        // Unpacked beside the installed copy, never over it. Extraction writes
        // one entry at a time, so unpacking in place left the folder holding
        // some of the new plugin and some of the old the moment anything went
        // wrong - and replacing a loaded plugin's own assembly always did. The
        // server then reports a version it is not running, and a catalogue
        // comparing versions calls it up to date and never offers it again.
        Fresh(staging);
        await ExtractAsync(archive, manifest, staging, ct);
        VerifyUnpacked(staging, manifest, "staged");

        bool wasLoaded = await _lifecycle.UnloadForUpdateAsync(manifest.Id, ct);
        bool installed = _driver.DirectoryExists(pluginDir);
        bool backedUp = false;

        try
        {
            if (installed)
            {
                // The backup is also how we find out the files are free: moving
                // a directory whose assembly is still held fails, and Unload()
                // only asks - the context goes when the GC collects it. So this
                // retries rather than trusting the unload it just did.
                backedUp = MoveWhenReleased(pluginDir, backup);

                if (!backedUp)
                {
                    throw new PluginUpdatePendingRestartException(manifest.FolderName);
                }
            }

            _driver.MoveDirectory(staging, pluginDir);

            // Checked after the move, not before: what matters is what is on
            // disk now, and a move that half-succeeded would pass a check made
            // against the source.
            VerifyUnpacked(pluginDir, manifest, "installed");

            await LoadPluginAssemblyAsync(Path.Combine(pluginDir, manifest.AssemblyFileName), ct);
            VerifyLoaded(manifest);
        }
        catch (Exception ex)
        {
            if (backedUp)
            {
                await RollBackAsync(pluginDir, backup, manifest, wasLoaded, ex, ct);
            }

            throw;
        }

        // Best-effort, and deliberately after the plugin is already loaded and
        // the update has succeeded. Windows lets a just-unloaded assembly be
        // renamed but not yet deleted - Unload() only asks, and the file goes
        // when the GC collects the context - so the backup often cannot be
        // removed this instant. Letting that throw would report a successful
        // update as a failure and send the caller into a rollback it does not
        // need. Whatever is left is cleared on the next start, where the same
        // pass also restores a backup whose update never finished.
        TryDiscard(backup);
        TryDiscard(Path.Combine(_pluginsPath, StagingFolder), onlyIfEmpty: true);
        TryDiscard(Path.Combine(_pluginsPath, RollbackFolder), onlyIfEmpty: true);
    }

    /// <summary>
    /// Where an update is unpacked and checked before it replaces anything.
    ///
    /// Inside the plugins folder rather than in temp, because the move into
    /// place has to be a move and not a copy: within one directory that is a
    /// rename, which either happens or does not, where a copy can stop halfway.
    /// The boot scan skips it by name, next to configurations and data - it
    /// holds versions that are not installed, and loading one would run
    /// something the rest of the server does not know about.
    /// </summary>
    internal const string StagingFolder = ".staging";

    /// <summary>Where the copy being replaced waits until the new one has proven itself.</summary>
    internal const string RollbackFolder = ".rollback";

    /// <summary>An empty directory, whatever was there before.</summary>
    private void Fresh(string path)
    {
        if (_driver.DirectoryExists(path))
        {
            _driver.DeleteDirectory(path, recursive: true);
        }

        _driver.CreateDirectory(path);
    }

    /// <summary>
    /// That a plugin folder holds what its manifest says it does.
    ///
    /// Run on the staged copy before anything is replaced, and again on the
    /// installed copy after the move, because those are two different claims:
    /// the first is about what arrived, the second about what is now on disk.
    /// </summary>
    private void VerifyUnpacked(string directory, PluginManifestEntry manifest, string stage)
    {
        string assembly = Path.Combine(directory, manifest.AssemblyFileName);

        if (!_driver.FileExists(assembly))
        {
            throw new PluginVerificationException(
                $"The {stage} plugin does not carry the assembly its manifest names: {manifest.AssemblyFileName}"
            );
        }

        // That it is an assembly at all, not merely a file with the right name.
        // Read rather than loaded: this opens the metadata and closes it again,
        // so a truncated download or an HTML error page saved under a .dll name
        // is refused here - while the copy that works is still installed and
        // still running - instead of being moved into place and then failing to
        // load, which is a rollback that did not need to happen.
        try
        {
            AssemblyName.GetAssemblyName(assembly);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException)
        {
            throw new PluginVerificationException(
                $"The {stage} plugin's {manifest.AssemblyFileName} is not a managed assembly."
            );
        }

        string manifestPath = Path.Combine(directory, "plugin.json");

        if (!_driver.FileExists(manifestPath))
        {
            throw new PluginVerificationException(
                $"The {stage} plugin has no plugin.json, so nothing would load it."
            );
        }

        // Parsed rather than merely present: a manifest that cannot be read is
        // a plugin the boot scan skips, and finding that out now means the old
        // one is still there to roll back to.
        PluginManifest? parsed;

        using (Stream source = _driver.OpenRead(manifestPath))
        using (StreamReader reader = new(source))
        {
            parsed = PluginManifestParser.Parse(reader.ReadToEnd());
        }

        if (parsed is null || parsed.Id != manifest.Id)
        {
            throw new PluginVerificationException(
                $"The {stage} plugin's manifest is unreadable or names a different plugin."
            );
        }
    }

    /// <summary>
    /// That the plugin the archive carried is actually running now.
    ///
    /// Read rather than assumed, because the loader does not throw when an
    /// assembly will not load: it records the plugin as Malfunctioned and
    /// carries on. That is right for a boot scan, where one bad plugin must not
    /// stop the others, and wrong here - an update that installed something the
    /// server cannot run is the case the rollback exists for, and without this
    /// it would be reported as a success and left in place.
    ///
    /// Disabled is not a failure. A plugin can install correctly and sit waiting
    /// for the owner to approve what it asks for.
    /// </summary>
    private void VerifyLoaded(PluginManifestEntry manifest)
    {
        // Nothing registered is not judged here. An assembly that carries no
        // IPlugin type registers nothing and is not an error the loader raises
        // either - the boot scan passes over it the same way - so failing an
        // update on it would be this path inventing a rule the rest of the
        // platform does not have.
        if (!_registry.TryGetValue(manifest.Id, out LoadedPlugin? loaded))
        {
            return;
        }

        if (loaded.Info.Status == PluginStatus.Malfunctioned)
        {
            throw new PluginVerificationException(
                $"The updated {manifest.FolderName} loaded as malfunctioned."
            );
        }
    }

    /// <summary>
    /// How long to keep asking for a just-unloaded assembly to be let go, and
    /// how often.
    ///
    /// <c>AssemblyLoadContext.Unload()</c> is a request: the context lives until
    /// the GC collects it, and the file stays locked until it does. A second and
    /// a half of collecting covers an ordinary unload and is short enough that
    /// an owner watching a spinner does not think it hung.
    /// </summary>
    private const int ReleaseAttempts = 15;

    private const int ReleaseDelayMs = 100;

    /// <summary>
    /// Moves a plugin folder once nothing holds its files, or gives up.
    ///
    /// The move is the test. Probing the assembly first would answer for a
    /// different moment, and the only probe that cannot truncate it is opening
    /// it exclusively - which is what the move does anyway. Returns false rather
    /// than throwing when the files stay held: that is not a fault, it is a
    /// plugin something in this process still references, and the caller turns
    /// it into an answer the owner can act on.
    /// </summary>
    private bool MoveWhenReleased(string from, string to)
    {
        string? parent = Path.GetDirectoryName(to);

        if (parent is not null && !_driver.DirectoryExists(parent))
        {
            _driver.CreateDirectory(parent);
        }

        for (int attempt = 0; attempt < ReleaseAttempts; attempt++)
        {
            try
            {
                _driver.MoveDirectory(from, to);

                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Windows reports "still resident" as UnauthorizedAccessException
                // for a directory rather than IOException, so both count.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(ReleaseDelayMs);
            }
        }

        return false;
    }

    /// <summary>
    /// Puts back the copy that was working, and loads it again if it was loaded.
    ///
    /// Every failure in here is logged and swallowed. The caller is already
    /// throwing the reason the update failed, and replacing that with whatever
    /// went wrong while cleaning up would lose it: the owner needs to know why
    /// their update did not take, not why the rollback's second step did not.
    /// </summary>
    private async Task RollBackAsync(
        string pluginDir,
        string backup,
        PluginManifestEntry manifest,
        bool wasLoaded,
        Exception cause,
        CancellationToken ct
    )
    {
        _logger.LogError(
            cause,
            "Update to {Folder} failed. Rolling back to the copy that was installed.",
            manifest.FolderName
        );

        try
        {
            if (_driver.DirectoryExists(pluginDir))
            {
                _driver.DeleteDirectory(pluginDir, recursive: true);
            }

            _driver.MoveDirectory(backup, pluginDir);

            if (wasLoaded)
            {
                await LoadPluginAssemblyAsync(
                    Path.Combine(pluginDir, manifest.AssemblyFileName),
                    ct
                );
            }

            _logger.LogInformation(
                "Rolled {Folder} back to the version that was installed before the update.",
                manifest.FolderName
            );
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex,
                "Could not roll {Folder} back. The copy that was installed is in {Backup} and has to be restored by hand.",
                manifest.FolderName,
                backup
            );
        }
    }

    /// <summary>
    /// Removes a working directory if it can, and says nothing if it cannot.
    ///
    /// Every caller is cleaning up after work that has already succeeded, so a
    /// directory that will not go yet is untidiness rather than a fault - and
    /// the boot pass clears it later, when nothing is loaded to hold it.
    /// </summary>
    private void TryDiscard(string directory, bool onlyIfEmpty = false)
    {
        try
        {
            if (!_driver.DirectoryExists(directory))
            {
                return;
            }

            if (
                onlyIfEmpty
                && _driver.EnumerateEntries(directory, "*", SearchOption.TopDirectoryOnly).Any()
            )
            {
                return;
            }

            _driver.DeleteDirectory(directory, recursive: !onlyIfEmpty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(
                "Could not remove {Directory} yet. It is cleared on the next start.",
                directory
            );
        }
    }

    /// <summary>
    /// Finishes what an interrupted update started, before anything is loaded.
    ///
    /// A backup exists for the moment between "the installed copy has been moved
    /// aside" and "the new one is in place and loaded". If the process died in
    /// that window, the backup holds the only copy of a plugin the server
    /// otherwise no longer has - so it goes back. If the plugin folder is there,
    /// the update finished and the backup is what the cleanup could not delete
    /// while the old assembly was still resident; now nothing holds it.
    /// </summary>
    private void RecoverInterruptedUpdates()
    {
        string rollback = Path.Combine(_pluginsPath, RollbackFolder);

        if (!_driver.DirectoryExists(rollback))
        {
            return;
        }

        foreach (
            StorageEntryInfo entry in _driver
                .EnumerateEntries(rollback, "*", SearchOption.TopDirectoryOnly)
                .ToList()
        )
        {
            if (!entry.IsDirectory)
            {
                continue;
            }

            string folderName = Path.GetFileName(entry.Path);
            string pluginDir = Path.Combine(_pluginsPath, folderName);

            if (_driver.DirectoryExists(pluginDir))
            {
                TryDiscard(entry.Path);
                continue;
            }

            try
            {
                _driver.MoveDirectory(entry.Path, pluginDir);

                _logger.LogWarning(
                    "An update to {Folder} did not finish. The version that was installed before it has been put back.",
                    folderName
                );
            }
            catch (Exception ex)
            {
                _logger.LogCritical(
                    ex,
                    "An update to {Folder} did not finish and its previous version could not be restored. It is in {Backup}.",
                    folderName,
                    entry.Path
                );
            }
        }

        TryDiscard(rollback, onlyIfEmpty: true);
        TryDiscard(Path.Combine(_pluginsPath, StagingFolder));
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

        // Before the scan, because this is the one moment nothing is loaded and
        // an interrupted update can still be put right.
        RecoverInterruptedUpdates();

        IReadOnlyList<StorageEntry> entries = _storage.List(_pluginsPath, null, recursive: false);
        foreach (StorageEntry entry in entries)
        {
            if (!entry.IsDirectory)
            {
                continue;
            }

            string pluginDir = entry.Path;
            string dirName = Path.GetFileName(pluginDir);
            if (dirName is "configurations" or "data" or StagingFolder or RollbackFolder)
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
