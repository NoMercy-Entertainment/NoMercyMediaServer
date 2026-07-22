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
            path1: Path.GetTempPath(),
            path2: "nomercy-svc-reg-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _tempPluginsDir);

        _echoPluginDir = Path.Combine(path1: _tempPluginsDir, path2: "Echo");
        Directory.CreateDirectory(path: _echoPluginDir);
    }

    public void Dispose()
    {
        // Force GC to collect any PluginLoadContext so Windows releases the DLL file lock.
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
        //
        // Test assembly location: .../<repo>/tests/NoMercy.Tests.Plugins/bin/<Config>/net10.0/
        // Echo location:          .../<repo>/tests/NoMercy.Plugin.Samples.Echo/bin/<Config>/net10.0/
        // Derive <Config> from the test assembly path so Debug and Release CI jobs both resolve.
        string testBinDir = Path.GetDirectoryName(
            path: typeof(PluginServiceRegistrationTests).Assembly.Location
        )!;

        // testBinDir == .../bin/<Config>/net10.0  →  parent of parent is .../bin
        // parent of that is the test project root, parent again is tests/, parent again is repo root.
        string tfmDir = testBinDir; // net10.0
        string configDir = Path.GetDirectoryName(path: tfmDir)!; // <Config> e.g. Debug | Release
        string buildConfig = Path.GetFileName(path: configDir);

        string repoRoot = Path.GetFullPath(path: Path.Combine(paths: [testBinDir, "..", "..", "..", "..", ".."]));

        // Prefer the configuration that matches the currently running test assembly.
        // Fall back to the other known configuration so a local Debug build still works
        // when a Release artefact happens to be present (and vice-versa).
        string preferred = Path.Combine(paths: [repoRoot, "tests", "NoMercy.Plugin.Samples.Echo", "bin", buildConfig, "net10.0"]
        );
        string fallback = Path.Combine(paths:
            [repoRoot, "tests", "NoMercy.Plugin.Samples.Echo", "bin", string.Equals(a: buildConfig, b: "Release", comparisonType: StringComparison.OrdinalIgnoreCase)
                ? "Debug"
                : "Release",
                "net10.0"
            ]
        );

        if (Directory.Exists(path: preferred))
            return preferred;

        if (Directory.Exists(path: fallback))
            return fallback;

        // Return preferred so the FileNotFoundException message names the right config.
        return preferred;
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

        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.dll"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: _echoPluginDir, path2: Path.GetFileName(path: file)), overwrite: true);

        if (File.Exists(path: manifestSrc))
            File.Copy(sourceFileName: manifestSrc, destFileName: Path.Combine(path1: _echoPluginDir, path2: "plugin.json"), overwrite: true);
    }

    private static string GetFailuresPluginBinDir()
    {
        string testBinDir = Path.GetDirectoryName(
            path: typeof(PluginServiceRegistrationTests).Assembly.Location
        )!;
        string tfmDir = testBinDir;
        string configDir = Path.GetDirectoryName(path: tfmDir)!;
        string buildConfig = Path.GetFileName(path: configDir);
        string repoRoot = Path.GetFullPath(path: Path.Combine(paths: [testBinDir, "..", "..", "..", "..", ".."]));

        return Path.Combine(paths: [repoRoot, "tests", "NoMercy.Plugin.Samples.Failures", "bin", buildConfig, "net10.0"]
        );
    }

    private void StageFailuresPlugin(string targetDir)
    {
        string binDir = GetFailuresPluginBinDir();
        string dllSrc = Path.Combine(path1: binDir, path2: "NoMercy.Plugin.Samples.Failures.dll");
        string manifestSrc = Path.Combine(path1: binDir, path2: "plugin.json");

        if (!File.Exists(path: dllSrc))
            throw new FileNotFoundException(
                message: $"Failures plugin DLL not found at '{dllSrc}'. Build NoMercy.Plugin.Samples.Failures first."
            );

        Directory.CreateDirectory(path: targetDir);
        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.dll"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: targetDir, path2: Path.GetFileName(path: file)), overwrite: true);
        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.deps.json"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: targetDir, path2: Path.GetFileName(path: file)), overwrite: true);

        if (File.Exists(path: manifestSrc))
            File.Copy(sourceFileName: manifestSrc, destFileName: Path.Combine(path1: targetDir, path2: "plugin.json"), overwrite: true);
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_EmptyDir_DoesNotThrow()
    {
        IServiceCollection services = new ServiceCollection();

        Action act = () => services.RegisterPluginServicesFromManifests(pluginsPath: _tempPluginsDir);

        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_NonExistentDir_DoesNotThrow()
    {
        IServiceCollection services = new ServiceCollection();
        string missing = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "no-such-" + Guid.NewGuid().ToString(format: "N")
        );

        Action act = () => services.RegisterPluginServicesFromManifests(pluginsPath: missing);

        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_EchoPlugin_DoesNotThrow()
    {
        StageEchoPlugin();

        IServiceCollection services = new ServiceCollection();

        Action act = () => services.RegisterPluginServicesFromManifests(pluginsPath: _tempPluginsDir);

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

        services.RegisterPluginServicesFromManifests(pluginsPath: _tempPluginsDir);

        int countAfter = services.Count;

        // Echo does not register any services — count stays the same.
        countAfter.Should().Be(expected: countBefore);
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_MalformedManifest_DoesNotThrow()
    {
        string badDir = Path.Combine(path1: _tempPluginsDir, path2: "Bad");
        Directory.CreateDirectory(path: badDir);
        File.WriteAllText(path: Path.Combine(path1: badDir, path2: "plugin.json"), contents: "not json {{{{");

        IServiceCollection services = new ServiceCollection();

        Action act = () => services.RegisterPluginServicesFromManifests(pluginsPath: _tempPluginsDir);

        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_ConfigurationsAndDataDirs_AreSkipped()
    {
        // Real reserved directories the plugin manager itself creates alongside
        // installed plugins — the scanner must recognise and skip both by name
        // rather than trying (and failing) to read a plugin.json from them.
        Directory.CreateDirectory(path: Path.Combine(path1: _tempPluginsDir, path2: "configurations"));
        Directory.CreateDirectory(path: Path.Combine(path1: _tempPluginsDir, path2: "data"));

        IServiceCollection services = new ServiceCollection();

        Action act = () => services.RegisterPluginServicesFromManifests(pluginsPath: _tempPluginsDir);

        act.Should().NotThrow();
        services.Should().BeEmpty();
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_ManifestReferencesMissingAssembly_SkipsPlugin()
    {
        string pluginDir = Path.Combine(path1: _tempPluginsDir, path2: "MissingAssembly");
        Directory.CreateDirectory(path: pluginDir);
        File.WriteAllText(
            path: Path.Combine(path1: pluginDir, path2: "plugin.json"),
            contents: """
                      {
                        "id": "44444444-4444-4444-4444-444444444444",
                        "name": "MissingAssembly",
                        "description": "manifest with no matching dll on disk",
                        "version": "1.0.0",
                        "assembly": "DoesNotExist.dll"
                      }
                      """
        );

        IServiceCollection services = new ServiceCollection();

        Action act = () => services.RegisterPluginServicesFromManifests(pluginsPath: _tempPluginsDir);

        act.Should().NotThrow();
        services.Should().BeEmpty();
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_FailuresPlugin_DiscoversAndRegistersServiceRegistrator()
    {
        // The Failures fixture assembly contains a healthy IPluginServiceRegistrator
        // (ServiceRegistratorPlugin), a non-instantiable abstract one
        // (AbstractServiceRegistratorBase, which must be found but never
        // constructed), and two IPlugin-only types that must not match the
        // IPluginServiceRegistrator filter at all.
        string pluginDir = Path.Combine(path1: _tempPluginsDir, path2: "Failures");
        StageFailuresPlugin(targetDir: pluginDir);

        IServiceCollection services = new ServiceCollection();

        services.RegisterPluginServicesFromManifests(pluginsPath: _tempPluginsDir);

        // The registered service's Type was loaded through a transient,
        // already-unloaded PluginLoadContext — comparing by CLR type identity
        // (or resolving it back out of a ServiceProvider) is unsafe across that
        // ALC boundary. Comparing the descriptor's type NAME is the correct,
        // ALC-agnostic way to prove RegisterServices actually ran.
        services.Should().ContainSingle();
        services[index: 0]
            .ServiceType.FullName.Should()
            .Be(expected: "NoMercy.Plugin.Samples.Failures.FailuresPluginMarker");
    }
}
