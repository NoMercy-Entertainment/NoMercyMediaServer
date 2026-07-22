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

    // SemaphoreSlim, not a plain lock object: the Async members below need to
    // hold this across an await, which a `lock` statement cannot do. The sync
    // and async members previously used DIFFERENT guards (this lock vs none
    // at all on the Async methods), so a sync Save/Get racing an async one
    // could interleave a read with a concurrent write.
    private readonly SemaphoreSlim _lock = new(initialCount: 1, maxCount: 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public PluginConfiguration(string dataFolderPath, IStorage storage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: dataFolderPath);
        ArgumentNullException.ThrowIfNull(argument: storage);
        _configFilePath = Path.Combine(path1: dataFolderPath, path2: "config.json");
        _storage = storage;
    }

    public T? GetConfiguration<T>()
        where T : class, new()
    {
        _lock.Wait();
        try
        {
            if (!_storage.Exists(path: _configFilePath))
            {
                return null;
            }

            byte[] bytes = _storage.Read(path: _configFilePath);
            string json = Encoding.UTF8.GetString(bytes: bytes);
            return TryDeserialize<T>(json: json);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T?> GetConfigurationAsync<T>(CancellationToken ct = default)
        where T : class, new()
    {
        await _lock.WaitAsync(cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);
        try
        {
            if (!_storage.Exists(path: _configFilePath))
            {
                return null;
            }

            string json = await _storage.ReadAllTextAsync(path: _configFilePath, ct: ct);
            return TryDeserialize<T>(json: json);
        }
        finally
        {
            _lock.Release();
        }
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
            return JsonSerializer.Deserialize<T>(json: json, options: JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void SaveConfiguration<T>(T configuration)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(argument: configuration);

        _lock.Wait();
        try
        {
            string? directory = Path.GetDirectoryName(path: _configFilePath);
            if (directory is not null && !_storage.Exists(path: directory))
            {
                _storage.CreateDirectory(path: directory);
            }

            string json = JsonSerializer.Serialize(value: configuration, options: JsonOptions);
            _storage.Write(path: _configFilePath, bytes: Encoding.UTF8.GetBytes(s: json));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveConfigurationAsync<T>(T configuration, CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(argument: configuration);

        await _lock.WaitAsync(cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);
        try
        {
            string? directory = Path.GetDirectoryName(path: _configFilePath);
            if (directory is not null && !_storage.Exists(path: directory))
            {
                _storage.CreateDirectory(path: directory);
            }

            string json = JsonSerializer.Serialize(value: configuration, options: JsonOptions);
            await _storage.WriteAllTextAsync(path: _configFilePath, contents: json, ct: ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public bool HasConfiguration()
    {
        return _storage.Exists(path: _configFilePath);
    }

    public void DeleteConfiguration()
    {
        _lock.Wait();
        try
        {
            if (_storage.Exists(path: _configFilePath))
            {
                _storage.Delete(path: _configFilePath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}
