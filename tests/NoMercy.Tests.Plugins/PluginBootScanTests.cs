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

// Stages the Echo sample plugin in a temp directory and verifies that
// PluginManager.LoadAllAsync discovers, loads, and returns it.
public class PluginBootScanTests : IDisposable
{
    private readonly string _tempPluginsDir;
    private readonly string _echoPluginDir;
    private readonly PluginManager _manager;
    private readonly InMemoryEventBus _eventBus;

    public PluginBootScanTests()
    {
        _tempPluginsDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-boot-scan-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _tempPluginsDir);

        _echoPluginDir = Path.Combine(path1: _tempPluginsDir, path2: "Echo");
        Directory.CreateDirectory(path: _echoPluginDir);

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
        _manager.Dispose();

        // Force GC to collect the PluginLoadContext so Windows releases the DLL file lock.
        // AssemblyLoadContext.Unload() is asynchronous — the context is only fully unloaded
        // after all roots are released and the GC has collected it.
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
        // Navigate from the test bin dir to the Echo project's own output directory.
        // Echo must NOT be loaded into the default AssemblyLoadContext (no ProjectReference
        // copy) — loading it through PluginLoadContext requires clean isolation.
        string testBinDir = Path.GetDirectoryName(path: typeof(PluginBootScanTests).Assembly.Location)!;
        string repoRoot = Path.GetFullPath(path: Path.Combine(paths: [testBinDir, "..", "..", "..", "..", ".."]));
        // Mirror the test's own build configuration + TFM so this works under
        // both Debug (local) and Release (CI coverage) — hardcoding "Debug"
        // makes every Echo-staging test fail when CI builds Release.
        string tfm = Path.GetFileName(path: testBinDir);
        string configuration = Path.GetFileName(path: Path.GetDirectoryName(path: testBinDir)!);
        return Path.Combine(paths: [repoRoot, "tests", "NoMercy.Plugin.Samples.Echo", "bin", configuration, tfm]
        );
    }

    private void StageEchoPlugin()
    {
        string binDir = GetEchoPluginBinDir();
        string dllSrc = Path.Combine(path1: binDir, path2: "NoMercy.Plugin.Samples.Echo.dll");
        string manifestSrc = Path.Combine(path1: binDir, path2: "plugin.json");

        if (!File.Exists(path: dllSrc))
            throw new FileNotFoundException(
                message: $"Echo plugin DLL not found at '{dllSrc}'. Build NoMercy.Plugin.Samples.Echo first."
            );

        // Copy DLL and all dependencies that the Echo assembly needs at runtime.
        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.dll"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: _echoPluginDir, path2: Path.GetFileName(path: file)), overwrite: true);

        // Copy the .deps.json — AssemblyDependencyResolver reads it to resolve the
        // plugin's dependencies. Without it the resolver constructor throws and the
        // plugin silently fails to load (results come back empty). This mirrors how
        // a real plugin package ships its DLL alongside its deps manifest.
        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.deps.json"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: _echoPluginDir, path2: Path.GetFileName(path: file)), overwrite: true);

        // Copy the plugin manifest.
        if (File.Exists(path: manifestSrc))
            File.Copy(sourceFileName: manifestSrc, destFileName: Path.Combine(path1: _echoPluginDir, path2: "plugin.json"), overwrite: true);
    }

    [Fact]
    public async Task LoadAllAsync_EmptyPluginsDir_ReturnsEmpty()
    {
        IReadOnlyList<PluginLoadResult> results = await _manager.LoadAllAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAllAsync_NonExistentPluginsDir_ReturnsEmptyWithoutThrowing()
    {
        string missing = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "no-such-dir-" + Guid.NewGuid().ToString(format: "N")
        );
        PluginManager manager = new(
            eventBus: new InMemoryEventBus(),
            serviceProvider: new MinimalServiceProvider(),
            logger: NullLogger<PluginManager>.Instance,
            pluginsPath: missing,
            storage: TestStorageHelper.CreateStorage(rootPath: missing),
            driver: TestStorageHelper.CreateBackend()
        );

        IReadOnlyList<PluginLoadResult> results = await manager.LoadAllAsync();

        results.Should().BeEmpty();
        manager.Dispose();
    }

    [Fact]
    public async Task LoadAllAsync_DirWithNoManifest_SkipsPlugin()
    {
        // Create a plugin dir without plugin.json — should be skipped.
        string noManifestDir = Path.Combine(path1: _tempPluginsDir, path2: "NoManifest");
        Directory.CreateDirectory(path: noManifestDir);
        await File.WriteAllTextAsync(path: Path.Combine(path1: noManifestDir, path2: "something.dll"), contents: "garbage");

        IReadOnlyList<PluginLoadResult> results = await _manager.LoadAllAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAllAsync_EchoPlugin_ReturnsOneResult()
    {
        StageEchoPlugin();

        IReadOnlyList<PluginLoadResult> results = await _manager.LoadAllAsync();

        results.Should().ContainSingle();
        results[index: 0].Name.Should().Be(expected: "Echo");
    }

    [Fact]
    public async Task LoadAllAsync_EchoPlugin_ResultVersionMatchesManifest()
    {
        StageEchoPlugin();

        IReadOnlyList<PluginLoadResult> results = await _manager.LoadAllAsync();

        results.Should().ContainSingle();
        results[index: 0].Version.Should().Be(expected: "0.1.0");
    }

    [Fact]
    public async Task LoadAllAsync_EchoPlugin_InstanceIsNotNull()
    {
        StageEchoPlugin();

        IReadOnlyList<PluginLoadResult> results = await _manager.LoadAllAsync();

        results.Should().ContainSingle();
        results[index: 0].Instance.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAllAsync_EchoPlugin_GetProfileReturnsNonNullProfile()
    {
        StageEchoPlugin();

        IReadOnlyList<PluginLoadResult> results = await _manager.LoadAllAsync();

        IPlugin instance = results.Should().ContainSingle().Subject.Instance;
        IEncoderPlugin encoderPlugin = instance.Should().BeAssignableTo<IEncoderPlugin>().Subject;

        EncodingProfile profile = encoderPlugin.GetProfile(info: new() { FilePath = "/test.mkv" })!;

        profile.Should().NotBeNull();
        profile.VideoCodec.Should().Be(expected: "h264");
        profile.AudioCodec.Should().Be(expected: "aac");
    }

    [Fact]
    public async Task LoadAllAsync_DisabledPluginWithSurvivingInstance_IsExcludedFromResults()
    {
        // DisablePluginAsync disposes the instance but LoadedPlugin (and its
        // Instance reference) is immutable — the record stays in the registry
        // with a non-null Instance and a non-Active status. LoadAllAsync's own
        // `Instance is not null && Status == Active` filter must still exclude
        // it, isolating the `&& Status == Active` half of that guard.
        //
        // Loaded directly via LoadPluginAssemblyAsync (not staged under
        // _tempPluginsDir) so the LoadAllAsync call below's own directory
        // rescan does not reprocess and re-enable it.
        string echoBinDir = GetEchoPluginBinDir();
        string echoDllPath = Path.Combine(path1: echoBinDir, path2: "NoMercy.Plugin.Samples.Echo.dll");
        await _manager.LoadPluginAssemblyAsync(assemblyPath: echoDllPath);
        PluginInfo activeBefore = _manager.GetInstalledPlugins().Should().ContainSingle().Subject;
        await _manager.DisablePluginAsync(pluginId: activeBefore.Id);

        IReadOnlyList<PluginLoadResult> results = await _manager.LoadAllAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAllAsync_NonAutoEnabledPluginAlongsideActiveOne_OnlyReturnsTheActiveOne()
    {
        // A manifest-driven plugin with autoEnabled:false loads Disabled with
        // Instance == null — the OTHER half of LoadAllAsync's filter (distinct
        // from the "disposed-but-still-referenced" case above, which has a
        // non-null Instance). Both plugins share the Echo assembly but use
        // different manifest ids so they occupy separate registry entries.
        StageEchoPlugin();
        string disabledDir = Path.Combine(path1: _tempPluginsDir, path2: "EchoDisabled");
        Directory.CreateDirectory(path: disabledDir);
        string binDir = GetEchoPluginBinDir();
        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.dll"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: disabledDir, path2: Path.GetFileName(path: file)), overwrite: true);
        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.deps.json"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: disabledDir, path2: Path.GetFileName(path: file)), overwrite: true);
        File.WriteAllText(
            path: Path.Combine(path1: disabledDir, path2: "plugin.json"),
            contents: """
                      {
                        "id": "66666666-1111-2222-3333-444444444444",
                        "name": "EchoDisabled",
                        "version": "0.1.0",
                        "description": "same assembly, never auto-enabled",
                        "assembly": "NoMercy.Plugin.Samples.Echo.dll",
                        "autoEnabled": false
                      }
                      """
        );

        IReadOnlyList<PluginLoadResult> results = await _manager.LoadAllAsync();

        results.Should().ContainSingle();
        results[index: 0].Name.Should().Be(expected: "Echo");
        _manager
            .GetInstalledPlugins()
            .Should()
            .Contain(predicate: p => p.Name == "EchoDisabled" && p.Status == PluginStatus.Disabled);
    }

    [Fact]
    public async Task LoadAllAsync_MalformedManifest_SkipsAndContinues()
    {
        // Bad manifest — should not throw; other plugins (if any) continue loading.
        string badDir = Path.Combine(path1: _tempPluginsDir, path2: "Bad");
        Directory.CreateDirectory(path: badDir);
        await File.WriteAllTextAsync(path: Path.Combine(path1: badDir, path2: "plugin.json"), contents: "not json");

        Func<Task> act = () => _manager.LoadAllAsync();

        await act.Should().NotThrowAsync();
    }

    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
