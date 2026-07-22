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
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Abstractions;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TVDB.Models.Auth;
using NoMercy.Providers.TVDB.Models.Shared;
using NoMercy.Setup.Server;
using Serilog.Events;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbBaseClient : ExternalApiClient
{
    private readonly string _language;

    private static TvdbLoginResponse? Token { get; set; }
    private static readonly SemaphoreSlim TokenLock = new(initialCount: 1, maxCount: 1);

    protected TvdbBaseClient()
    {
        _language = "eng";
    }

    protected TvdbBaseClient(int id, string language = "eng")
    {
        Id = id;
        _language = language;
    }

    // TVDB identifies entities with integer ids, unlike the Guid-based default.
    public new int Id { get; private set; }

    protected override string HttpClientName => HttpClientNames.Tvdb;
    protected override Uri BaseUrl => new(uriString: "https://api4.thetvdb.com/v4/");
    protected override int ConcurrentRequests => 50;

    protected override void LogRequest(string url) => Logger.Tvdb(message: url, level: LogEventLevel.Verbose);

    private static int Max(int available, int wanted, int constraint)
    {
        return wanted < available
            ? wanted > constraint
                ? constraint
                : wanted
            : available;
    }

    private static bool IsTokenValid(TvdbLoginResponse? token)
    {
        if (token is null)
            return false;

        // ExpiresAt is set to ~1 month after login. Refresh 5 min early.
        return token.Data.ExpiresAt >= DateTime.UtcNow.AddMinutes(value: 5);
    }

    private async Task EnsureAuthenticatedAsync()
    {
        if (IsTokenValid(token: Token))
            return;

        await TokenLock.WaitAsync();
        try
        {
            if (IsTokenValid(token: Token))
                return;

            Token = await LoginAsync();
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private async Task<TvdbLoginResponse?> LoginAsync()
    {
        if (string.IsNullOrEmpty(value: ApiKeyStore.Current.TvdbKey))
        {
            Logger.Tvdb(message: "TVDB API key not configured", level: LogEventLevel.Warning);
            return null;
        }

        try
        {
            HttpClient loginClient = HttpClientProvider.CreateClient(name: HttpClientNames.TvdbLogin);
            loginClient.BaseAddress ??= BaseUrl;

            using JsonContent content = JsonContent.Create(
                inputValue: new { apikey = ApiKeyStore.Current.TvdbKey }
            );
            using HttpRequestMessage request = new(method: HttpMethod.Post, requestUri: "login");
            request.Content = content;

            using HttpResponseMessage response = await loginClient.SendAsync(request: request);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Tvdb(
                    message: $"TVDB login failed: {(int)response.StatusCode} {response.ReasonPhrase}",
                    level: LogEventLevel.Error
                );
                return null;
            }

            string body = await response.Content.ReadAsStringAsync();
            TvdbLoginResponse? login = body.FromJson<TvdbLoginResponse>();
            if (login is not null)
                login.Data.ExpiresAt = DateTime.UtcNow.AddMonths(months: 1);
            return login;
        }
        catch (Exception ex)
        {
            Logger.Tvdb(message: $"TVDB login error: {ex.Message}", level: LogEventLevel.Error);
            return null;
        }
    }

    protected override async Task<T?> Get<T>(
        string url,
        Dictionary<string, string?>? query = null,
        bool? priority = false,
        bool skipCache = false
    )
        where T : class
    {
        await EnsureAuthenticatedAsync();
        if (Token is null)
            return null;

        query ??= new();
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
                            objectName: nameof(TvdbBaseClient),
                            message: "Cannot access a disposed TVDB client."
                        );
                    return SendAuthorizedAsync(url: newUrl);
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
            Logger.Tvdb(message: $"TVDB client disposed during {newUrl}", level: LogEventLevel.Debug);
            return null;
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode
                    is HttpStatusCode.NotFound
                        or HttpStatusCode.BadRequest
                        or HttpStatusCode.UnprocessableEntity
            )
        {
            Logger.Tvdb(message: $"HTTP {ex.StatusCode} for {newUrl}", level: LogEventLevel.Debug);
            return null;
        }
    }

    private async Task<string> SendAuthorizedAsync(string url)
    {
        using HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: url);
        if (Token is not null)
            request.Headers.Authorization = new(scheme: "Bearer", parameter: Token.Data.Token);
        if (!string.IsNullOrEmpty(value: _language))
            request.Headers.Add(name: "Accept-Language", value: _language);

        using HttpResponseMessage response = await Client.SendAsync(request: request);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Token rotated/expired mid-flight — clear and let the next call re-login.
            Token = null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    protected async Task<List<T>?> Paginated<T>(
        string url,
        int limit,
        Dictionary<string, string?>? query = null
    )
        where T : class
    {
        List<T> list = [];
        Dictionary<string, string?> first = query is null ? new() : new(dictionary: query);
        TvdbPaginatedResponse<T>? page = await Get<TvdbPaginatedResponse<T>>(url: url, query: first);
        if (page is null)
            return list;

        list.AddRange(collection: page.Data ?? []);
        int pages = 1;
        while (!string.IsNullOrEmpty(value: page?.Links?.Next) && pages < Max(available: int.MaxValue, wanted: limit, constraint: 500))
        {
            Dictionary<string, string?> next = query is null ? new() : new(dictionary: query);
            next[key: "page"] = (++pages).ToString();
            page = await Get<TvdbPaginatedResponse<T>>(url: url, query: next);
            if (page is null)
                break;

            list.AddRange(collection: page.Data ?? []);
        }

        return list;
    }
}
