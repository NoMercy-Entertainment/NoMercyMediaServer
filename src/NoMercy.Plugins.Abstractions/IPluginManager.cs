namespace NoMercy.Plugins.Abstractions;

public interface IPluginManager
{
    IReadOnlyList<PluginInfo> GetInstalledPlugins();
    Task InstallPluginAsync(string packageUrl, CancellationToken ct = default);
    Task EnablePluginAsync(Guid pluginId, CancellationToken ct = default);
    Task DisablePluginAsync(Guid pluginId, CancellationToken ct = default);
    Task UninstallPluginAsync(Guid pluginId, CancellationToken ct = default);
}
