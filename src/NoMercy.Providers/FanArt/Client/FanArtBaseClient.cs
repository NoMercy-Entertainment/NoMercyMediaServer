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

using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Setup.Server;
using Serilog.Events;

namespace NoMercy.Providers.FanArt.Client;

public class FanArtBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("http://webservice.fanart.tv/v3/");

    protected Guid Id { get; private set; }
    private readonly HttpClient _client;

    protected FanArtBaseClient()
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.FanArt);
        _client.BaseAddress ??= _baseUrl;
        _client.DefaultRequestHeaders.Add("api-key", ApiKeyStore.Current.FanArtApiKey);
        if (!string.IsNullOrEmpty(ApiKeyStore.Current.FanArtClientKey))
        {
            _client.DefaultRequestHeaders.Add("client-key", ApiKeyStore.Current.FanArtClientKey);
        }
    }

    protected FanArtBaseClient(Guid id)
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.FanArt);
        _client.BaseAddress ??= _baseUrl;
        _client.DefaultRequestHeaders.Add("api-key", ApiKeyStore.Current.FanArtApiKey);
        if (!string.IsNullOrEmpty(ApiKeyStore.Current.FanArtClientKey))
        {
            _client.DefaultRequestHeaders.Add("client-key", ApiKeyStore.Current.FanArtClientKey);
        }
        Id = id;
    }

    private static Queue? _queue;

    private static Queue GetQueue()
    {
        return _queue ??= new(
            new()
            {
                Concurrent = 3,
                Interval = 1000,
                Start = true,
            }
        );
    }

    protected async Task<T?> Get<T>(
        string url,
        Dictionary<string, string>? query = null,
        bool? priority = false
    )
        where T : class
    {
        query ??= new();

        string newUrl = QueryHelpers.AddQueryString(url, query!);

        if (CacheController.Read(newUrl, out T? result))
            return result;

        Logger.CoverArt(_baseUrl + newUrl, LogEventLevel.Verbose);

        try
        {
            string response = await GetQueue()
                .Enqueue(() => _client.GetStringAsync(newUrl), newUrl, priority);

            await CacheController.Write(newUrl, response);
            return response.FromJson<T>();
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            // FanArt returns 404 for entities with no artwork — soft-fail.
            return null;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
