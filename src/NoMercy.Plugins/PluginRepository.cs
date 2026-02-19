using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Plugins;

public class PluginRepository : IPluginRepository
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly string _repositoriesFilePath;
    private readonly List<PluginRepositoryInfo> _repositories = [];
    private readonly List<PluginRepositoryEntry> _availablePlugins = [];
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public PluginRepository(HttpClient httpClient, ILogger logger, string pluginsPath)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsPath);

        string configDir = Path.Combine(pluginsPath, "configurations");
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        _repositoriesFilePath = Path.Combine(configDir, "repositories.json");
        LoadRepositoriesFromDisk();
    }

    public IReadOnlyList<PluginRepositoryInfo> GetRepositories()
    {
        lock (_lock)
        {
            return _repositories.ToList().AsReadOnly();
        }
    }

    public async Task AddRepositoryAsync(string name, string url, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        lock (_lock)
        {
            if (_repositories.Any(r => r.Name == name))
            {
                throw new InvalidOperationException($"Repository '{name}' already exists.");
            }

            _repositories.Add(new()
            {
                Name = name,
                Url = url,
                Enabled = true
            });
        }

        SaveRepositoriesToDisk();
        await RefreshRepositoryAsync(name, url, ct);
    }

    public Task RemoveRepositoryAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_lock)
        {
            int removed = _repositories.RemoveAll(r => r.Name == name);
            if (removed == 0)
            {
                throw new InvalidOperationException($"Repository '{name}' not found.");
            }
        }

        SaveRepositoriesToDisk();
        return Task.CompletedTask;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        List<PluginRepositoryInfo> repos;
        lock (_lock)
        {
            repos = _repositories.Where(r => r.Enabled).ToList();
        }

        List<PluginRepositoryEntry> allPlugins = [];

        foreach (PluginRepositoryInfo repo in repos)
        {
            try
            {
                List<PluginRepositoryEntry> plugins = await FetchRepositoryPluginsAsync(repo.Url, ct);
                allPlugins.AddRange(plugins);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to refresh repository '{Name}' ({Url}): {Error}", repo.Name, repo.Url, ex.Message);
            }
        }

        lock (_lock)
        {
            _availablePlugins.Clear();
            _availablePlugins.AddRange(allPlugins);
        }
    }

    public IReadOnlyList<PluginRepositoryEntry> GetAvailablePlugins()
    {
        lock (_lock)
        {
            return _availablePlugins.ToList().AsReadOnly();
        }
    }

    public PluginRepositoryEntry? FindPlugin(Guid pluginId)
    {
        lock (_lock)
        {
            return _availablePlugins.FirstOrDefault(p => p.Id == pluginId);
        }
    }

    public PluginVersionEntry? FindVersion(Guid pluginId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        lock (_lock)
        {
            PluginRepositoryEntry? plugin = _availablePlugins.FirstOrDefault(p => p.Id == pluginId);
            return plugin?.Versions.FirstOrDefault(v => v.Version == version);
        }
    }

    internal async Task<List<PluginRepositoryEntry>> FetchRepositoryPluginsAsync(string url, CancellationToken ct = default)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct);
        PluginRepositoryManifest? manifest = JsonSerializer.Deserialize<PluginRepositoryManifest>(json, JsonOptions);

        if (manifest is null)
        {
            return [];
        }

        return manifest.Plugins;
    }

    private async Task RefreshRepositoryAsync(string name, string url, CancellationToken ct)
    {
        try
        {
            List<PluginRepositoryEntry> plugins = await FetchRepositoryPluginsAsync(url, ct);

            lock (_lock)
            {
                _availablePlugins.AddRange(plugins);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to fetch repository '{Name}' ({Url}): {Error}", name, url, ex.Message);
        }
    }

    private void LoadRepositoriesFromDisk()
    {
        if (!File.Exists(_repositoriesFilePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(_repositoriesFilePath);
            List<PluginRepositoryInfo>? repos = JsonSerializer.Deserialize<List<PluginRepositoryInfo>>(json, JsonOptions);
            if (repos is not null)
            {
                _repositories.AddRange(repos);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to load repositories from {Path}: {Error}", _repositoriesFilePath, ex.Message);
        }
    }

    private void SaveRepositoriesToDisk()
    {
        try
        {
            string json = JsonSerializer.Serialize(_repositories, JsonOptions);
            File.WriteAllText(_repositoriesFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to save repositories to {Path}: {Error}", _repositoriesFilePath, ex.Message);
        }
    }
}
