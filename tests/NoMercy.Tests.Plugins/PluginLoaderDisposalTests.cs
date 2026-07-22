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
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

/// <summary>
/// Stages the Echo sample plugin with a manifest that is NOT auto-enabled and
/// verifies PluginLoader disposes the constructed-but-never-stored instance
/// instead of discarding it unreferenced. Before the fix, a disabled plugin's
/// instance was constructed (running its constructor) but neither
/// Initialize()'d nor stored, and nothing ever called Dispose() on it.
/// </summary>
public class PluginLoaderDisposalTests : IDisposable
{
    private const string DisposalMarkerEnvVar = "NOMERCY_TEST_ECHO_DISPOSAL_MARKER";

    private readonly string _tempPluginsDir;
    private readonly string _echoPluginDir;
    private readonly string _markerPath;
    private readonly PluginManager _manager;
    private readonly InMemoryEventBus _eventBus;

    public PluginLoaderDisposalTests()
    {
        _tempPluginsDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-loader-disposal-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _tempPluginsDir);

        _echoPluginDir = Path.Combine(path1: _tempPluginsDir, path2: "Echo");
        Directory.CreateDirectory(path: _echoPluginDir);

        _markerPath = Path.Combine(path1: _tempPluginsDir, path2: "disposed.marker");

        _eventBus = new();
        _manager = new(
            eventBus: _eventBus,
            serviceProvider: new MinimalServiceProvider(),
            logger: NullLogger<PluginManager>.Instance,
            pluginsPath: _tempPluginsDir,
            storage: TestStorageHelper.CreateStorage(rootPath: _tempPluginsDir),
            driver: TestStorageHelper.CreateBackend()
        );
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(variable: DisposalMarkerEnvVar, value: null);

        _manager.Dispose();

        // Force GC to collect the PluginLoadContext so Windows releases the DLL file lock.
        GC.Collect();
        GC.WaitForPendingFinalizers();

        try
        {
            if (Directory.Exists(path: _tempPluginsDir))
                Directory.Delete(path: _tempPluginsDir, recursive: true);
        }
        catch (Exception) { }
    }

    private static string GetEchoPluginBinDir()
    {
        string testBinDir = Path.GetDirectoryName(
            path: typeof(PluginLoaderDisposalTests).Assembly.Location
        )!;
        string repoRoot = Path.GetFullPath(path: Path.Combine(paths: [testBinDir, "..", "..", "..", "..", ".."]));
        // Mirror the test's own build configuration + TFM so this works under
        // both Debug (local) and Release (CI coverage).
        string tfm = Path.GetFileName(path: testBinDir);
        string configuration = Path.GetFileName(path: Path.GetDirectoryName(path: testBinDir)!);
        return Path.Combine(paths: [repoRoot, "tests", "NoMercy.Plugin.Samples.Echo", "bin", configuration, tfm]
        );
    }

    private void StageEchoPlugin(bool autoEnabled)
    {
        string binDir = GetEchoPluginBinDir();
        string dllSrc = Path.Combine(path1: binDir, path2: "NoMercy.Plugin.Samples.Echo.dll");

        if (!File.Exists(path: dllSrc))
            throw new FileNotFoundException(
                message: $"Echo plugin DLL not found at '{dllSrc}'. Build NoMercy.Plugin.Samples.Echo first."
            );

        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.dll"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: _echoPluginDir, path2: Path.GetFileName(path: file)), overwrite: true);

        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.deps.json"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: _echoPluginDir, path2: Path.GetFileName(path: file)), overwrite: true);

        // A custom manifest (not the repo's committed plugin.json) so this
        // test controls autoEnabled independently of every other Echo-staging
        // test in this project.
        string manifestJson = $$"""
            {
              "id": "11111111-2222-3333-4444-555555555555",
              "name": "Echo",
              "version": "0.1.0",
              "description": "Sample plugin for the test suite",
              "assembly": "NoMercy.Plugin.Samples.Echo.dll",
              "autoEnabled": {{(autoEnabled ? "true" : "false")}}
            }
            """;
        File.WriteAllText(path: Path.Combine(path1: _echoPluginDir, path2: "plugin.json"), contents: manifestJson);
    }

    [Fact]
    public async Task LoadPluginsFromDirectoryAsync_NonAutoEnabledPlugin_DisposesConstructedInstance()
    {
        Environment.SetEnvironmentVariable(variable: DisposalMarkerEnvVar, value: _markerPath);
        StageEchoPlugin(autoEnabled: false);

        await _manager.LoadPluginsFromDirectoryAsync();

        IReadOnlyList<PluginInfo> installed = _manager.GetInstalledPlugins();
        installed.Should().ContainSingle();
        installed[index: 0].Status.Should().Be(expected: PluginStatus.Disabled);

        // Prior to the fix, the constructed-but-never-stored instance was
        // simply discarded — Dispose() never ran and this marker never
        // appeared.
        File.Exists(path: _markerPath).Should().BeTrue();
    }

    [Fact]
    public async Task LoadPluginsFromDirectoryAsync_AutoEnabledPlugin_DoesNotDisposeActiveInstance()
    {
        Environment.SetEnvironmentVariable(variable: DisposalMarkerEnvVar, value: _markerPath);
        StageEchoPlugin(autoEnabled: true);

        await _manager.LoadPluginsFromDirectoryAsync();

        IReadOnlyList<PluginInfo> installed = _manager.GetInstalledPlugins();
        installed.Should().ContainSingle();
        installed[index: 0].Status.Should().Be(expected: PluginStatus.Active);

        // An active plugin's instance is stored and stays live — the fix
        // must only dispose the instance when it is NOT going to be stored.
        File.Exists(path: _markerPath).Should().BeFalse();
    }

    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
