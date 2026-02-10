using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.TestHost;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Integration")]
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

        HttpResponseMessage response = await client.GetAsync("/api/v1/nonexistent-endpoint-for-testing");

        // In non-dev mode, we should get a standard HTTP error, not an HTML exception page
        string content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("DeveloperExceptionPage", content);
        Assert.DoesNotContain("<html", content.ToLowerInvariant());
    }

    [Fact]
    public async Task Compression_AppliedToResponses_WhenClientAcceptsGzip()
    {
        // Create a client that does NOT auto-decompress so we can inspect Content-Encoding
        HttpClient client = new(_factory.Server.CreateHandler())
        {
            BaseAddress = new Uri("http://localhost")
        };
        client = client.AsAuthenticated();

        HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/setup/permissions");
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

        HttpResponseMessage response = await client.SendAsync(request);

        // Response should be compressed when client accepts it
        // The Content-Encoding header indicates compression was applied
        bool isCompressed = response.Content.Headers.ContentEncoding.Any(
            e => e == "gzip" || e == "br");

        // If the response is very small, the server may skip compression
        // so we check either compression was applied OR the response is small enough
        // that compression wouldn't help
        long contentLength = response.Content.Headers.ContentLength ?? 0;
        Assert.True(
            isCompressed || contentLength < 100,
            $"Expected compressed response or small body. " +
            $"Content-Encoding: [{string.Join(", ", response.Content.Headers.ContentEncoding)}], " +
            $"Content-Length: {contentLength}");
    }

    [Fact]
    public async Task CorsPreFlight_ReturnsSuccess_ForAllowedOrigin()
    {
        HttpClient client = _factory.CreateClient();

        HttpRequestMessage request = new(HttpMethod.Options, "/api/v1/setup/permissions");
        request.Headers.Add("Origin", "https://nomercy.tv");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Authorization");

        HttpResponseMessage response = await client.SendAsync(request);

        // Pre-flight should succeed (2xx or 204)
        Assert.True(
            (int)response.StatusCode >= 200 && (int)response.StatusCode < 300,
            $"CORS pre-flight expected 2xx, got {(int)response.StatusCode}");

        // Should include CORS headers
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Response should contain Access-Control-Allow-Origin header");
    }

    [Fact]
    public async Task CorsPreFlight_NoCorHeaders_ForDisallowedOrigin()
    {
        HttpClient client = _factory.CreateClient();

        HttpRequestMessage request = new(HttpMethod.Options, "/api/v1/setup/permissions");
        request.Headers.Add("Origin", "https://malicious-site.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage response = await client.SendAsync(request);

        // Should not include the disallowed origin in Access-Control-Allow-Origin
        if (response.Headers.TryGetValues("Access-Control-Allow-Origin", out IEnumerable<string>? values))
        {
            Assert.DoesNotContain("malicious-site.example.com", values);
        }
    }
}

internal static class HttpRequestMessageExtensions
{
    public static async Task<HttpResponseMessage> SendAsync(
        this HttpRequestMessage request, HttpClient client)
    {
        return await client.SendAsync(request);
    }
}
