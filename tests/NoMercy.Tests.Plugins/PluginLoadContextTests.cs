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

using System.Reflection;
using FluentAssertions;
using NoMercy.Plugins;
using Xunit;

namespace NoMercy.Tests.Plugins;

/// <summary>
/// Drives <see cref="PluginLoadContext"/>'s protected resolution hooks directly
/// through a minimal exposing subclass. The production call sites only invoke
/// these indirectly (via the CLR's own assembly-resolution machinery while
/// running plugin code), which cannot deterministically exercise every branch —
/// e.g. an unmanaged DLL resolution failure requires no real plugin to ever
/// P/Invoke anything. Calling the real, inherited implementation directly is not
/// a mock of the unit under test: it is the same method the runtime would call.
/// </summary>
public class PluginLoadContextTests
{
    private static string GetFailuresPluginDllPath()
    {
        string testBinDir = Path.GetDirectoryName(
            typeof(PluginLoadContextTests).Assembly.Location
        )!;
        string configDir = Path.GetDirectoryName(testBinDir)!;
        string buildConfig = Path.GetFileName(configDir);
        string repoRoot = Path.GetFullPath(Path.Combine([testBinDir, "..", "..", "..", "..", ".."]));

        string dllPath = Path.Combine([repoRoot, "tests", "NoMercy.Plugin.Samples.Failures", "bin", buildConfig, "net10.0", "NoMercy.Plugin.Samples.Failures.dll"]
        );

        if (!File.Exists(dllPath))
            throw new FileNotFoundException(
                $"Failures plugin DLL not found at '{dllPath}'. Build NoMercy.Plugin.Samples.Failures first."
            );

        return dllPath;
    }

    [Fact]
    public void Load_SharedAssemblyName_ReturnsNull_FallsBackToDefaultContext()
    {
        using ExposedPluginLoadContext context = new(GetFailuresPluginDllPath());

        Assembly? resolved = context.InvokeLoad(new AssemblyName("NoMercy.Plugins.Abstractions"));

        resolved.Should().BeNull("shared assemblies must defer to the default AssemblyLoadContext");
    }

    [Fact]
    public void Load_AssemblyNameWithNullName_FallsThroughToResolver()
    {
        // AssemblyName.Name is null only for a bare `new AssemblyName()` with
        // no name ever assigned — not something the CLR's own assembly
        // resolution machinery would realistically pass, but the `is not
        // null` guard exists for exactly this shape and deserves a direct,
        // deterministic test rather than being left unreached.
        using ExposedPluginLoadContext context = new(GetFailuresPluginDllPath());
        AssemblyName unnamed = new();
        unnamed.Name.Should().BeNull("this is exactly the edge case under test");

        Assembly? resolved = context.InvokeLoad(unnamed);

        resolved.Should().BeNull();
    }

    [Fact]
    public void Load_NonSharedNameWithNoResolvablePath_ReturnsNull()
    {
        using ExposedPluginLoadContext context = new(GetFailuresPluginDllPath());

        Assembly? resolved = context.InvokeLoad(new AssemblyName("Totally.Unknown.Assembly.Xyz"));

        resolved.Should().BeNull();
    }

    [Fact]
    public void Load_NonSharedBundledDependency_ResolvesAndLoadsIt()
    {
        // Newtonsoft.Json is bundled alongside the Failures fixture assembly
        // specifically because it is NOT in PluginHostOptions.DefaultSharedAssemblies —
        // the only way to reach the resolver's "found a path" branch is a real,
        // non-shared dependency the plugin's own .deps.json actually declares.
        using ExposedPluginLoadContext context = new(GetFailuresPluginDllPath());

        Assembly? resolved = context.InvokeLoad(new AssemblyName("Newtonsoft.Json"));

        resolved.Should().NotBeNull();
        resolved!.GetName().Name.Should().Be("Newtonsoft.Json");
    }

    [Fact]
    public void LoadUnmanagedDll_UnresolvableName_ReturnsZero()
    {
        using ExposedPluginLoadContext context = new(GetFailuresPluginDllPath());

        IntPtr handle = context.InvokeLoadUnmanagedDll("nomercy-test-nonexistent-native-lib");

        handle.Should().Be(IntPtr.Zero);
    }

    private sealed class ExposedPluginLoadContext(string pluginPath)
        : PluginLoadContext(pluginPath),
            IDisposable
    {
        public Assembly? InvokeLoad(AssemblyName assemblyName) => Load(assemblyName);

        public IntPtr InvokeLoadUnmanagedDll(string unmanagedDllName) =>
            LoadUnmanagedDll(unmanagedDllName);

        public void Dispose() => Unload();
    }
}
