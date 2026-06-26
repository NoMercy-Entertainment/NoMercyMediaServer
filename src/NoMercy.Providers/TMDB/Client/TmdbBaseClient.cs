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
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Models.Networks;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Setup.Server;
using Serilog.Events;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://api.themoviedb.org/3/");
    private readonly string Language;
    private bool _disposed;

    public int Id { get; private set; }

    private readonly HttpClient _client;

    protected TmdbBaseClient()
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.Tmdb);
        _client.BaseAddress ??= _baseUrl;
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKeyStore.Current.TmdbToken}");
        Language = "en,null";
    }

    protected TmdbBaseClient(int id, string language = "en-US")
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.Tmdb);
        _client.BaseAddress ??= _baseUrl;
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKeyStore.Current.TmdbToken}");
        Language = language + ",null";
        Id = id;
    }

    private static Queue? _queue;

    protected static Queue GetQueue()
    {
        return _queue ??= new(
            new()
            {
                Concurrent = 50,
                Interval = 1000,
                Start = true,
            }
        );
    }

    private static int Max(int available, int wanted, int constraint)
    {
        return wanted < available
            ? wanted > constraint
                ? constraint
                : wanted
            : available;
    }

    protected async Task<T?> Get<T>(
        string url,
        Dictionary<string, string?>? query = null,
        bool? priority = false,
        bool skipCache = false
    )
        where T : class
    {
        query ??= new();

        query["language"] = priority is true ? Language : "";

        query["include_adult"] = RuntimeServerSettings.Current.ShowAdultContent ? "true" : "false";

        string newUrl = QueryHelpers.AddQueryString(url, query);

        if (!skipCache && CacheController.Read(newUrl, out T? result))
            return result;

        Logger.MovieDb(_baseUrl + newUrl, LogEventLevel.Verbose);

        try
        {
            string response = await GetQueue()
                .Enqueue(
                    () =>
                    {
                        if (_disposed)
                        {
                            throw new ObjectDisposedException(
                                nameof(TmdbBaseClient),
                                "Cannot access a disposed TMDB client."
                            );
                        }
                        return _client.GetStringAsync(newUrl);
                    },
                    newUrl,
                    priority
                );

            if (!skipCache)
                await CacheController.Write(newUrl, response);

            T? data = response.FromJson<T>();

            return data;
        }
        catch (ObjectDisposedException)
        {
            // If the client is disposed, return null gracefully
            Logger.MovieDb(
                $"TMDB client disposed during operation for {newUrl}",
                LogEventLevel.Debug
            );
            return null;
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode
                    is HttpStatusCode.NotFound
                        or HttpStatusCode.UnprocessableEntity
                        or HttpStatusCode.BadRequest
            )
        {
            // Soft-fail on TMDB "no data" status codes via StatusCode (not
            // ex.Message.Contains) — message-matching false-positives on URLs
            // that contain "404" etc. as path segments.
            Logger.MovieDb($"HTTP {ex.StatusCode} for {newUrl}", LogEventLevel.Debug);
            return null;
        }
    }

    protected async Task<List<T>?> Paginated<T>(string url, int limit)
        where T : class
    {
        List<T> list = [];

        TmdbPaginatedResponse<T>? firstPage = await Get<TmdbPaginatedResponse<T>>(url);
        list.AddRange(firstPage?.Results ?? []);

        if (limit > 1)
            await Parallel.ForAsync(
                2,
                Max(firstPage?.TotalPages ?? 0, limit, 500),
                async (i, _) =>
                {
                    TmdbPaginatedResponse<T>? page = await Get<TmdbPaginatedResponse<T>>(
                        url,
                        new() { ["page"] = i.ToString() }
                    );
                    lock (list)
                    {
                        list.AddRange(page?.Results ?? []);
                    }
                }
            );

        return list;
    }

    public Task<TmdbTmdbNetworkDetails?> CompanyDetails(int id, bool? priority = false)
    {
        return Get<TmdbTmdbNetworkDetails>("company/" + id, priority: priority);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
