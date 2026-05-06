namespace NoMercy.Plugins.Abstractions;

public interface IPluginManager
{
    IReadOnlyList<PluginInfo> GetInstalledPlugins();
    Task InstallPluginAsync(string packageUrl, CancellationToken ct = default);
    Task EnablePluginAsync(Guid pluginId, CancellationToken ct = default);
    Task DisablePluginAsync(Guid pluginId, CancellationToken ct = default);
    Task UninstallPluginAsync(Guid pluginId, CancellationToken ct = default);

    // Boot-time scan: load all plugins in the plugins directory, isolating failures
    // per plugin so one bad plugin never blocks the others.
    Task<IReadOnlyList<PluginLoadResult>> LoadAllAsync(CancellationToken ct = default);
}
