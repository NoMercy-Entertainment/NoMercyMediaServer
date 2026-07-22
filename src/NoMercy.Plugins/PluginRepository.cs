// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoMercy.Plugins.Abstractions;
using NoMercy.Storage;

namespace NoMercy.Plugins;

public class PluginRepository : IPluginRepository
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly string _repositoriesFilePath;
    private readonly IStorage _storage;
    private readonly List<PluginRepositoryInfo> _repositories = [];
    private readonly List<PluginRepositoryEntry> _availablePlugins = [];
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public PluginRepository(
        HttpClient httpClient,
        ILogger logger,
        string pluginsPath,
        IStorage storage
    )
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(paramName: nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(paramName: nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: pluginsPath);
        _storage = storage ?? throw new ArgumentNullException(paramName: nameof(storage));

        string configDir = Path.Combine(path1: pluginsPath, path2: "configurations");

        if (!_storage.Exists(path: configDir))
        {
            _storage.CreateDirectory(path: configDir);
        }

        _repositoriesFilePath = Path.Combine(path1: configDir, path2: "repositories.json");
    }

    /// <summary>
    /// Creates a repository and asynchronously loads any persisted repository
    /// list from disk. Prefer this over the constructor: the previous ctor read
    /// the file sync-over-async (.GetAwaiter().GetResult()), which can deadlock
    /// during DI resolution under a synchronization context.
    /// </summary>
    public static async Task<PluginRepository> CreateAsync(
        HttpClient httpClient,
        ILogger logger,
        string pluginsPath,
        IStorage storage,
        CancellationToken ct = default
    )
    {
        PluginRepository repository = new(httpClient: httpClient, logger: logger, pluginsPath: pluginsPath, storage: storage);
        await repository.LoadRepositoriesFromDiskAsync(ct: ct);
        return repository;
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
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: name);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: url);

        lock (_lock)
        {
            if (_repositories.Any(predicate: r => r.Name == name))
            {
                throw new InvalidOperationException(message: $"Repository '{name}' already exists.");
            }

            _repositories.Add(
                item: new()
                {
                    Name = name,
                    Url = url,
                    Enabled = true,
                }
            );
        }

        await SaveRepositoriesToDiskAsync(ct: ct);
        await RefreshRepositoryAsync(name: name, url: url, ct: ct);
    }

    public async Task RemoveRepositoryAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: name);

        lock (_lock)
        {
            int removed = _repositories.RemoveAll(match: r => r.Name == name);
            if (removed == 0)
            {
                throw new InvalidOperationException(message: $"Repository '{name}' not found.");
            }
        }

        await SaveRepositoriesToDiskAsync(ct: ct);
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        List<PluginRepositoryInfo> repos;
        lock (_lock)
        {
            repos = _repositories.Where(predicate: r => r.Enabled).ToList();
        }

        List<PluginRepositoryEntry> allPlugins = [];

        foreach (PluginRepositoryInfo repo in repos)
        {
            try
            {
                List<PluginRepositoryEntry> plugins = await FetchRepositoryPluginsAsync(
                    url: repo.Url,
                    ct: ct
                );
                allPlugins.AddRange(collection: plugins);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    message: "Failed to refresh repository '{Name}' ({Url}): {Error}", args: [repo.Name, repo.Url, ex.Message]
                );
            }
        }

        lock (_lock)
        {
            _availablePlugins.Clear();
            _availablePlugins.AddRange(collection: allPlugins);
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
            return _availablePlugins.FirstOrDefault(predicate: p => p.Id == pluginId);
        }
    }

    public PluginVersionEntry? FindVersion(Guid pluginId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: version);

        lock (_lock)
        {
            PluginRepositoryEntry? plugin = _availablePlugins.FirstOrDefault(predicate: p => p.Id == pluginId);
            return plugin?.Versions.FirstOrDefault(predicate: v => v.Version == version);
        }
    }

    internal async Task<List<PluginRepositoryEntry>> FetchRepositoryPluginsAsync(
        string url,
        CancellationToken ct = default
    )
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(requestUri: url, cancellationToken: ct);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken: ct);
        PluginRepositoryManifest? manifest = JsonSerializer.Deserialize<PluginRepositoryManifest>(
            json: json,
            options: JsonOptions
        );

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
            List<PluginRepositoryEntry> plugins = await FetchRepositoryPluginsAsync(url: url, ct: ct);

            lock (_lock)
            {
                _availablePlugins.AddRange(collection: plugins);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                message: "Failed to fetch repository '{Name}' ({Url}): {Error}", args: [name, url, ex.Message]
            );
        }
    }

    private async Task LoadRepositoriesFromDiskAsync(CancellationToken ct)
    {
        if (!_storage.Exists(path: _repositoriesFilePath))
        {
            return;
        }

        try
        {
            string json = await _storage.ReadAllTextAsync(path: _repositoriesFilePath, ct: ct);
            List<PluginRepositoryInfo>? repos = JsonSerializer.Deserialize<
                List<PluginRepositoryInfo>
            >(json: json, options: JsonOptions);
            if (repos is not null)
            {
                _repositories.AddRange(collection: repos);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                message: "Failed to load repositories from {Path}: {Error}", args: [_repositoriesFilePath, ex.Message]
            );
        }
    }

    private async Task SaveRepositoriesToDiskAsync(CancellationToken ct)
    {
        try
        {
            // AddRepositoryAsync / RemoveRepositoryAsync mutate _repositories
            // under `lock (_lock)` but call this AFTER releasing it — reading
            // the live list here (as this used to) could race a concurrent
            // mutation and throw "Collection was modified" or serialize a
            // half-updated list. Snapshot under the lock; serialize + write
            // outside it so file I/O never blocks the other lock holders.
            List<PluginRepositoryInfo> snapshot;
            lock (_lock)
            {
                snapshot = _repositories.ToList();
            }

            string json = JsonSerializer.Serialize(value: snapshot, options: JsonOptions);
            await _storage.WriteAllTextAsync(path: _repositoriesFilePath, contents: json, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                message: "Failed to save repositories to {Path}: {Error}", args: [_repositoriesFilePath, ex.Message]
            );
        }
    }
}
