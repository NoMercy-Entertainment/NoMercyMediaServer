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

using System.Text;
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

    public PluginConfiguration(string dataFolderPath, IStorage storage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFolderPath);
        ArgumentNullException.ThrowIfNull(storage);
        _configFilePath = Path.Combine(dataFolderPath, "config.json");
        _storage = storage;
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
            string json = Encoding.UTF8.GetString(bytes);
            return TryDeserialize<T>(json);
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
        return TryDeserialize<T>(json);
    }

    private static T? TryDeserialize<T>(string json)
        where T : class, new()
    {
        // Plugin config files can drift to malformed JSON across upgrades or
        // crashes mid-write. Treating that as 'no config' lets the plugin
        // re-initialise with defaults instead of taking the load path down
        // with a JsonException.
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
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
            _storage.Write(_configFilePath, Encoding.UTF8.GetBytes(json));
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
