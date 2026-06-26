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
using NoMercy.Providers.AcoustId.Models;
using NoMercy.Providers.Helpers;
using Serilog.Events;

namespace NoMercy.Providers.AcoustId.Client;

public class AcoustIdBaseClient : IDisposable
{
    private const int MaxRetries = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly Uri _baseUrl = new("https://api.acoustid.org/v2/");
    private readonly HttpClient _client;

    protected AcoustIdBaseClient()
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.AcoustId);
        _client.BaseAddress ??= _baseUrl;
    }

    protected AcoustIdBaseClient(Guid id)
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.AcoustId);
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
        Dictionary<string, string?>? query = default,
        bool? priority = false,
        int retry = 0
    )
        where T : AcoustIdFingerprint
    {
        query ??= new();
        string newUrl = QueryHelpers.AddQueryString(url, query);

        if (CacheController.Read(newUrl, out T? cached) && HasRecordings(cached))
            return cached;

        Logger.AcoustId(newUrl, LogEventLevel.Verbose);

        try
        {
            string response = await GetQueue()
                .Enqueue(() => _client.GetStringAsync(newUrl), newUrl, priority);
            await CacheController.Write(newUrl, response);

            T? data = response.FromJson<T>();
            return HasRecordings(data) ? data : null;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (HttpRequestException ex) when (retry < MaxRetries)
        {
            Logger.AcoustId(
                $"AcoustId {ex.StatusCode} retry {retry + 1}/{MaxRetries} for {newUrl}",
                LogEventLevel.Debug
            );
            await Task.Delay(RetryDelay);
            return await Get<T>(url, query, priority, retry + 1);
        }
        catch (TaskCanceledException) when (retry < MaxRetries)
        {
            await Task.Delay(RetryDelay);
            return await Get<T>(url, query, priority, retry + 1);
        }
    }

    private static bool HasRecordings<T>(T? data)
        where T : AcoustIdFingerprint
    {
        return data?.Results.Length > 0
            && data.Results.Any(fpResult =>
                fpResult.Recordings is not null
                && fpResult.Recordings.Any(recording => recording?.Title != null)
            );
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
