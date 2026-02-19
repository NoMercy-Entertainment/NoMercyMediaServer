namespace NoMercy.Plugins.Abstractions;

public interface IPluginConfiguration
{
    T? GetConfiguration<T>() where T : class, new();
    Task<T?> GetConfigurationAsync<T>(CancellationToken ct = default) where T : class, new();
    void SaveConfiguration<T>(T configuration) where T : class;
    Task SaveConfigurationAsync<T>(T configuration, CancellationToken ct = default) where T : class;
    bool HasConfiguration();
    void DeleteConfiguration();
}
