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
using Polly;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;

namespace NoMercy.NmSystem.Extensions;

public class GenericHttpClient
{
    private readonly HttpClient _client;
    private readonly AsyncPolicyWrap<HttpResponseMessage> _resiliencePolicy;

    public GenericHttpClient(string? baseUrl = null, int timeoutSeconds = 5, int retryCount = 3)
    {
        _client = new();

        if (!string.IsNullOrEmpty(value: baseUrl))
            _client.BaseAddress = new(uriString: baseUrl);

        // Timeout policy
        AsyncTimeoutPolicy<HttpResponseMessage>? timeoutPolicy =
            Policy.TimeoutAsync<HttpResponseMessage>(
                timeout: TimeSpan.FromSeconds(seconds: timeoutSeconds),
                timeoutStrategy: TimeoutStrategy.Optimistic
            );

        // Retry only for transient failures: 5xx, 408 (RequestTimeout), 429 (TooManyRequests) and network exceptions
        AsyncRetryPolicy<HttpResponseMessage>? retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>() // often indicates a timeout or network drop
            .OrResult(resultPredicate: r => r != null && IsTransientStatusCode(statusCode: r.StatusCode))
            .WaitAndRetryAsync(
                retryCount: retryCount,
                sleepDurationProvider: retryAttempt =>
                {
                    // Exponential backoff with cap
                    double seconds = Math.Min(val1: Math.Pow(x: 2, y: retryAttempt), val2: 30);
                    return TimeSpan.FromSeconds(value: seconds);
                },
                onRetryAsync: (_, _, _, _) => Task.CompletedTask
            );

        _resiliencePolicy = Policy.WrapAsync(policies: [retryPolicy, timeoutPolicy]);
    }

    public void SetDefaultHeaders(string userAgent, string? bearerToken = null)
    {
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(input: userAgent);
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(item: new(mediaType: "application/json"));

        if (!string.IsNullOrEmpty(value: bearerToken))
            _client.DefaultRequestHeaders.Authorization = new(scheme: "Bearer", parameter: bearerToken);
    }

    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string endpoint,
        HttpContent? content = null,
        CancellationToken cancellationToken = default
    )
    {
        return _resiliencePolicy.ExecuteAsync(
            action: ct =>
            {
                HttpRequestMessage request = new(method: method, requestUri: endpoint) { Content = content };
                return _client.SendAsync(request: request, cancellationToken: ct);
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task<string> SendAndReadAsync(
        HttpMethod method,
        string endpoint,
        HttpContent? content = null,
        Dictionary<string, string>? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        // Wrap the response in 'using' so the underlying connection
        // returns to the SocketsHttpHandler pool promptly. Without this
        // every SendAndReadAsync call held the connection until GC.
        using HttpResponseMessage response =
            queryParams?.Count > 0
                ? await SendAsync(method: method, endpoint: endpoint, queryParams: queryParams, cancellationToken: cancellationToken)
                : await SendAsync(method: method, endpoint: endpoint, content: content, cancellationToken: cancellationToken);

        string body = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                message: $"Request to {endpoint} failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}",
                inner: null,
                statusCode: response.StatusCode
            );

        return body;
    }

    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string endpoint,
        Dictionary<string, string> queryParams,
        CancellationToken cancellationToken = default
    )
    {
        if (queryParams.Count > 0)
        {
            string query = string.Join(
                separator: "&",
                values: queryParams.Select(selector: kvp =>
                    $"{Uri.EscapeDataString(stringToEscape: kvp.Key)}={Uri.EscapeDataString(stringToEscape: kvp.Value)}"
                )
            );
            endpoint = $"{endpoint}{(endpoint.Contains(value: '?') ? "&" : "?")}{query}";
        }

        return _resiliencePolicy.ExecuteAsync(
            action: ct =>
            {
                HttpRequestMessage request = new(method: method, requestUri: endpoint);
                return _client.SendAsync(request: request, cancellationToken: ct);
            },
            cancellationToken: cancellationToken
        );
    }

    private static bool IsTransientStatusCode(HttpStatusCode? statusCode)
    {
        if (!statusCode.HasValue)
            return false;
        int code = (int)statusCode.Value;

        return code switch
        {
            // Retry on server errors (5xx), 408 Request Timeout, and 429 Too Many Requests
            >= 500 and <= 599 or 408 or 429 => true,
            _ => false,
        };
    }
}
