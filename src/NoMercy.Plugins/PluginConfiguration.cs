using System.Text.Json;
using NoMercy.Plugins.Abstractions;
using NoMercy.Storage;

namespace NoMercy.Plugins;

public class PluginConfiguration : IPluginConfiguration
{
    private readonly string _configFilePath;
    private readonly IStorage _storage;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public PluginConfiguration(string dataFolderPath, IStorage? storage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFolderPath);
        _configFilePath = Path.Combine(dataFolderPath, "config.json");

        if (storage is not null)
        {
            _storage = storage;
        }
        else
        {
            IStorageBackend backend = new SystemIoStorageBackend();
            _storage = new LocalStorage(backend, new StoragePathGuard([dataFolderPath], backend));
        }
    }

    public T? GetConfiguration<T>()
        where T : class, new()
    {
        lock (_lock)
        {
            if (!_storage.Exists(_configFilePath))
            {
                return null;
            }

            byte[] bytes = _storage.Read(_configFilePath);
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
    }

    public async Task<T?> GetConfigurationAsync<T>(CancellationToken ct = default)
        where T : class, new()
    {
        if (!_storage.Exists(_configFilePath))
        {
            return null;
        }

        string json = await _storage.ReadAllTextAsync(_configFilePath, ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public void SaveConfiguration<T>(T configuration)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(configuration);

        lock (_lock)
        {
            string? directory = Path.GetDirectoryName(_configFilePath);
            if (directory is not null && !_storage.Exists(directory))
            {
                _storage.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(configuration, JsonOptions);
            _storage.Write(_configFilePath, System.Text.Encoding.UTF8.GetBytes(json));
        }
    }

    public async Task SaveConfigurationAsync<T>(T configuration, CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? directory = Path.GetDirectoryName(_configFilePath);
        if (directory is not null && !_storage.Exists(directory))
        {
            _storage.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(configuration, JsonOptions);
        await _storage.WriteAllTextAsync(_configFilePath, json, ct);
    }

    public bool HasConfiguration()
    {
        return _storage.Exists(_configFilePath);
    }

    public void DeleteConfiguration()
    {
        lock (_lock)
        {
            if (_storage.Exists(_configFilePath))
            {
                _storage.Delete(_configFilePath);
            }
        }
    }
}
