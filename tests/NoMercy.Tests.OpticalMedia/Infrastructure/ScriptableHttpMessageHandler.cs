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

namespace NoMercy.Tests.OpticalMedia.Infrastructure;

/// <summary>
/// A single scripted route: a predicate over the request, and an ordered queue
/// of canned responses. The Nth matching request gets the Nth queued response;
/// once the queue is drained, the last response repeats.
/// </summary>
internal sealed class HandlerRoute(
    Func<HttpRequestMessage, bool> match,
    IEnumerable<Func<HttpRequestMessage, HttpResponseMessage>> responses
)
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new(
        collection: responses
    );
    private Func<HttpRequestMessage, HttpResponseMessage>? _last;

    public bool Matches(HttpRequestMessage request) => match(arg: request);

    public HttpResponseMessage Respond(HttpRequestMessage request)
    {
        Func<HttpRequestMessage, HttpResponseMessage> respond =
            _responses.Count > 0 ? _responses.Dequeue() : _last!;
        _last = respond;
        return respond(arg: request);
    }
}

/// <summary>
/// Test-only <see cref="HttpMessageHandler"/> that stands in for the network.
/// Provider clients (TMDB / MusicBrainz / CoverArt) resolve their
/// <see cref="HttpClient"/> through the process-wide
/// <c>NoMercy.Providers.Helpers.HttpClientProvider</c> static, so one instance
/// of this handler — wired in via <see cref="ProviderHttpHarness"/> —
/// intercepts every outgoing request for the lifetime of a test.
///
/// Trimmed local copy of the pattern established in
/// <c>tests/NoMercy.Tests.Providers/Infrastructure</c>: cross-test-project
/// references aren't used elsewhere in the repo, so OpticalMedia carries its
/// own minimal harness rather than depending on another test assembly.
/// </summary>
public sealed class ScriptableHttpMessageHandler : HttpMessageHandler
{
    private readonly List<HandlerRoute> _routes = [];
    private readonly object _gate = new();

    public void WhenGet(
        string pathContains,
        params Func<HttpRequestMessage, HttpResponseMessage>[] responses
    ) =>
        When(
            match: request =>
                request.Method == HttpMethod.Get
                && (
                    request.RequestUri?.AbsolutePath.Contains(
                        value: pathContains,
                        comparisonType: StringComparison.Ordinal
                    ) ?? false
                ),
            responses: responses
        );

    private void When(
        Func<HttpRequestMessage, bool> match,
        params Func<HttpRequestMessage, HttpResponseMessage>[] responses
    )
    {
        lock (_gate)
            _routes.Add(item: new(match: match, responses: responses));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        HandlerRoute? route;
        lock (_gate)
            route = _routes.FirstOrDefault(predicate: r => r.Matches(request: request));

        if (route is null)
            return Task.FromResult(
                result: new HttpResponseMessage(statusCode: HttpStatusCode.NotFound)
                {
                    Content = new StringContent(
                        content: $"ScriptableHttpMessageHandler: no route scripted for {request.Method} {request.RequestUri}"
                    ),
                }
            );

        return Task.FromResult(result: route.Respond(request: request));
    }
}

/// <summary>Canned-response builders shared by every test that scripts a route.</summary>
public static class MockResponse
{
    public static Func<HttpRequestMessage, HttpResponseMessage> Json(
        HttpStatusCode status,
        string body
    ) =>
        _ => new HttpResponseMessage(statusCode: status)
        {
            Content = new StringContent(content: body, encoding: Encoding.UTF8, mediaType: "application/json"),
        };

    public static Func<HttpRequestMessage, HttpResponseMessage> Status(HttpStatusCode status) =>
        _ => new HttpResponseMessage(statusCode: status) { Content = new StringContent(content: string.Empty) };
}
