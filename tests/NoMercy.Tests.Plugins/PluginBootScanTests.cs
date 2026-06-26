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
            Path.GetTempPath(),
            "nomercy-boot-scan-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(_tempPluginsDir);

        _echoPluginDir = Path.Combine(_tempPluginsDir, "Echo");
        Directory.CreateDirectory(_echoPluginDir);

        _eventBus = new();
        _manager = new(
            _eventBus,
            new MinimalServiceProvider(),
            NullLogger<PluginManager>.Instance,
            _tempPluginsDir,
            TestStorageHelper.CreateStorage(_tempPluginsDir),
            TestStorageHelper.CreateBackend()
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
            if (Directory.Exists(_tempPluginsDir))
                Directory.Delete(_tempPluginsDir, recursive: true);
        }
        catch (Exception) { }
    }

    private static string GetEchoPluginBinDir()
    {
        // Navigate from the test bin dir to the Echo project's own output directory.
        // Echo must NOT be loaded into the default AssemblyLoadContext (no ProjectReference
        // copy) — loading it through PluginLoadContext requires clean isolation.
        string testBinDir = Path.GetDirectoryName(typeof(PluginBootScanTests).Assembly.Location)!;
        string repoRoot = Path.GetFullPath(Path.Combine(testBinDir, "..", "..", "..", "..", ".."));
        // Mirror the test's own build configuration + TFM so this works under
        // both Debug (local) and Release (CI coverage) — hardcoding "Debug"
        // makes every Echo-staging test fail when CI builds Release.
        string tfm = Path.GetFileName(testBinDir);
        string configuration = Path.GetFileName(Path.GetDirectoryName(testBinDir)!);
        return Path.Combine(
            repoRoot,
            "tests",
            "NoMercy.Plugin.Samples.Echo",
            "bin",
            configuration,
            tfm
        );
    }

    private void StageEchoPlugin()
    {
        string binDir = GetEchoPluginBinDir();
        string dllSrc = Path.Combine(binDir, "NoMercy.Plugin.Samples.Echo.dll");
        string manifestSrc = Path.Combine(binDir, "plugin.json");

        if (!File.Exists(dllSrc))
            throw new FileNotFoundException(
                $"Echo plugin DLL not found at '{dllSrc}'. Build NoMercy.Plugin.Samples.Echo first."
            );

        // Copy DLL and all dependencies that the Echo assembly needs at runtime.
        foreach (string file in Directory.EnumerateFiles(binDir, "*.dll"))
            File.Copy(file, Path.Combine(_echoPluginDir, Path.GetFileName(file)), overwrite: true);

        // Copy the .deps.json — AssemblyDependencyResolver reads it to resolve the
        // plugin's dependencies. Without it the resolver constructor throws and the
        // plugin silently fails to load (results come back empty). This mirrors how
        // a real plugin package ships its DLL alongside its deps manifest.
        foreach (string file in Directory.EnumerateFiles(binDir, "*.deps.json"))
            File.Copy(file, Path.Combine(_echoPluginDir, Path.GetFileName(file)), overwrite: true);

        // Copy the plugin manifest.
        if (File.Exists(manifestSrc))
            File.Copy(manifestSrc, Path.Combine(_echoPluginDir, "plugin.json"), overwrite: true);
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
            Path.GetTempPath(),
            "no-such-dir-" + Guid.NewGuid().ToString("N")
        );
        PluginManager manager = new(
            new InMemoryEventBus(),
            new MinimalServiceProvider(),
            NullLogger<PluginManager>.Instance,
            missing,
            TestStorageHelper.CreateStorage(missing),
            TestStorageHelper.CreateBackend()
        );

        IReadOnlyList<PluginLoadResult> results = await manager.LoadAllAsync();

        results.Should().BeEmpty();
        manager.Dispose();
    }

    [Fact]
    public async Task LoadAllAsync_DirWithNoManifest_SkipsPlugin()
    {
        // Create a plugin dir without plugin.json — should be skipped.
        string noManifestDir = Path.Combine(_tempPluginsDir, "NoManifest");
        Directory.CreateDirectory(noManifestDir);
        await File.WriteAllTextAsync(Path.Combine(noManifestDir, "something.dll"), "garbage");

        IReadOnlyList<PluginLoadResult> results = await _manager.LoadAllAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAllAsync_EchoPlugin_ReturnsOneResult()
    {
        StageEchoPlugin();

        IReadOnlyList<PluginLoadResult> results = await _manager.LoadAllAsync();

        results.Should().ContainSingle();
        results[0].Name.Should().Be("Echo");
    }

    [Fact]
    public async Task LoadAllAsync_EchoPlugin_ResultVersionMatchesManifest()
    {
        StageEchoPlugin();

        IReadOnlyList<PluginLoadResult> results = await _manager.LoadAllAsync();

        results.Should().ContainSingle();
        results[0].Version.Should().Be("0.1.0");
    }

    [Fact]
    public async Task LoadAllAsync_EchoPlugin_InstanceIsNotNull()
    {
        StageEchoPlugin();

        IReadOnlyList<PluginLoadResult> results = await _manager.LoadAllAsync();

        results.Should().ContainSingle();
        results[0].Instance.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAllAsync_EchoPlugin_GetProfileReturnsNonNullProfile()
    {
        StageEchoPlugin();

        IReadOnlyList<PluginLoadResult> results = await _manager.LoadAllAsync();

        IPlugin instance = results.Should().ContainSingle().Subject.Instance;
        IEncoderPlugin encoderPlugin = instance.Should().BeAssignableTo<IEncoderPlugin>().Subject;

        EncodingProfile profile = encoderPlugin.GetProfile(new() { FilePath = "/test.mkv" });

        profile.Should().NotBeNull();
        profile.VideoCodec.Should().Be("h264");
        profile.AudioCodec.Should().Be("aac");
    }

    [Fact]
    public async Task LoadAllAsync_MalformedManifest_SkipsAndContinues()
    {
        // Bad manifest — should not throw; other plugins (if any) continue loading.
        string badDir = Path.Combine(_tempPluginsDir, "Bad");
        Directory.CreateDirectory(badDir);
        await File.WriteAllTextAsync(Path.Combine(badDir, "plugin.json"), "not json");

        Func<Task> act = () => _manager.LoadAllAsync();

        await act.Should().NotThrowAsync();
    }

    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
