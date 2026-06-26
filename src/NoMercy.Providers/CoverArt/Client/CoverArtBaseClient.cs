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
using Serilog.Events;

namespace NoMercy.Providers.CoverArt.Client;

public class CoverArtBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://coverartarchive.org/");

    private readonly HttpClient _client;

    protected CoverArtBaseClient()
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.CoverArt);
        _client.BaseAddress ??= _baseUrl;
    }

    protected CoverArtBaseClient(Guid id)
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.CoverArt);
        _client.BaseAddress ??= _baseUrl;
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

    protected Guid Id { get; private set; }

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
            // CoverArtArchive 404s when no front cover exists — soft-fail.
            return null;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
