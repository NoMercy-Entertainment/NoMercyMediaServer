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
/// Unit tests for <see cref="LicenseTokenClient"/>.
/// All tests drive a fake <see cref="HttpMessageHandler"/> — no network I/O.
/// </summary>
public class LicenseTokenClientTests
{
    // ── RequestAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RequestAsync_SuccessResponse_ReturnsToken()
    {
        DateTime expiresAt = DateTime.UtcNow.AddHours(value: 1);
        string body = JsonSerializer.Serialize(
            value: new
            {
                secret = "super-secret-key",
                expires_at = expiresAt,
                scopes = new[] { "distributed_encoding" },
            }
        );

        ILicenseTokenClient sut = MakeClient(handler: _ => new(statusCode: HttpStatusCode.OK)
        {
            Content = new StringContent(content: body, encoding: Encoding.UTF8, mediaType: "application/json"),
        });

        ClusterTokenResult result = await sut.RequestAsync(ct: CancellationToken.None);

        result.Token.Should().NotBeNull();
        result.Failure.Should().BeNull();
        result.Token!.Secret.Should().Be(expected: "super-secret-key");
        result.Token.Scopes.Should().Contain(expected: "distributed_encoding");
        result.Token.ExpiresAt.Should().BeCloseTo(nearbyTime: expiresAt, precision: TimeSpan.FromSeconds(seconds: 2));
    }

    [Fact]
    public async Task RequestAsync_401Response_ReturnsUnauthenticated()
    {
        ILicenseTokenClient sut = MakeClient(handler: _ => new(
            statusCode: HttpStatusCode.Unauthorized
        ));

        ClusterTokenResult result = await sut.RequestAsync(ct: CancellationToken.None);

        result.Token.Should().BeNull();
        result.Failure.Should().Be(expected: LicenseFailureKind.Unauthenticated);
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RequestAsync_403Response_ReturnsEntitlementRevoked()
    {
        ILicenseTokenClient sut = MakeClient(handler: _ => new(
            statusCode: HttpStatusCode.Forbidden
        ));

        ClusterTokenResult result = await sut.RequestAsync(ct: CancellationToken.None);

        result.Token.Should().BeNull();
        result.Failure.Should().Be(expected: LicenseFailureKind.EntitlementRevoked);
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RequestAsync_NetworkException_ReturnsNetworkError()
    {
        ILicenseTokenClient sut = MakeClient(handler: _ => throw new HttpRequestException(message: "timeout"));

        ClusterTokenResult result = await sut.RequestAsync(ct: CancellationToken.None);

        result.Token.Should().BeNull();
        result.Failure.Should().Be(expected: LicenseFailureKind.NetworkError);
        result.Message.Should().Contain(expected: "timeout");
    }

    [Fact]
    public async Task RequestAsync_AttachesAccessTokenHeader()
    {
        string? capturedAuth = null;
        string body = JsonSerializer.Serialize(
            value: new
            {
                secret = "s",
                expires_at = DateTime.UtcNow.AddMinutes(value: 10),
                scopes = Array.Empty<string>(),
            }
        );

        ILicenseTokenClient sut = MakeClient(
            handler: req =>
            {
                capturedAuth = req.Headers.Authorization?.ToString();
                return new(statusCode: HttpStatusCode.OK)
                {
                    Content = new StringContent(content: body, encoding: Encoding.UTF8, mediaType: "application/json"),
                };
            },
            accessToken: "my-access-token"
        );

        await sut.RequestAsync(ct: CancellationToken.None);

        capturedAuth.Should().Be(expected: "Bearer my-access-token");
    }

    // ── IntrospectAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IntrospectAsync_ActiveToken_ReturnsActiveTrue()
    {
        string body = JsonSerializer.Serialize(
            value: new { active = true, scopes = new[] { "distributed_encoding" } }
        );

        ILicenseTokenClient sut = MakeClient(handler: _ => new(statusCode: HttpStatusCode.OK)
        {
            Content = new StringContent(content: body, encoding: Encoding.UTF8, mediaType: "application/json"),
        });

        IntrospectResult result = await sut.IntrospectAsync(token: "some-token", ct: CancellationToken.None);

        result.Active.Should().BeTrue();
        result.Scopes.Should().Contain(expected: "distributed_encoding");
    }

    [Fact]
    public async Task IntrospectAsync_InactiveToken_ReturnsActiveFalse()
    {
        string body = JsonSerializer.Serialize(
            value: new { active = false, scopes = Array.Empty<string>() }
        );

        ILicenseTokenClient sut = MakeClient(handler: _ => new(statusCode: HttpStatusCode.OK)
        {
            Content = new StringContent(content: body, encoding: Encoding.UTF8, mediaType: "application/json"),
        });

        IntrospectResult result = await sut.IntrospectAsync(
            token: "expired-token",
            ct: CancellationToken.None
        );

        result.Active.Should().BeFalse();
    }

    [Fact]
    public async Task IntrospectAsync_CachesResult_DoesNotRepeatNetworkCall()
    {
        int callCount = 0;
        string body = JsonSerializer.Serialize(
            value: new { active = true, scopes = Array.Empty<string>() }
        );

        ILicenseTokenClient sut = MakeClient(handler: _ =>
        {
            Interlocked.Increment(location: ref callCount);
            return new(statusCode: HttpStatusCode.OK)
            {
                Content = new StringContent(content: body, encoding: Encoding.UTF8, mediaType: "application/json"),
            };
        });

        const string token = "cached-token";
        await sut.IntrospectAsync(token: token, ct: CancellationToken.None);
        await sut.IntrospectAsync(token: token, ct: CancellationToken.None);
        await sut.IntrospectAsync(token: token, ct: CancellationToken.None);

        callCount.Should().Be(expected: 1, because: "subsequent calls for same token must be served from cache");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
