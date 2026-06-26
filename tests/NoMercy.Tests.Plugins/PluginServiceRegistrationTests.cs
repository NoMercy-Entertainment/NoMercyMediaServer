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
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Plugins;
using Xunit;

namespace NoMercy.Tests.Plugins;

// Verifies RegisterPluginServicesFromManifests against the Echo sample plugin.
public class PluginServiceRegistrationTests : IDisposable
{
    private readonly string _tempPluginsDir;
    private readonly string _echoPluginDir;

    public PluginServiceRegistrationTests()
    {
        _tempPluginsDir = Path.Combine(
            Path.GetTempPath(),
            "nomercy-svc-reg-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(_tempPluginsDir);

        _echoPluginDir = Path.Combine(_tempPluginsDir, "Echo");
        Directory.CreateDirectory(_echoPluginDir);
    }

    public void Dispose()
    {
        // Force GC to collect any PluginLoadContext so Windows releases the DLL file lock.
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
        //
        // Test assembly location: .../<repo>/tests/NoMercy.Tests.Plugins/bin/<Config>/net10.0/
        // Echo location:          .../<repo>/tests/NoMercy.Plugin.Samples.Echo/bin/<Config>/net10.0/
        // Derive <Config> from the test assembly path so Debug and Release CI jobs both resolve.
        string testBinDir = Path.GetDirectoryName(
            typeof(PluginServiceRegistrationTests).Assembly.Location
        )!;

        // testBinDir == .../bin/<Config>/net10.0  →  parent of parent is .../bin
        // parent of that is the test project root, parent again is tests/, parent again is repo root.
        string tfmDir = testBinDir; // net10.0
        string configDir = Path.GetDirectoryName(tfmDir)!; // <Config> e.g. Debug | Release
        string buildConfig = Path.GetFileName(configDir);

        string repoRoot = Path.GetFullPath(Path.Combine(testBinDir, "..", "..", "..", "..", ".."));

        // Prefer the configuration that matches the currently running test assembly.
        // Fall back to the other known configuration so a local Debug build still works
        // when a Release artefact happens to be present (and vice-versa).
        string preferred = Path.Combine(
            repoRoot,
            "tests",
            "NoMercy.Plugin.Samples.Echo",
            "bin",
            buildConfig,
            "net10.0"
        );
        string fallback = Path.Combine(
            repoRoot,
            "tests",
            "NoMercy.Plugin.Samples.Echo",
            "bin",
            string.Equals(buildConfig, "Release", StringComparison.OrdinalIgnoreCase)
                ? "Debug"
                : "Release",
            "net10.0"
        );

        if (Directory.Exists(preferred))
            return preferred;

        if (Directory.Exists(fallback))
            return fallback;

        // Return preferred so the FileNotFoundException message names the right config.
        return preferred;
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

        foreach (string file in Directory.EnumerateFiles(binDir, "*.dll"))
            File.Copy(file, Path.Combine(_echoPluginDir, Path.GetFileName(file)), overwrite: true);

        if (File.Exists(manifestSrc))
            File.Copy(manifestSrc, Path.Combine(_echoPluginDir, "plugin.json"), overwrite: true);
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_EmptyDir_DoesNotThrow()
    {
        IServiceCollection services = new ServiceCollection();

        Action act = () => services.RegisterPluginServicesFromManifests(_tempPluginsDir);

        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_NonExistentDir_DoesNotThrow()
    {
        IServiceCollection services = new ServiceCollection();
        string missing = Path.Combine(
            Path.GetTempPath(),
            "no-such-" + Guid.NewGuid().ToString("N")
        );

        Action act = () => services.RegisterPluginServicesFromManifests(missing);

        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_EchoPlugin_DoesNotThrow()
    {
        StageEchoPlugin();

        IServiceCollection services = new ServiceCollection();

        Action act = () => services.RegisterPluginServicesFromManifests(_tempPluginsDir);

        // Echo plugin does not implement IPluginServiceRegistrator so no services are added,
        // but the scan itself must never throw.
        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_EchoPlugin_RegistrationCountUnchanged()
    {
        StageEchoPlugin();

        IServiceCollection services = new ServiceCollection();
        int countBefore = services.Count;

        services.RegisterPluginServicesFromManifests(_tempPluginsDir);

        int countAfter = services.Count;

        // Echo does not register any services — count stays the same.
        countAfter.Should().Be(countBefore);
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_MalformedManifest_DoesNotThrow()
    {
        string badDir = Path.Combine(_tempPluginsDir, "Bad");
        Directory.CreateDirectory(badDir);
        File.WriteAllText(Path.Combine(badDir, "plugin.json"), "not json {{{{");

        IServiceCollection services = new ServiceCollection();

        Action act = () => services.RegisterPluginServicesFromManifests(_tempPluginsDir);

        act.Should().NotThrow();
    }
}
