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
        DateTime expiresAt = DateTime.UtcNow.AddHours(1);
        string body = JsonSerializer.Serialize(
            new
            {
                secret = "super-secret-key",
                expires_at = expiresAt,
                scopes = new[] { "distributed_encoding" },
            }
        );

        ILicenseTokenClient sut = MakeClient(_ => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

        ClusterTokenResult result = await sut.RequestAsync(CancellationToken.None);

        result.Token.Should().NotBeNull();
        result.Failure.Should().BeNull();
        result.Token!.Secret.Should().Be("super-secret-key");
        result.Token.Scopes.Should().Contain("distributed_encoding");
        result.Token.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RequestAsync_401Response_ReturnsUnauthenticated()
    {
        ILicenseTokenClient sut = MakeClient(_ => new(
            HttpStatusCode.Unauthorized
        ));

        ClusterTokenResult result = await sut.RequestAsync(CancellationToken.None);

        result.Token.Should().BeNull();
        result.Failure.Should().Be(LicenseFailureKind.Unauthenticated);
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RequestAsync_403Response_ReturnsEntitlementRevoked()
    {
        ILicenseTokenClient sut = MakeClient(_ => new(
            HttpStatusCode.Forbidden
        ));

        ClusterTokenResult result = await sut.RequestAsync(CancellationToken.None);

        result.Token.Should().BeNull();
        result.Failure.Should().Be(LicenseFailureKind.EntitlementRevoked);
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RequestAsync_NetworkException_ReturnsNetworkError()
    {
        ILicenseTokenClient sut = MakeClient(_ => throw new HttpRequestException("timeout"));

        ClusterTokenResult result = await sut.RequestAsync(CancellationToken.None);

        result.Token.Should().BeNull();
        result.Failure.Should().Be(LicenseFailureKind.NetworkError);
        result.Message.Should().Contain("timeout");
    }

    [Fact]
    public async Task RequestAsync_AttachesAccessTokenHeader()
    {
        string? capturedAuth = null;
        string body = JsonSerializer.Serialize(
            new
            {
                secret = "s",
                expires_at = DateTime.UtcNow.AddMinutes(10),
                scopes = Array.Empty<string>(),
            }
        );

        ILicenseTokenClient sut = MakeClient(
            req =>
            {
                capturedAuth = req.Headers.Authorization?.ToString();
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };
            },
            "my-access-token"
        );

        await sut.RequestAsync(CancellationToken.None);

        capturedAuth.Should().Be("Bearer my-access-token");
    }

    // ── IntrospectAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IntrospectAsync_ActiveToken_ReturnsActiveTrue()
    {
        string body = JsonSerializer.Serialize(
            new { active = true, scopes = new[] { "distributed_encoding" } }
        );

        ILicenseTokenClient sut = MakeClient(_ => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

        IntrospectResult result = await sut.IntrospectAsync("some-token", CancellationToken.None);

        result.Active.Should().BeTrue();
        result.Scopes.Should().Contain("distributed_encoding");
    }

    [Fact]
    public async Task IntrospectAsync_InactiveToken_ReturnsActiveFalse()
    {
        string body = JsonSerializer.Serialize(
            new { active = false, scopes = Array.Empty<string>() }
        );

        ILicenseTokenClient sut = MakeClient(_ => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

        IntrospectResult result = await sut.IntrospectAsync(
            "expired-token",
            CancellationToken.None
        );

        result.Active.Should().BeFalse();
    }

    [Fact]
    public async Task IntrospectAsync_CachesResult_DoesNotRepeatNetworkCall()
    {
        int callCount = 0;
        string body = JsonSerializer.Serialize(
            new { active = true, scopes = Array.Empty<string>() }
        );

        ILicenseTokenClient sut = MakeClient(_ =>
        {
            Interlocked.Increment(ref callCount);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        });

        const string token = "cached-token";
        await sut.IntrospectAsync(token, CancellationToken.None);
        await sut.IntrospectAsync(token, CancellationToken.None);
        await sut.IntrospectAsync(token, CancellationToken.None);

        callCount.Should().Be(1, "subsequent calls for same token must be served from cache");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ILicenseTokenClient MakeClient(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        string? accessToken = null
    )
    {
        FakeHandler fake = new(handler);
        HttpClient http = new(fake) { BaseAddress = new("https://api.nomercy.tv/") };
        return new LicenseTokenClient(http, () => accessToken);
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
            return Task.FromResult(respond(request));
        }
    }
}
