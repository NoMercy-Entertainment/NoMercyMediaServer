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

using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using Serilog.Events;

namespace NoMercy.Providers.Abstractions;

/// <summary>
/// Shared base for the HTTP provider clients (TMDB, FanArt, CoverArt, …).
/// Captures the boilerplate that was duplicated across every provider base
/// class: a named <see cref="HttpClient"/> from the factory, a per-provider
/// rate-limited <see cref="Queue"/>, dev-only on-disk caching via
/// <see cref="CacheController"/>, soft-fail on 404/400, and IDisposable.
///
/// Concrete providers supply <see cref="HttpClientName"/>, <see cref="BaseUrl"/>
/// and (optionally) their concurrency/interval, and may override
/// <see cref="ConfigureClient"/> for auth headers and <see cref="LogRequest"/>
/// for their provider-specific log channel.
/// </summary>
public abstract class ExternalApiClient : IDisposable
{
    // One queue per concrete provider type, shared across all instances of that
    // type, so the provider-wide concurrency/interval limit is preserved (the
    // old per-provider 'static Queue' semantics).
    private static readonly ConcurrentDictionary<Type, Queue> Queues = new();

    protected Guid Id { get; private set; }
    protected readonly HttpClient Client;

    protected abstract string HttpClientName { get; }
    protected abstract Uri BaseUrl { get; }
    protected virtual int ConcurrentRequests => 1;
    protected virtual int RequestIntervalMs => 1000;

    protected ExternalApiClient()
    {
        Client = HttpClientProvider.CreateClient(HttpClientName);
        Client.BaseAddress ??= BaseUrl;
        ConfigureClient(Client);
    }

    protected ExternalApiClient(Guid id)
        : this()
    {
        Id = id;
    }

    /// <summary>Per-provider hook to add default headers / auth. Default: no-op.</summary>
    protected virtual void ConfigureClient(HttpClient client) { }

    /// <summary>Per-provider verbose request log. Default: the generic HTTP channel.</summary>
    protected virtual void LogRequest(string url) => Logger.Http(url, LogEventLevel.Verbose);

    protected Queue RequestQueue =>
        Queues.GetOrAdd(
            GetType(),
            _ =>
                new(
                    new()
                    {
                        Concurrent = ConcurrentRequests,
                        Interval = RequestIntervalMs,
                        Start = true,
                    }
                )
        );

    protected async Task<T?> Get<T>(
        string url,
        Dictionary<string, string?>? query = null,
        bool? priority = false
    )
        where T : class
    {
        query ??= new();
        string newUrl = QueryHelpers.AddQueryString(url, query);

        if (CacheController.Read(newUrl, out T? result))
            return result;

        LogRequest(BaseUrl + newUrl);

        try
        {
            string response = await RequestQueue.Enqueue(
                () => Client.GetStringAsync(newUrl),
                newUrl,
                priority
            );

            await CacheController.Write(newUrl, response);
            return response.FromJson<T>();
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            // Provider returned 404/400 — treat as "not found", soft-fail to null.
            return null;
        }
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
