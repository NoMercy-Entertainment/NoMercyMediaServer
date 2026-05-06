namespace NoMercy.Plugins.Abstractions;

public record PluginLoadResult(Guid PluginId, string Name, string Version, IPlugin Instance);
