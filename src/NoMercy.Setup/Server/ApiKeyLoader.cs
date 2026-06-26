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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using NoMercy.Storage;
using Serilog.Events;
using Config = NoMercy.NmSystem.Information.Config;

namespace NoMercy.Setup.Server;

public class ApiKeyLoader : IApiKeyLoader
{
    private readonly ILogger<ApiKeyLoader> _logger;
    private readonly IApiKeyStore _apiKeyStore;
    private readonly IStorageDriver _storageDriver;
    private static readonly int[] BackoffSeconds = [30, 60, 300, 900, 1800];

    public ApiKeyLoader(ILogger<ApiKeyLoader> logger, IApiKeyStore apiKeyStore, IStorageDriver storageDriver)
    {
        _logger = logger;
        _apiKeyStore = apiKeyStore;
        _storageDriver = storageDriver;
    }

    public async Task LoadKeys(CancellationToken ct = default)
    {
        // 1. Try network first
        ApiInfoResponse? liveData = await TryFetchFromNetwork();

        if (liveData is not null)
        {
            ApplyKeys(liveData);
            await WriteCacheFile(liveData);
            _logger.LogInformation("API keys loaded from network");
            return;
        }

        // 2. Network failed — try cache
        ApiInfoResponse? cachedData = await TryReadCacheFile();

        if (cachedData is not null)
        {
            ApplyKeys(cachedData);
            string cachedAt = cachedData.CachedAt ?? "unknown";

            DateTime? cachedAtDate = cachedData.CachedAt is not null
                ? DateTime.TryParse(cachedData.CachedAt, out DateTime parsed)
                    ? parsed
                    : null
                : null;

            if (cachedAtDate.HasValue && (DateTime.UtcNow - cachedAtDate.Value).TotalDays > 30)
            {
                _logger.LogWarning("API keys loaded from cache (cached at {CachedAt}) — cache is over 30 days old", cachedAt);
            }
            else
            {
                _logger.LogInformation("API keys loaded from cache (cached at {CachedAt})", cachedAt);
            }

            StartBackgroundRefresh(ct);
            return;
        }

        // 3. No network, no cache — cannot function without keys
        _logger.LogError("API unreachable and no cached keys available — provider features will be unavailable");
    }

    private async Task<ApiInfoResponse?> TryFetchFromNetwork()
    {
        try
        {
            _logger.LogInformation("Requesting server info");

            GenericHttpClient apiClient = new(Config.ApiBaseUrl);
            apiClient.SetDefaultHeaders(Config.UserAgent, Globals.Globals.AccessToken);

            string content = await apiClient.SendAndReadAsync(HttpMethod.Get, "v1/info");

            ApiInfoResponse? data = content.FromJson<ApiInfoResponse>();
            if (data?.Data.Keys is null)
                return null;

            if (string.IsNullOrEmpty(data.Data.Keys.TmdbToken))
            {
                _logger.LogWarning("API keys response contained empty keys — auth token may be expired, discarding response");
                return null;
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch API keys from network");
            return null;
        }
    }

    private void ApplyKeys(ApiInfoResponse data)
    {
        if (_apiKeyStore is ApiKeyStore store)
        {
            store.MakeMkvKey = data.Data.Keys.MakeMkvKey;
            store.TmdbKey = data.Data.Keys.TmdbKey;
            store.OmdbKey = data.Data.Keys.OmdbKey;
            store.FanArtApiKey = data.Data.Keys.FanArtKey;
            store.RottenTomatoes = data.Data.Keys.RottenTomatoes;
            store.AcousticIdKey = data.Data.Keys.AcousticIdKey;
            store.TadbKey = data.Data.Keys.TadbKey;
            store.TmdbToken = data.Data.Keys.TmdbToken;
            store.TvdbKey = data.Data.Keys.TvdbKey;
            store.MusixmatchKey = data.Data.Keys.MusixmatchKey;
            store.JwplayerKey = data.Data.Keys.JwplayerKey;
            store.KeysLoaded = true;
        }
    }

    private async Task WriteCacheFile(ApiInfoResponse data)
    {
        try
        {
            data.CachedAt = DateTime.UtcNow.ToString("O");
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            await using Stream stream = _storageDriver.OpenWrite(AppFiles.ApiKeysFile, overwrite: true);
            await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            await writer.WriteAsync(json);
            await writer.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write API keys cache");
        }
    }

    private async Task<ApiInfoResponse?> TryReadCacheFile()
    {
        try
        {
            if (!_storageDriver.FileExists(AppFiles.ApiKeysFile))
                return null;

            string json;
            using (StreamReader reader = new(_storageDriver.OpenRead(AppFiles.ApiKeysFile)))
                json = await reader.ReadToEndAsync();
            
            if (string.IsNullOrWhiteSpace(json))
                return null;

            ApiInfoResponse? data = json.FromJson<ApiInfoResponse>();
            if (data?.Data.Keys is null)
                return null;

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read API keys cache");
            return null;
        }
    }

    private void StartBackgroundRefresh(CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            int attempt = 0;

            while (!ct.IsCancellationRequested)
            {
                int delay = BackoffSeconds[Math.Min(attempt, BackoffSeconds.Length - 1)];
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                ApiInfoResponse? fresh = await TryFetchFromNetwork();
                if (fresh is not null)
                {
                    ApplyKeys(fresh);
                    await WriteCacheFile(fresh);
                    _logger.LogInformation("API keys refreshed from network");
                    return;
                }

                attempt++;
                _logger.LogWarning("API key refresh attempt {Attempt} failed, retrying in {Delay}s", attempt, delay);
            }
        }, ct);
    }
}
