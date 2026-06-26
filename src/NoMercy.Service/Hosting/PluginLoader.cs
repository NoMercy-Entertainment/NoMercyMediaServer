#region License
// Copyright NoMercy (c) 2026. All rights reserved.
#endregion

using NoMercy.NmSystem.SystemCalls;
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Service.Hosting;

public class PluginLoader : IPluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly IPluginManager _pluginManager;

    public PluginLoader(ILogger<PluginLoader> logger, IPluginManager pluginManager)
    {
        _logger = logger;
        _pluginManager = pluginManager;
    }

    public async Task<IReadOnlyList<PluginLoadResult>> LoadPlugins(CancellationToken ct)
    {
        _logger.LogInformation("Loading plugins...");
        
        IReadOnlyList<PluginLoadResult> loadedPlugins = await _pluginManager.LoadAllAsync(ct);
        
        foreach (PluginLoadResult pluginResult in loadedPlugins)
        {
            _logger.LogInformation(
                "Plugin loaded: {Name} {Version} ({PluginId})",
                pluginResult.Name,
                pluginResult.Version,
                pluginResult.PluginId
            );
        }

        return loadedPlugins;
    }
}
