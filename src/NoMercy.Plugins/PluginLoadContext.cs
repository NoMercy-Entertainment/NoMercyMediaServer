using System.Reflection;
using System.Runtime.Loader;

namespace NoMercy.Plugins;

public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    // Assemblies that must be shared with the host so type identity is preserved.
    // Any assembly whose types must be castable across the plugin/host boundary goes here.
    private static readonly HashSet<string> SharedAssemblies =
    [
        "NoMercy.Plugins.Abstractions",
        "NoMercy.Events",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Logging",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.DependencyInjection",
    ];

    public PluginLoadContext(string pluginPath)
        : base(isCollectible: true)
    {
        _resolver = new(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Return null for shared assemblies so the runtime falls back to the default
        // AssemblyLoadContext. This preserves type identity across the host/plugin boundary.
        if (assemblyName.Name is not null && SharedAssemblies.Contains(assemblyName.Name))
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
