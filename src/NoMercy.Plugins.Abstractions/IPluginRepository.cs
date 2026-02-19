namespace NoMercy.Plugins.Abstractions;

public interface IPluginRepository
{
    IReadOnlyList<PluginRepositoryInfo> GetRepositories();
    Task AddRepositoryAsync(string name, string url, CancellationToken ct = default);
    Task RemoveRepositoryAsync(string name, CancellationToken ct = default);
    Task RefreshAsync(CancellationToken ct = default);
    IReadOnlyList<PluginRepositoryEntry> GetAvailablePlugins();
    PluginRepositoryEntry? FindPlugin(Guid pluginId);
    PluginVersionEntry? FindVersion(Guid pluginId, string version);
}

public class PluginRepositoryInfo
{
    public required string Name { get; init; }
    public required string Url { get; init; }
    public bool Enabled { get; set; } = true;
}
