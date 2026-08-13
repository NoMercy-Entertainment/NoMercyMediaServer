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
    private readonly string _pluginDir;
    private readonly IReadOnlySet<string> _sharedAssemblies;

    public PluginLoadContext(string pluginPath, IReadOnlySet<string>? sharedAssemblies = null)
        : base(isCollectible: true)
    {
        _resolver = new(pluginPath);
        _pluginDir = Path.GetDirectoryName(pluginPath)
                     ?? throw new InvalidOperationException("Plugin directory could not be determined.");

        _sharedAssemblies = sharedAssemblies ?? PluginHostOptions.DefaultSharedAssemblies;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Shared assemblies → host resolves
        if (assemblyName.Name is not null && _sharedAssemblies.Contains(assemblyName.Name))
            return null;

        // Framework assemblies → host resolves
        if (assemblyName.Name is not null &&
            (assemblyName.Name.StartsWith("System.", StringComparison.Ordinal) ||
             assemblyName.Name == "System.Private.CoreLib"))
        {
            return null;
        }

        // Resolver first
        string? resolved = _resolver.ResolveAssemblyToPath(assemblyName);
        if (resolved is not null)
            return LoadFromAssemblyPath(resolved);

        // Fallback: plugin directory
        string candidate = Path.Combine(_pluginDir, assemblyName.Name + ".dll");
        if (File.Exists(candidate))
            return LoadFromAssemblyPath(candidate);

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
