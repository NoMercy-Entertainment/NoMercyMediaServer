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
/// Shared base for the HTTP provider clients (TMDB, FanArt, CoverArt, Lrclib,
/// MusixMatch, …). Captures the boilerplate that was duplicated across every
/// provider base class: a named <see cref="HttpClient"/> from the factory, a
/// per-provider rate-limited <see cref="Queue"/>, dev-only on-disk caching via
/// <see cref="CacheController"/>, soft-fail on configurable error statuses,
/// optional retry-with-delay, and IDisposable.
///
/// Concrete providers supply <see cref="HttpClientName"/>, <see cref="BaseUrl"/>
/// and (optionally) their concurrency/interval, and may override
/// <see cref="ConfigureClient"/> for auth headers, <see cref="LogRequest"/> for
/// their provider-specific log channel, <see cref="AugmentQuery"/> to inject
/// fixed/secret query parameters, <see cref="ShouldSoftFail"/> to tune which
/// error statuses resolve to null, and <see cref="MaxRetries"/>/
/// <see cref="RetryDelay"/> to opt into retrying transient failures.
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

    // Retry policy. Default: no retries (a single attempt). Providers that need
    // resilience against transient failures opt in by overriding MaxRetries.
    protected virtual int MaxRetries => 0;
    protected virtual TimeSpan RetryDelay => TimeSpan.FromSeconds(5);

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

    /// <summary>
    /// Per-provider hook to inject query parameters (api keys, fixed format
    /// flags, …) into a private copy of the caller's query before the URL is
    /// built. Default: no-op. The dictionary is already a copy, so
    /// implementations may mutate it freely.
    /// </summary>
    protected virtual void AugmentQuery(Dictionary<string, string?> query) { }

    /// <summary>
    /// Whether an HTTP error status should resolve to "no result" (null) rather
    /// than be retried/thrown. Default: 404 and 400.
    /// </summary>
    protected virtual bool ShouldSoftFail(HttpStatusCode? status) =>
        status is HttpStatusCode.NotFound or HttpStatusCode.BadRequest;

    /// <summary>Hook invoked just before a soft-fail resolves to null. Default: no-op.</summary>
    protected virtual void OnSoftFail(HttpStatusCode? status, string url) { }

    /// <summary>Hook invoked before each retry attempt. Default: no-op.</summary>
    protected virtual void OnRetry(HttpStatusCode? status, int attempt) { }

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
        // Copy so AugmentQuery / our own additions never mutate the caller's dict.
        Dictionary<string, string?> effectiveQuery = query is null ? new() : new(query);
        AugmentQuery(effectiveQuery);
        string newUrl = QueryHelpers.AddQueryString(url, effectiveQuery);

        if (CacheController.Read(newUrl, out T? result))
            return result;

        LogRequest(BaseUrl + newUrl);

        int attempt = 0;
        while (true)
        {
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
            catch (HttpRequestException ex) when (ShouldSoftFail(ex.StatusCode))
            {
                // Provider signalled "not found" — soft-fail to null.
                OnSoftFail(ex.StatusCode, newUrl);
                return null;
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                OnRetry(ex.StatusCode, ++attempt);
                await Task.Delay(RetryDelay);
            }
            catch (TaskCanceledException) when (attempt < MaxRetries)
            {
                OnRetry(null, ++attempt);
                await Task.Delay(RetryDelay);
            }
        }
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
