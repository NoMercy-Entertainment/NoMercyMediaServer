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

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Events;
using NoMercy.Events.Plugins;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Capabilities;
using Xunit;

namespace NoMercy.Tests.Plugins;

/// <summary>
/// Manifest-path scenarios that PluginBootScanTests/PluginLoaderDisposalTests/
/// PluginManagerVerificationTests do not reach: the auto-enable Initialize()
/// failure branch, the elevated-capability-plus-pre-granted-consent branch of
/// mayAutoEnable, and a manifest whose referenced assembly contains zero
/// IPlugin types.
/// </summary>
public class PluginLoaderManifestPathTests : IDisposable
{
    private static readonly Guid ManifestFailurePluginId = Guid.Parse(
        input: "77777777-8888-9999-aaaa-bbbbbbbbbbbb"
    );

    private readonly string _tempPluginsDir;
    private readonly InMemoryEventBus _eventBus;

    public PluginLoaderManifestPathTests()
    {
        _tempPluginsDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-manifest-path-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _tempPluginsDir);
        _eventBus = new();
    }

    public void Dispose()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        try
        {
            if (Directory.Exists(path: _tempPluginsDir))
                Directory.Delete(path: _tempPluginsDir, recursive: true);
        }
        catch (Exception) { }
    }

    private PluginManager BuildManager(IPluginConsentService? consentService = null) =>
        new(
            eventBus: _eventBus,
            serviceProvider: new MinimalServiceProvider(),
            logger: NullLogger<PluginManager>.Instance,
            pluginsPath: _tempPluginsDir,
            storage: TestStorageHelper.CreateStorage(rootPath: _tempPluginsDir),
            driver: TestStorageHelper.CreateBackend(),
            consentService: consentService
        );

    private static string GetManifestFailureBinDir()
    {
        string testBinDir = Path.GetDirectoryName(
            path: typeof(PluginLoaderManifestPathTests).Assembly.Location
        )!;
        string configDir = Path.GetDirectoryName(path: testBinDir)!;
        string buildConfig = Path.GetFileName(path: configDir);
        string repoRoot = Path.GetFullPath(path: Path.Combine(paths: [testBinDir, "..", "..", "..", "..", ".."]));

        return Path.Combine(paths: [repoRoot, "tests", "NoMercy.Plugin.Samples.ManifestFailure", "bin", buildConfig, "net10.0"]
        );
    }

    private string StageManifestFailurePlugin(string manifestJson)
    {
        string binDir = GetManifestFailureBinDir();
        string dllSrc = Path.Combine(path1: binDir, path2: "NoMercy.Plugin.Samples.ManifestFailure.dll");

        if (!File.Exists(path: dllSrc))
            throw new FileNotFoundException(
                message: $"ManifestFailure plugin DLL not found at '{dllSrc}'. Build NoMercy.Plugin.Samples.ManifestFailure first."
            );

        string pluginDir = Path.Combine(path1: _tempPluginsDir, path2: "ManifestFailure");
        Directory.CreateDirectory(path: pluginDir);

        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.dll"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: pluginDir, path2: Path.GetFileName(path: file)), overwrite: true);
        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.deps.json"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: pluginDir, path2: Path.GetFileName(path: file)), overwrite: true);

        string manifestPath = Path.Combine(path1: pluginDir, path2: "plugin.json");
        File.WriteAllText(path: manifestPath, contents: manifestJson);
        return manifestPath;
    }

    [Fact]
    public async Task LoadPluginFromManifestAsync_AutoEnabledInitializeThrows_MarksMalfunctionedAndPublishesError()
    {
        string manifestJson = """
            {
              "id": "77777777-8888-9999-aaaa-bbbbbbbbbbbb",
              "name": "ManifestFailure",
              "version": "0.1.0",
              "description": "manifest auto-enable initialize failure",
              "assembly": "NoMercy.Plugin.Samples.ManifestFailure.dll",
              "autoEnabled": true
            }
            """;
        string manifestPath = StageManifestFailurePlugin(manifestJson: manifestJson);
        PluginManager manager = BuildManager();
        List<PluginErrorOccurredEvent> errors = [];
        _eventBus.Subscribe<PluginErrorOccurredEvent>(
            handler: (evt, _) =>
            {
                errors.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await manager.LoadPluginFromManifestAsync(manifestPath: manifestPath);

        PluginInfo? info = manager
            .GetInstalledPlugins()
            .FirstOrDefault(predicate: p => p.Id == ManifestFailurePluginId);
        info.Should().NotBeNull();
        info!.Status.Should().Be(expected: PluginStatus.Malfunctioned);
        manager
            .GetPluginInstance(pluginId: ManifestFailurePluginId)
            .Should()
            .BeNull(because: "the failed instance was disposed and never stored");
        errors.Should().ContainSingle(predicate: e => e.PluginId == ManifestFailurePluginId.ToString());

        manager.Dispose();
    }

    [Fact]
    public async Task LoadPluginFromManifestAsync_ElevatedCapabilitiesWithPreGrantedConsent_StillAutoEnables()
    {
        // mayAutoEnable = manifest.AutoEnabled && (IsBaseline || HasConsent).
        // "rest": true makes IsBaseline false — the ONLY way to reach Active
        // here is through the HasConsent(manifest.Id) side of the OR, which
        // requires a consent service that already has this exact id granted
        // BEFORE the load runs.
        string manifestJson = """
            {
              "id": "77777777-8888-9999-aaaa-bbbbbbbbbbbb",
              "name": "ManifestFailure",
              "version": "0.1.0",
              "description": "elevated capability with pre-granted consent",
              "assembly": "NoMercy.Plugin.Samples.ManifestFailure.dll",
              "autoEnabled": true,
              "capabilities": { "rest": true }
            }
            """;
        string manifestPath = StageManifestFailurePlugin(manifestJson: manifestJson);
        InMemoryConsentStore consentStore = new();
        consentStore.Add(pluginId: ManifestFailurePluginId);
        PluginConsentService consentService = new(store: consentStore);
        PluginManager manager = BuildManager(consentService: consentService);

        await manager.LoadPluginFromManifestAsync(manifestPath: manifestPath);

        // ManifestAutoEnableInitializeThrowsPlugin always throws in Initialize(),
        // so mayAutoEnable=true still ends in Malfunctioned — but reaching that
        // Initialize() call AT ALL only happens when mayAutoEnable evaluated
        // true, which is exactly the branch this test targets.
        PluginInfo? info = manager
            .GetInstalledPlugins()
            .FirstOrDefault(predicate: p => p.Id == ManifestFailurePluginId);
        info.Should().NotBeNull();
        info!.Status.Should().Be(expected: PluginStatus.Malfunctioned);

        manager.Dispose();
    }

    [Fact]
    public async Task LoadPluginFromManifestAsync_ElevatedCapabilitiesWithoutConsent_StaysDisabled()
    {
        string manifestJson = """
            {
              "id": "77777777-8888-9999-aaaa-bbbbbbbbbbbb",
              "name": "ManifestFailure",
              "version": "0.1.0",
              "description": "elevated capability without consent",
              "assembly": "NoMercy.Plugin.Samples.ManifestFailure.dll",
              "autoEnabled": true,
              "capabilities": { "rest": true }
            }
            """;
        string manifestPath = StageManifestFailurePlugin(manifestJson: manifestJson);
        PluginManager manager = BuildManager(consentService: new PluginConsentService(store: new InMemoryConsentStore()));

        await manager.LoadPluginFromManifestAsync(manifestPath: manifestPath);

        // Never auto-enabled, so Initialize() (which always throws) is never
        // even called — the plugin loads Disabled, not Malfunctioned.
        PluginInfo? info = manager
            .GetInstalledPlugins()
            .FirstOrDefault(predicate: p => p.Id == ManifestFailurePluginId);
        info.Should().NotBeNull();
        info!.Status.Should().Be(expected: PluginStatus.Disabled);

        manager.Dispose();
    }

    [Fact]
    public async Task LoadPluginFromManifestAsync_MultiTypeAssembly_NeverThrowsRegardlessOfPerTypeOutcome()
    {
        // The manifest path represents ONE logical plugin per manifest.Id — a
        // multi-type assembly here is out of contract (see
        // PluginLoaderFailureFixtureTests' class doc) and this deliberately
        // does not assert which type "wins" the registry slot. Its purpose is
        // reflection-filter coverage: `assembly.GetTypes().Where(predicate)`
        // evaluates the predicate against EVERY type in the module — including
        // AbstractPluginBase (assignable to IPlugin, but abstract) — before
        // the foreach that processes pluginTypes ever runs, regardless of
        // whether that foreach later aborts on a throwing type.
        string binDir = GetFailuresPluginBinDir();
        string dllSrc = Path.Combine(path1: binDir, path2: "NoMercy.Plugin.Samples.Failures.dll");
        if (!File.Exists(path: dllSrc))
            throw new FileNotFoundException(
                message: $"Failures plugin DLL not found at '{dllSrc}'. Build NoMercy.Plugin.Samples.Failures first."
            );

        string pluginDir = Path.Combine(path1: _tempPluginsDir, path2: "FailuresViaManifest");
        Directory.CreateDirectory(path: pluginDir);
        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.dll"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: pluginDir, path2: Path.GetFileName(path: file)), overwrite: true);
        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.deps.json"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: pluginDir, path2: Path.GetFileName(path: file)), overwrite: true);

        Guid manifestId = Guid.NewGuid();
        string manifestJson = $$"""
            {
              "id": "{{manifestId}}",
              "name": "FailuresViaManifest",
              "version": "0.1.0",
              "description": "multi-type assembly staged via manifest, out of contract by design",
              "assembly": "NoMercy.Plugin.Samples.Failures.dll",
              "autoEnabled": false
            }
            """;
        string manifestPath = Path.Combine(path1: pluginDir, path2: "plugin.json");
        File.WriteAllText(path: manifestPath, contents: manifestJson);
        PluginManager manager = BuildManager();

        Func<Task> act = () => manager.LoadPluginFromManifestAsync(manifestPath: manifestPath);

        await act.Should().NotThrowAsync();

        manager.Dispose();
    }

    private static string GetFailuresPluginBinDir()
    {
        string testBinDir = Path.GetDirectoryName(
            path: typeof(PluginLoaderManifestPathTests).Assembly.Location
        )!;
        string configDir = Path.GetDirectoryName(path: testBinDir)!;
        string buildConfig = Path.GetFileName(path: configDir);
        string repoRoot = Path.GetFullPath(path: Path.Combine(paths: [testBinDir, "..", "..", "..", "..", ".."]));

        return Path.Combine(paths: [repoRoot, "tests", "NoMercy.Plugin.Samples.Failures", "bin", buildConfig, "net10.0"]
        );
    }

    [Fact]
    public async Task LoadPluginFromManifestAsync_AssemblyHasNoPluginTypes_UnloadsContextWithoutRegisteringAnything()
    {
        // NoMercy.Plugins.Abstractions.dll is a real, validly-loadable assembly
        // with zero concrete IPlugin implementations — a manifest can
        // (incorrectly, but not fatally) point at any real assembly.
        string pluginDir = Path.Combine(path1: _tempPluginsDir, path2: "NoPluginTypes");
        Directory.CreateDirectory(path: pluginDir);
        string abstractionsSrc = typeof(IPlugin).Assembly.Location;
        string abstractionsDest = Path.Combine(path1: pluginDir, path2: "NoMercy.Plugins.Abstractions.dll");
        File.Copy(sourceFileName: abstractionsSrc, destFileName: abstractionsDest, overwrite: true);

        Guid manifestId = Guid.NewGuid();
        string manifestJson = $$"""
            {
              "id": "{{manifestId}}",
              "name": "NoPluginTypes",
              "version": "0.1.0",
              "description": "assembly with zero plugin types",
              "assembly": "NoMercy.Plugins.Abstractions.dll",
              "autoEnabled": true
            }
            """;
        string manifestPath = Path.Combine(path1: pluginDir, path2: "plugin.json");
        File.WriteAllText(path: manifestPath, contents: manifestJson);
        PluginManager manager = BuildManager();

        Func<Task> act = () => manager.LoadPluginFromManifestAsync(manifestPath: manifestPath);

        await act.Should().NotThrowAsync();
        manager.GetInstalledPlugins().Should().NotContain(predicate: p => p.Id == manifestId);

        manager.Dispose();
    }

    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
