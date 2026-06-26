using NoMercy.Plugins.Abstractions;

namespace NoMercy.Service.Hosting;

public interface IPluginLoader
{
    Task<IReadOnlyList<PluginLoadResult>> LoadPlugins(CancellationToken ct);
}
