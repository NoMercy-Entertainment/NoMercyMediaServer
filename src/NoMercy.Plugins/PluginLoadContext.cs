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
using System.Runtime.Loader;

namespace NoMercy.Plugins;

public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    // Assemblies whose type identity must be preserved across the host/plugin
    // boundary. Defaults come from PluginHostOptions; callers may pass a custom
    // set (e.g. resolved from IOptions<PluginHostOptions>).
    private readonly IReadOnlySet<string> _sharedAssemblies;

    public PluginLoadContext(string pluginPath, IReadOnlySet<string>? sharedAssemblies = null)
        : base(true)
    {
        _resolver = new(pluginPath);
        _sharedAssemblies = sharedAssemblies ?? PluginHostOptions.DefaultSharedAssemblies;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Return null for shared assemblies so the runtime falls back to the default
        // AssemblyLoadContext. This preserves type identity across the host/plugin boundary.
        if (assemblyName.Name is not null && _sharedAssemblies.Contains(assemblyName.Name))
            return null;

        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is not null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath is not null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
