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

using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait(name: "Category", value: "Integration")]
public class MiddlewareOrderingTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public MiddlewareOrderingTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeveloperExceptionPage_NotServed_InNonDevMode()
    {
        // Config.IsDev is false in tests (not started with --dev flag)
        // A route that doesn't exist should return 404, not the dev exception page
        HttpClient client = _factory.CreateClient().AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync(
            requestUri: "/api/v1/nonexistent-endpoint-for-testing"
        );

        // In non-dev mode, we should get a standard HTTP error, not an HTML exception page
        string content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(expectedSubstring: "DeveloperExceptionPage", actualString: content);
        Assert.DoesNotContain(expectedSubstring: "<html", actualString: content.ToLowerInvariant());
    }

    [Fact]
    public async Task Compression_AppliedToResponses_WhenClientAcceptsGzip()
    {
        // Create a client that does NOT auto-decompress so we can inspect Content-Encoding
        HttpClient client = new(handler: _factory.Server.CreateHandler())
        {
            BaseAddress = new(uriString: "http://localhost"),
        };
        client = client.AsAuthenticated();

        HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: "/api/v1/setup/permissions");
        request.Headers.AcceptEncoding.Add(item: new(value: "gzip"));
        request.Headers.AcceptEncoding.Add(item: new(value: "br"));

        HttpResponseMessage response = await client.SendAsync(request: request);

        // Response should be compressed when client accepts it
        // The Content-Encoding header indicates compression was applied
        bool isCompressed = response.Content.Headers.ContentEncoding.Any(predicate: e =>
            e == "gzip" || e == "br"
        );

        // If the response is very small, the server may skip compression
        // so we check either compression was applied OR the response is small enough
        // that compression wouldn't help
        long contentLength = response.Content.Headers.ContentLength ?? 0;
        Assert.True(
            condition: isCompressed || contentLength < 100,
            userMessage: $"Expected compressed response or small body. "
                         + $"Content-Encoding: [{string.Join(separator: ", ", values: response.Content.Headers.ContentEncoding)}], "
                         + $"Content-Length: {contentLength}"
        );
    }

    [Theory]
    [InlineData(data: "https://nomercy.tv")]
    [InlineData(data: "http://localhost:7625")]
    public async Task CorsPreFlight_ReturnsSuccess_ForAllowedOrigin(string origin)
    {
        HttpClient client = _factory.CreateClient();

        HttpRequestMessage request = new(method: HttpMethod.Options, requestUri: "/api/v1/setup/permissions");
        request.Headers.Add(name: "Origin", value: origin);
        request.Headers.Add(name: "Access-Control-Request-Method", value: "GET");
        request.Headers.Add(name: "Access-Control-Request-Headers", value: "Authorization");

        HttpResponseMessage response = await client.SendAsync(request: request);

        // Pre-flight should succeed (2xx or 204)
        Assert.True(
            condition: (int)response.StatusCode >= 200 && (int)response.StatusCode < 300,
            userMessage: $"CORS pre-flight expected 2xx for {origin}, got {(int)response.StatusCode}"
        );

        // Should include CORS headers
        Assert.True(
            condition: response.Headers.Contains(name: "Access-Control-Allow-Origin"),
            userMessage: $"Response should contain Access-Control-Allow-Origin header for {origin}"
        );
    }

    [Fact]
    public async Task CorsPreFlight_NoCorHeaders_ForDisallowedOrigin()
    {
        HttpClient client = _factory.CreateClient();

        HttpRequestMessage request = new(method: HttpMethod.Options, requestUri: "/api/v1/setup/permissions");
        request.Headers.Add(name: "Origin", value: "https://malicious-site.example.com");
        request.Headers.Add(name: "Access-Control-Request-Method", value: "GET");

        HttpResponseMessage response = await client.SendAsync(request: request);

        // Should not include the disallowed origin in Access-Control-Allow-Origin
        if (
            response.Headers.TryGetValues(
                name: "Access-Control-Allow-Origin",
                values: out IEnumerable<string>? values
            )
        )
        {
            Assert.DoesNotContain(expected: "malicious-site.example.com", collection: values);
        }
    }

    [Theory]
    [InlineData(data: "http://192.168.2.201:5501")]
    [InlineData(data: "http://192.168.2.201:5502")]
    [InlineData(data: "http://192.168.2.201:5503")]
    [InlineData(data: "http://localhost")]
    [InlineData(data: "https://localhost")]
    public async Task CorsPreFlight_DevOrigins_NotAllowed_InNonDevMode(string devOrigin)
    {
        // Config.IsDev is false in tests — dev-only origins should be rejected
        HttpClient client = _factory.CreateClient();

        HttpRequestMessage request = new(method: HttpMethod.Options, requestUri: "/api/v1/setup/permissions");
        request.Headers.Add(name: "Origin", value: devOrigin);
        request.Headers.Add(name: "Access-Control-Request-Method", value: "GET");

        HttpResponseMessage response = await client.SendAsync(request: request);

        // Should not include the dev origin in Access-Control-Allow-Origin
        if (
            response.Headers.TryGetValues(
                name: "Access-Control-Allow-Origin",
                values: out IEnumerable<string>? values
            )
        )
        {
            Assert.DoesNotContain(expected: devOrigin, collection: values);
        }
    }
}

internal static class HttpRequestMessageExtensions
{
    public static async Task<HttpResponseMessage> SendAsync(
        this HttpRequestMessage request,
        HttpClient client
    )
    {
        return await client.SendAsync(request: request);
    }
}
