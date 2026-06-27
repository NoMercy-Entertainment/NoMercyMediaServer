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
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using Serilog.Events;
using HttpClient = System.Net.Http.HttpClient;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://musicbrainz.org/ws/2/");

    private readonly HttpClient _client;

    protected MusicBrainzBaseClient()
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.MusicBrainz);
        _client.BaseAddress ??= _baseUrl;
        EnsureUserAgent(_client);
    }

    protected MusicBrainzBaseClient(Guid id)
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.MusicBrainz);
        _client.BaseAddress ??= _baseUrl;
        EnsureUserAgent(_client);
        Id = id;
    }

    // MusicBrainz refuses anonymous UAs with a 403. The factory-registered UA
    // only takes effect once HttpClientProvider has been initialized, which
    // happens after the web app is built. Early seed callers fall back to
    // `new HttpClient()` with no headers, so apply the UA defensively here.
    private static void EnsureUserAgent(HttpClient client)
    {
        if (client.DefaultRequestHeaders.UserAgent.Count > 0)
            return;
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", ExternalServicesConfig.Current.UserAgent);
    }

    private static Queue? _queue;

    private static Queue GetQueue()
    {
        return _queue ??= new(
            new()
            {
                Concurrent = 1,
                Interval = 1100,
                Start = true,
            }
        );
    }

    protected Guid Id { get; private set; }

    protected async Task<T?> Get<T>(
        string url,
        Dictionary<string, string>? query = null,
        bool? priority = false,
        int retry = 0
    )
        where T : class
    {
        query ??= new();

        string newUrl = url.ToQueryUri(query);

        if (CacheController.Read(newUrl, out T? result))
            return result;

        Logger.MusicBrainz(_baseUrl + newUrl, LogEventLevel.Verbose);

        try
        {
            using HttpResponseMessage httpResponse = await GetQueue()
                .Enqueue(() => _client.GetAsync(newUrl), newUrl, priority);

            if (!httpResponse.IsSuccessStatusCode)
            {
                string body = await httpResponse.Content.ReadAsStringAsync();
                string truncated = body.Length > 500 ? body[..500] : body;
                Logger.App(
                    $"MusicBrainz non-success {(int)httpResponse.StatusCode} ({newUrl}): {truncated}",
                    LogEventLevel.Debug
                );

                int statusCode = (int)httpResponse.StatusCode;

                if (statusCode == 403 && retry < 3)
                {
                    int delay = (retry + 1) * 30_000;
                    Logger.App(
                        $"MusicBrainz 403 ({newUrl}), retrying in {delay / 1000}s (attempt {retry + 1}/3)",
                        LogEventLevel.Debug
                    );
                    await Task.Delay(delay);
                    return await Get<T>(url, query, priority, retry + 1);
                }

                if ((statusCode == 429 || statusCode == 503) && retry < 10)
                {
                    int delay = (int)Math.Pow(2, retry + 1) * 1000;
                    Logger.App(
                        $"MusicBrainz {statusCode} ({newUrl}), retrying in {delay / 1000}s (attempt {retry + 1}/10)",
                        LogEventLevel.Debug
                    );
                    await Task.Delay(delay);
                    return await Get<T>(url, query, priority, retry + 1);
                }

                httpResponse.EnsureSuccessStatusCode();
            }

            string response = await httpResponse.Content.ReadAsStringAsync();
            await CacheController.Write(newUrl, response);

            return response.FromJson<T>();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (HttpRequestException ex)
            when (retry < 10
                && ex.StatusCode
                    is HttpStatusCode.TooManyRequests
                        or HttpStatusCode.ServiceUnavailable
            )
        {
            int delay = (int)Math.Pow(2, retry + 1) * 1000;
            Logger.App(
                $"MusicBrainz {ex.StatusCode} ({newUrl}), retrying in {delay / 1000}s (attempt {retry + 1}/10)",
                LogEventLevel.Debug
            );
            await Task.Delay(delay);
            return await Get<T>(url, query, priority, retry + 1);
        }
        catch (TaskCanceledException) when (retry < 10)
        {
            int delay = (int)Math.Pow(2, retry + 1) * 1000;
            await Task.Delay(delay);
            return await Get<T>(url, query, priority, retry + 1);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
