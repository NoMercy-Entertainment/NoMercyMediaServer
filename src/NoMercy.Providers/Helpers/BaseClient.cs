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

using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Providers.Helpers;

public class BaseClient : IDisposable
{
    protected Guid Id { get; set; }

    protected readonly HttpClient Client;

    protected virtual Uri BaseUrl => new(uriString: "http://localhost:8080");
    protected virtual int ConcurrentRequests => 1;
    protected virtual int Interval => 1000;
    protected virtual Dictionary<string, string?> QueryParams => new();
    protected virtual string UserAgent => ExternalServicesConfig.Current.UserAgent;

    protected BaseClient()
    {
        Client = HttpClientProvider.CreateClient(name: HttpClientNames.General);
        Client.BaseAddress = BaseUrl;
        Client.Timeout = TimeSpan.FromMinutes(minutes: 5);

        foreach ((string? key, string? value) in QueryParams)
            Client.DefaultRequestHeaders.Add(name: key, value: value);
    }

    protected BaseClient(Guid id)
    {
        Id = id;
        Client = HttpClientProvider.CreateClient(name: HttpClientNames.General);
        Client.BaseAddress = BaseUrl;
        Client.Timeout = TimeSpan.FromMinutes(minutes: 5);

        foreach ((string? key, string? value) in QueryParams)
            Client.DefaultRequestHeaders.Add(name: key, value: value);
    }

    private static Queue? _queue;

    protected static Queue Queue()
    {
        return _queue ??= new(
            options: new()
            {
                Concurrent = 1,
                Interval = 1000,
                Start = true,
            }
        );
    }

    protected virtual async Task<T?> Get<T>(
        string url,
        Dictionary<string, string?>? query,
        bool? priority = false
    )
        where T : class
    {
        query ??= new();

        foreach (KeyValuePair<string, string?> queryParam in QueryParams)
            query.Add(key: queryParam.Key, value: queryParam.Value);

        string newUrl = QueryHelpers.AddQueryString(uri: url, queryString: query);

        (bool found, T? result) = await CacheController.ReadAsync<T>(url: newUrl);
        if (found)
            return result;

        Logger.Http(message: newUrl, level: LogEventLevel.Verbose);

        string response = await Queue()
            .Enqueue(task: () => Client.GetStringAsync(requestUri: newUrl), url: newUrl, priority: priority);

        await CacheController.Write(url: newUrl, data: response);

        T? data = response.FromJson<T>();

        return data;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(obj: this);
    }
}
