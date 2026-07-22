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
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Abstractions;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Models.Networks;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Setup.Server;
using Serilog.Events;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbBaseClient : ExternalApiClient
{
    private readonly string Language;

    protected TmdbBaseClient()
    {
        Language = "en,null";
    }

    protected TmdbBaseClient(int id, string language = "en-US")
    {
        Id = id;
        Language = language + ",null";
    }

    // TMDB identifies entities with integer ids, unlike the Guid-based default.
    public new int Id { get; private set; }

    protected override string HttpClientName => HttpClientNames.Tmdb;
    protected override Uri BaseUrl => new(uriString: "https://api.themoviedb.org/3/");
    protected override int ConcurrentRequests => 50;

    protected override void ConfigureClient(HttpClient client) =>
        client.DefaultRequestHeaders.Add(
            name: "Authorization",
            value: $"Bearer {ApiKeyStore.Current.TmdbToken}"
        );

    protected override void LogRequest(string url) => Logger.MovieDb(message: url, level: LogEventLevel.Verbose);

    private static int Max(int available, int wanted, int constraint)
    {
        return wanted < available
            ? wanted > constraint
                ? constraint
                : wanted
            : available;
    }

    protected override async Task<T?> Get<T>(
        string url,
        Dictionary<string, string?>? query = null,
        bool? priority = false,
        bool skipCache = false
    )
        where T : class
    {
        query ??= new();
        if (!query.ContainsKey(key: "language"))
            query[key: "language"] = priority is true ? Language : "";
        query[key: "include_adult"] = RuntimeServerSettings.Current.ShowAdultContent ? "true" : "false";

        string newUrl = QueryHelpers.AddQueryString(uri: url, queryString: query);

        if (!skipCache)
        {
            (bool found, T? result) = await CacheController.ReadAsync<T>(url: newUrl);
            if (found)
                return result;
        }

        LogRequest(url: BaseUrl + newUrl);

        try
        {
            string response = await RequestQueue.Enqueue(
                task: () =>
                {
                    if (Disposed)
                        throw new ObjectDisposedException(
                            objectName: nameof(TmdbBaseClient),
                            message: "Cannot access a disposed TMDB client."
                        );
                    return Client.GetStringAsync(requestUri: newUrl);
                },
                url: newUrl,
                priority: priority
            );

            if (!skipCache)
                await CacheController.Write(url: newUrl, data: response);
            return response.FromJson<T>();
        }
        catch (ObjectDisposedException)
        {
            Logger.MovieDb(
                message: $"TMDB client disposed during operation for {newUrl}",
                level: LogEventLevel.Debug
            );
            return null;
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode
                    is HttpStatusCode.NotFound
                        or HttpStatusCode.UnprocessableEntity
                        or HttpStatusCode.BadRequest
                        or HttpStatusCode.Forbidden
                        or HttpStatusCode.TooManyRequests
                        or HttpStatusCode.ServiceUnavailable
            )
        {
            Logger.MovieDb(message: $"HTTP {ex.StatusCode} for {newUrl}", level: LogEventLevel.Debug);
            return null;
        }
        catch (HttpRequestException ex)
        {
            // Any other HTTP failure — an unexpected status, or a transport-level
            // error with no status code (connection reset / timeout under load).
            // Degrade to a null (cache-miss-style) result rather than throwing so
            // callers — and the *HandlesGracefully contract — never see an exception.
            Logger.MovieDb(
                message: $"TMDB HTTP request failed for {newUrl}: {ex.Message}",
                level: LogEventLevel.Warning
            );
            return null;
        }
        catch (Exception ex)
            when (ex is Newtonsoft.Json.JsonException or OperationCanceledException)
        {
            Logger.MovieDb(
                message: $"TMDB response could not be processed for {newUrl}: {ex.Message}",
                level: LogEventLevel.Warning
            );
            return null;
        }
    }

    protected async Task<List<T>?> Paginated<T>(string url, int limit)
        where T : class
    {
        List<T> list = [];
        TmdbPaginatedResponse<T>? firstPage = await Get<TmdbPaginatedResponse<T>>(url: url);
        list.AddRange(collection: firstPage?.Results ?? []);
        if (limit > 1)
            await Parallel.ForAsync(
                fromInclusive: 2,
                toExclusive: Max(available: firstPage?.TotalPages ?? 0, wanted: limit, constraint: 500) + 1,
                body: async (i, _) =>
                {
                    TmdbPaginatedResponse<T>? page = await Get<TmdbPaginatedResponse<T>>(
                        url: url,
                        query: new() { [key: "page"] = i.ToString() }
                    );
                    lock (list)
                    {
                        list.AddRange(collection: page?.Results ?? []);
                    }
                }
            );
        return list;
    }

    public Task<TmdbTmdbNetworkDetails?> CompanyDetails(int id, bool? priority = false)
    {
        return Get<TmdbTmdbNetworkDetails>(url: "company/" + id, priority: priority);
    }
}
