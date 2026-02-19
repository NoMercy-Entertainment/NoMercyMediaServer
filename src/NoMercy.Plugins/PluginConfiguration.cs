using System.Text.Json;
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Plugins;

public class PluginConfiguration : IPluginConfiguration
{
    private readonly string _configFilePath;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public PluginConfiguration(string dataFolderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFolderPath);
        _configFilePath = Path.Combine(dataFolderPath, "config.json");
    }

    public T? GetConfiguration<T>() where T : class, new()
    {
        lock (_lock)
        {
            if (!File.Exists(_configFilePath))
            {
                return null;
            }

            string json = File.ReadAllText(_configFilePath);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
    }

    public async Task<T?> GetConfigurationAsync<T>(CancellationToken ct = default) where T : class, new()
    {
        if (!File.Exists(_configFilePath))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(_configFilePath, ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public void SaveConfiguration<T>(T configuration) where T : class
    {
        ArgumentNullException.ThrowIfNull(configuration);

        lock (_lock)
        {
            string? directory = Path.GetDirectoryName(_configFilePath);
            if (directory is not null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(configuration, JsonOptions);
            File.WriteAllText(_configFilePath, json);
        }
    }

    public async Task SaveConfigurationAsync<T>(T configuration, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? directory = Path.GetDirectoryName(_configFilePath);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(configuration, JsonOptions);
        await File.WriteAllTextAsync(_configFilePath, json, ct);
    }

    public bool HasConfiguration()
    {
        return File.Exists(_configFilePath);
    }

    public void DeleteConfiguration()
    {
        lock (_lock)
        {
            if (File.Exists(_configFilePath))
            {
                File.Delete(_configFilePath);
            }
        }
    }
}
