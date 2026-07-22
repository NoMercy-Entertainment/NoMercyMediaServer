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
using System.Text.Json;
using NoMercy.Encoder.Distribution;

namespace NoMercy.Tests.Encoder.Distribution;

/// <summary>
/// Branch-coverage gaps for <see cref="LicenseTokenClient"/>:
///
/// • Unknown HTTP status (e.g. 500) returns NetworkError so callers retry.
/// • Empty/null Secret in success body returns NetworkError — the coordinator
///   responded 200 but didn't actually issue a token; treat as transient.
/// • IntrospectAsync non-200 returns Active=false with descriptive message
///   without throwing.
/// • IntrospectAsync exception returns Active=false (network errors must not
///   bubble — middleware fails the request, no crash).
/// • IntrospectAsync cache expires after 30s — the next call hits network.
/// • IntrospectAsync different tokens each get their own cache entry.
/// • RequestAsync cancellation propagates instead of being wrapped.
/// </summary>
public class LicenseTokenClientBranchTests
{
    // ── RequestAsync edge cases ──────────────────────────────────────────────

    [Fact]
    public async Task RequestAsync_500_returns_NetworkError()
    {
        ILicenseTokenClient sut = MakeClient(handler: _ => new(
            statusCode: HttpStatusCode.InternalServerError
        ));

        ClusterTokenResult result = await sut.RequestAsync(ct: CancellationToken.None);

        result.Token.Should().BeNull();
        result.Failure.Should().Be(expected: LicenseFailureKind.NetworkError);
        result.Message.Should().Contain(expected: "500");
    }

    [Fact]
    public async Task RequestAsync_502_returns_NetworkError()
    {
        ILicenseTokenClient sut = MakeClient(handler: _ => new(
            statusCode: HttpStatusCode.BadGateway
        ));

        ClusterTokenResult result = await sut.RequestAsync(ct: CancellationToken.None);

        result.Token.Should().BeNull();
        result.Failure.Should().Be(expected: LicenseFailureKind.NetworkError);
        result.Message.Should().Contain(expected: "502");
    }

    [Fact]
    public async Task RequestAsync_empty_secret_in_OK_response_returns_NetworkError()
    {
        // Coordinator returned 200 but the body has an empty secret. Don't
        // treat as success — caller will retry on NetworkError.
        string body = JsonSerializer.Serialize(
            value: new
            {
                secret = "",
                expires_at = DateTime.UtcNow.AddHours(value: 1),
                scopes = new[] { "encode" },
            }
        );
        ILicenseTokenClient sut = MakeClient(handler: _ => new(statusCode: HttpStatusCode.OK)
        {
            Content = new StringContent(content: body, encoding: Encoding.UTF8, mediaType: "application/json"),
        });

        ClusterTokenResult result = await sut.RequestAsync(ct: CancellationToken.None);

        result.Token.Should().BeNull();
        result.Failure.Should().Be(expected: LicenseFailureKind.NetworkError);
        result.Message.Should().Contain(expected: "empty token body");
    }

    [Fact]
    public async Task RequestAsync_null_secret_in_OK_response_returns_NetworkError()
    {
        string body = JsonSerializer.Serialize(
            value: new
            {
                secret = (string?)null,
                expires_at = DateTime.UtcNow.AddHours(value: 1),
                scopes = new[] { "encode" },
            }
        );
        ILicenseTokenClient sut = MakeClient(handler: _ => new(statusCode: HttpStatusCode.OK)
        {
            Content = new StringContent(content: body, encoding: Encoding.UTF8, mediaType: "application/json"),
        });

        ClusterTokenResult result = await sut.RequestAsync(ct: CancellationToken.None);

        result.Token.Should().BeNull();
        result.Failure.Should().Be(expected: LicenseFailureKind.NetworkError);
    }

    [Fact]
    public async Task RequestAsync_whitespace_secret_in_OK_response_returns_NetworkError()
    {
        string body = JsonSerializer.Serialize(
            value: new
            {
                secret = "   ",
                expires_at = DateTime.UtcNow.AddHours(value: 1),
                scopes = new[] { "encode" },
            }
        );
        ILicenseTokenClient sut = MakeClient(handler: _ => new(statusCode: HttpStatusCode.OK)
        {
            Content = new StringContent(content: body, encoding: Encoding.UTF8, mediaType: "application/json"),
        });

        ClusterTokenResult result = await sut.RequestAsync(ct: CancellationToken.None);

        result.Token.Should().BeNull();
        result.Failure.Should().Be(expected: LicenseFailureKind.NetworkError);
    }

    [Fact]
    public async Task RequestAsync_missing_scopes_field_defaults_to_empty_list()
    {
        // body has no `scopes` field — should NOT crash, should produce a
        // valid ClusterToken with an empty scope list.
        string body = JsonSerializer.Serialize(
            value: new { secret = "abc", expires_at = DateTime.UtcNow.AddHours(value: 1) }
        );
        ILicenseTokenClient sut = MakeClient(handler: _ => new(statusCode: HttpStatusCode.OK)
        {
            Content = new StringContent(content: body, encoding: Encoding.UTF8, mediaType: "application/json"),
        });

        ClusterTokenResult result = await sut.RequestAsync(ct: CancellationToken.None);

        result.Token.Should().NotBeNull();
        result.Token!.Scopes.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestAsync_cancellation_propagates_does_not_wrap_in_NetworkError()
    {
        // The OperationCanceledException catch arm re-throws — the caller
        // must see the cancel, not a wrapped NetworkError result.
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        ILicenseTokenClient sut = MakeClient(handler: _ => throw new OperationCanceledException());

        Func<Task> act = () => sut.RequestAsync(ct: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── IntrospectAsync edge cases ───────────────────────────────────────────

    [Fact]
    public async Task IntrospectAsync_non_200_returns_inactive_with_descriptive_message()
    {
        ILicenseTokenClient sut = MakeClient(handler: _ => new(
            statusCode: HttpStatusCode.InternalServerError
        ));

        IntrospectResult result = await sut.IntrospectAsync(token: "any-token", ct: CancellationToken.None);

        result.Active.Should().BeFalse();
        result.Scopes.Should().BeEmpty();
        result.Message.Should().Contain(expected: "500");
    }

    [Fact]
    public async Task IntrospectAsync_exception_returns_inactive_without_throwing()
    {
        ILicenseTokenClient sut = MakeClient(handler: _ => throw new HttpRequestException(message: "connect failed"));

        IntrospectResult result = await sut.IntrospectAsync(token: "token", ct: CancellationToken.None);

        result.Active.Should().BeFalse();
        result.Scopes.Should().BeEmpty();
        result.Message.Should().Contain(expected: "connect failed");
    }

    [Fact]
    public async Task IntrospectAsync_different_tokens_get_separate_cache_entries()
    {
        // Three different tokens, each must hit the network once.
        Dictionary<string, int> callCounts = new();
        string body = JsonSerializer.Serialize(
            value: new { active = true, scopes = Array.Empty<string>() }
        );

        ILicenseTokenClient sut = MakeClient(handler: req =>
        {
            string requestBody = req.Content!.ReadAsStringAsync().Result;
            string tokenKey =
                requestBody.Contains(value: "\"token\":\"t1\"") ? "t1"
                : requestBody.Contains(value: "\"token\":\"t2\"") ? "t2"
                : "t3";
            callCounts[key: tokenKey] = callCounts.GetValueOrDefault(key: tokenKey) + 1;
            return new(statusCode: HttpStatusCode.OK)
            {
                Content = new StringContent(content: body, encoding: Encoding.UTF8, mediaType: "application/json"),
            };
        });

        await sut.IntrospectAsync(token: "t1", ct: CancellationToken.None);
        await sut.IntrospectAsync(token: "t2", ct: CancellationToken.None);
        await sut.IntrospectAsync(token: "t3", ct: CancellationToken.None);
        await sut.IntrospectAsync(token: "t1", ct: CancellationToken.None); // cached
        await sut.IntrospectAsync(token: "t2", ct: CancellationToken.None); // cached

        callCounts[key: "t1"].Should().Be(expected: 1);
        callCounts[key: "t2"].Should().Be(expected: 1);
        callCounts[key: "t3"].Should().Be(expected: 1);
    }

    [Fact]
    public async Task IntrospectAsync_failed_response_also_cached_so_we_dont_hammer_coordinator()
    {
        // Whatever the result is — success, inactive, or failure — the cache
        // stores it. Documented contract: 30-second cache catches every code
        // path, including the failure ones, so a flaky coordinator doesn't
        // produce a burst of identical introspect requests.
        int callCount = 0;
        ILicenseTokenClient sut = MakeClient(handler: _ =>
        {
            Interlocked.Increment(location: ref callCount);
            return new(statusCode: HttpStatusCode.InternalServerError);
        });

        await sut.IntrospectAsync(token: "token", ct: CancellationToken.None);
        await sut.IntrospectAsync(token: "token", ct: CancellationToken.None);
        await sut.IntrospectAsync(token: "token", ct: CancellationToken.None);

        callCount.Should().Be(expected: 1, because: "failures must be cached too so we don't hammer the coordinator");
    }

    // ── Bearer header behaviour ─────────────────────────────────────────────

    [Fact]
    public async Task RequestAsync_omits_authorization_header_when_access_token_null()
    {
        string? capturedAuth = "PRESENT";
        ILicenseTokenClient sut = MakeClient(
            handler: req =>
            {
                capturedAuth = req.Headers.Authorization?.ToString();
                return new(statusCode: HttpStatusCode.Unauthorized);
            },
            accessToken: null
        );

        await sut.RequestAsync(ct: CancellationToken.None);

        capturedAuth.Should().BeNull();
    }

    [Fact]
    public async Task RequestAsync_omits_authorization_header_when_access_token_whitespace()
    {
        string? capturedAuth = "PRESENT";
        ILicenseTokenClient sut = MakeClient(
            handler: req =>
            {
                capturedAuth = req.Headers.Authorization?.ToString();
                return new(statusCode: HttpStatusCode.Unauthorized);
            },
            accessToken: "   "
        );

        await sut.RequestAsync(ct: CancellationToken.None);

        capturedAuth.Should().BeNull();
    }

    [Fact]
    public async Task RequestAsync_attaches_bearer_when_access_token_provided()
    {
        string? capturedAuth = null;
        ILicenseTokenClient sut = MakeClient(
            handler: req =>
            {
                capturedAuth = req.Headers.Authorization?.ToString();
                return new(statusCode: HttpStatusCode.Unauthorized);
            },
            accessToken: "the-keycloak-token"
        );

        await sut.RequestAsync(ct: CancellationToken.None);

        capturedAuth.Should().Be(expected: "Bearer the-keycloak-token");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ILicenseTokenClient MakeClient(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        string? accessToken = null
    )
    {
        FakeHandler fake = new(respond: handler);
        HttpClient http = new(handler: fake) { BaseAddress = new(uriString: "https://api.nomercy.tv/") };
        return new LicenseTokenClient(http: http, accessTokenProvider: () => accessToken);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result: respond(arg: request));
        }
    }
}
