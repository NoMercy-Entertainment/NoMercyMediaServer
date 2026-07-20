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
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.NewtonSoftConverters;

namespace NoMercy.Tests.Providers.Infrastructure;

/// <summary>
/// Immutable snapshot of an outgoing request. <see cref="ExternalApiClient"/>
/// (and its subclasses) build a fresh <see cref="HttpRequestMessage"/> per call
/// and dispose it as soon as <c>SendAsync</c> returns, so a route/assertion that
/// held onto the live message would see it torn down under it. Every field here
/// is copied out inside <see cref="ScriptableHttpMessageHandler.SendAsync"/>
/// before the response is produced.
/// </summary>
public sealed record CapturedRequest(
    HttpMethod Method,
    Uri Uri,
    IReadOnlyDictionary<string, IEnumerable<string>> Headers
)
{
    public string Path => Uri.AbsolutePath;

    // Concrete Dictionary (not IReadOnlyDictionary) so FluentAssertions'
    // dictionary assertions (ContainKey/.WhoseValue, NotContainKey, ...)
    // resolve unambiguously against Should().
    public Dictionary<string, string?> Query =>
        QueryHelpers
            .ParseQuery(Uri.Query)
            .ToDictionary(pair => pair.Key, pair => (string?)pair.Value.ToString());

    public bool HasHeader(string name) =>
        Headers.Keys.Any(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

    // HttpHeaders re-tokenizes some well-known headers (User-Agent's product/
    // comment tokens, for one) into several entries in the values list even
    // though only a single logical value was ever added — so this must join
    // every value rather than take the first.
    public string? HeaderValue(string name)
    {
        List<string> values = Headers
            .Where(kv => string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
            .SelectMany(kv => kv.Value)
            .ToList();

        return values.Count == 0 ? null : string.Join(" ", values);
    }
}

/// <summary>
/// A single scripted route: a predicate over the request, and an ordered queue
/// of canned responses. The Nth matching request gets the Nth queued response
/// (dequeued); once the queue is drained, every further match repeats the last
/// response — which is what lets a test script "429, 429, 200" for a retry
/// contract without the route silently falling through afterwards.
/// </summary>
internal sealed class HandlerRoute(
    Func<HttpRequestMessage, bool> match,
    IEnumerable<Func<HttpRequestMessage, HttpResponseMessage>> responses
)
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new(
        responses
    );
    private Func<HttpRequestMessage, HttpResponseMessage>? _last;

    public bool Matches(HttpRequestMessage request) => match(request);

    public HttpResponseMessage Respond(HttpRequestMessage request)
    {
        Func<HttpRequestMessage, HttpResponseMessage> respond =
            _responses.Count > 0 ? _responses.Dequeue() : _last!;
        _last = respond;
        return respond(request);
    }
}

/// <summary>
/// Test-only <see cref="HttpMessageHandler"/> that stands in for the network.
/// Every provider client under test resolves its <see cref="HttpClient"/>
/// through the process-wide <see cref="HttpClientProvider"/> static, so one
/// instance of this handler — wired in via <see cref="ProviderHttpHarness"/> —
/// intercepts every outgoing request for the lifetime of a test.
///
/// Unmatched requests fail loudly (404 carrying a diagnostic body) rather than
/// silently falling through to a default 200 — a route that never matches
/// because of a URL-building regression should fail the test that scripted it,
/// not quietly return an empty success.
/// </summary>
public sealed class ScriptableHttpMessageHandler : HttpMessageHandler
{
    private readonly List<HandlerRoute> _routes = [];
    private readonly List<CapturedRequest> _requests = [];
    private readonly object _gate = new();

    public IReadOnlyList<CapturedRequest> Requests
    {
        get
        {
            lock (_gate)
                return _requests.ToList();
        }
    }

    public int RequestCountFor(string pathContains) =>
        Requests.Count(r => r.Path.Contains(pathContains, StringComparison.Ordinal));

    public void When(
        Func<HttpRequestMessage, bool> match,
        params Func<HttpRequestMessage, HttpResponseMessage>[] responses
    )
    {
        lock (_gate)
            _routes.Add(new(match, responses));
    }

    public void WhenGet(
        string pathContains,
        params Func<HttpRequestMessage, HttpResponseMessage>[] responses
    ) =>
        When(
            request =>
                request.Method == HttpMethod.Get
                && (
                    request.RequestUri?.AbsolutePath.Contains(
                        pathContains,
                        StringComparison.Ordinal
                    ) ?? false
                ),
            responses
        );

    public void WhenPost(
        string pathContains,
        params Func<HttpRequestMessage, HttpResponseMessage>[] responses
    ) =>
        When(
            request =>
                request.Method == HttpMethod.Post
                && (
                    request.RequestUri?.AbsolutePath.Contains(
                        pathContains,
                        StringComparison.Ordinal
                    ) ?? false
                ),
            responses
        );

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        CapturedRequest snapshot = new(
            request.Method,
            request.RequestUri ?? throw new InvalidOperationException("Request has no URI."),
            request
                .Headers.Concat(
                    request.Content?.Headers
                        ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>()
                )
                .ToDictionary(h => h.Key, h => h.Value)
        );

        HandlerRoute? route;
        lock (_gate)
        {
            _requests.Add(snapshot);
            route = _routes.FirstOrDefault(r => r.Matches(request));
        }

        if (route is null)
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(
                        $"ScriptableHttpMessageHandler: no route scripted for {request.Method} {request.RequestUri}"
                    ),
                }
            );

        return Task.FromResult(route.Respond(request));
    }
}

/// <summary>Canned-response builders shared by every provider test that scripts a route.</summary>
public static class MockResponse
{
    public static Func<HttpRequestMessage, HttpResponseMessage> Json(
        HttpStatusCode status,
        string body
    ) =>
        _ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    public static Func<HttpRequestMessage, HttpResponseMessage> Json<T>(
        HttpStatusCode status,
        T body
    ) => Json(status, body!.ToJson());

    public static Func<HttpRequestMessage, HttpResponseMessage> Status(HttpStatusCode status) =>
        _ => new HttpResponseMessage(status) { Content = new StringContent(string.Empty) };

    /// <summary>200 OK with a body that fails JSON deserialization.</summary>
    public static Func<HttpRequestMessage, HttpResponseMessage> Malformed() =>
        _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{ this is not valid json",
                Encoding.UTF8,
                "application/json"
            ),
        };
}
