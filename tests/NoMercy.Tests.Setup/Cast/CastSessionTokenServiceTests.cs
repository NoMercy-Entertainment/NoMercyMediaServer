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

using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Security;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Cast;
using NoMercy.Setup.Dto;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Tests.Setup.Infrastructure;

namespace NoMercy.Tests.Setup.Cast;

/// <summary>
/// Requirement: minting a cast-session token bundle must refuse to proceed without a
/// server access token, must never mint against a subject token issued by a different
/// Keycloak realm than the one currently configured (a stale/foreign JWT must not be
/// exchanged for cast-receiver-scoped tokens), and must return null — never throw —
/// on every failure so a cast launch degrades to "no session" instead of crashing.
/// </summary>
/// <remarks>
/// <see cref="CastSessionTokenService.MintAsync"/> unconditionally calls
/// <see cref="AuthManager.RefreshAsync"/> first (production behavior — it keeps the
/// subject token fresh before exchanging it), which itself hits the SAME Keycloak token
/// endpoint. Every test here seeds a DB refresh token and routes the loopback server by
/// <c>grant_type</c> in the request body so the refresh leg and the token-exchange leg
/// can be scripted independently, mirroring the two real HTTP calls MintAsync makes.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class CastSessionTokenServiceTests : IDisposable
{
    private readonly AppDbContext _appContext;
    private readonly AuthManager _authManager;
    private readonly AuthTokenStore _authTokenStore = new();

    public CastSessionTokenServiceTests()
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        ServiceProvider provider = services.BuildServiceProvider();
        TokenStore.Initialize(provider);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=:memory:");
        _appContext = new(optionsBuilder.Options);
        _appContext.Database.OpenConnection();
        _appContext.Database.EnsureCreated();

        _authManager = new(_appContext, new LocalStorageDriver(), _authTokenStore);
    }

    public void Dispose()
    {
        _appContext.Database.CloseConnection();
        _appContext.Dispose();
        _authTokenStore.SetAccessToken(null);
    }

    private async Task SeedRefreshToken(string value)
    {
        // Must go through EF's Add/SaveChanges so the SecureValue column's encrypt
        // value-converter runs — a raw INSERT would leave plaintext where LoadSecureValue
        // expects ciphertext, and TokenStore.DecryptToken would fail to unprotect it,
        // silently reading back as "no refresh token" instead of the seeded value.
        _appContext.Configuration.Add(
            new()
            {
                Key = "auth_refresh_token",
                Value = string.Empty,
                SecureValue = value,
            }
        );
        await _appContext.SaveChangesAsync();
    }

    private static string CreateJwt(
        string issuer = "https://auth.nomercy.tv/realms/NoMercyTV",
        DateTime? validTo = null,
        string azp = "nomercy-server"
    )
    {
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken token = new(
            issuer: issuer,
            audience: "nomercy-server",
            claims: [new("sub", Guid.NewGuid().ToString()), new("azp", azp)],
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: validTo ?? DateTime.UtcNow.AddHours(1)
        );
        return handler.WriteToken(token);
    }

    /// <summary>Dispatches the shared token endpoint by grant_type in the form body.</summary>
    private static LoopbackResponse RouteByGrantType(
        LoopbackRequest req,
        Func<LoopbackRequest, LoopbackResponse>? onRefresh = null,
        Func<LoopbackRequest, LoopbackResponse>? onExchange = null
    )
    {
        bool isExchange = req.Body.Contains("token-exchange");
        return isExchange
            ? onExchange?.Invoke(req) ?? new(404, "no exchange handler configured")
            : onRefresh?.Invoke(req) ?? new(404, "no refresh handler configured");
    }

    private static string RefreshSuccessJson(
        string accessToken,
        string refreshToken = "new-refresh"
    ) =>
        JsonConvert.SerializeObject(
            new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = "Bearer",
                ExpiresIn = 3600,
            }
        );

    [Fact]
    public async Task MintAsync_NoServerAccessToken_ReturnsNull()
    {
        CastSessionTokenService service = new(_authManager, _authTokenStore);

        LaunchCustomData? result = await service.MintAsync(
            Guid.NewGuid(),
            "server-1",
            "https://server1.nomercy.app",
            Ulid.NewUlid(),
            CastIntent.Idle()
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task MintAsync_RefreshDropsAccessToken_ReturnsNull()
    {
        // No refresh token in the DB: RefreshAsync's own no-refresh-token branch clears
        // the access token — MintAsync's second guard must catch that and return null.
        _authTokenStore.SetAccessToken(CreateJwt());

        CastSessionTokenService service = new(_authManager, _authTokenStore);

        LaunchCustomData? result = await service.MintAsync(
            Guid.NewGuid(),
            "server-1",
            "https://server1.nomercy.app",
            Ulid.NewUlid(),
            CastIntent.Idle()
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task MintAsync_IssuerMismatch_ReturnsNullWithoutCallingExchange()
    {
        await SeedRefreshToken("some-refresh-token");
        string foreignJwt = CreateJwt(issuer: "https://auth.example.com/realms/Other");
        // MintAsync's first guard requires a non-empty CURRENT access token before it
        // even attempts a refresh — the placeholder here is immediately overwritten by
        // the mocked refresh response below, but must be non-empty to reach that call.
        _authTokenStore.SetAccessToken("placeholder-pre-refresh-token");

        bool exchangeCalled = false;
        using LoopbackHttpServer server = new();
        server.Handler = req =>
            RouteByGrantType(
                req,
                onRefresh: _ => new(200, RefreshSuccessJson(foreignJwt)),
                onExchange: _ =>
                {
                    exchangeCalled = true;
                    return new(200, "{}");
                }
            );
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        CastSessionTokenService service = new(_authManager, _authTokenStore);

        LaunchCustomData? result = await service.MintAsync(
            Guid.NewGuid(),
            "server-1",
            "https://server1.nomercy.app",
            Ulid.NewUlid(),
            CastIntent.Idle()
        );

        Assert.Null(result);
        Assert.False(
            exchangeCalled,
            "a foreign-issuer subject token must never reach token-exchange"
        );
    }

    [Fact]
    public async Task MintAsync_TokenExchangeSucceeds_ReturnsPopulatedBundle()
    {
        await SeedRefreshToken("some-refresh-token");
        _authTokenStore.SetAccessToken("placeholder-pre-refresh-token");

        using LoopbackHttpServer server = new();
        // The refreshed subject token's issuer must match the CONFIGURED realm — which
        // the scope below repoints at this loopback server — so the exchange leg is
        // actually reached (a real Keycloak issuer would never equal a loopback URL).
        string subjectJwt = CreateJwt(issuer: server.BaseUrl);
        server.Handler = req =>
            RouteByGrantType(
                req,
                onRefresh: _ => new(200, RefreshSuccessJson(subjectJwt)),
                onExchange: _ =>
                    new(
                        200,
                        RefreshSuccessJson(
                            "exchanged-access-token",
                            refreshToken: "exchanged-refresh-token"
                        )
                    )
            );
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        CastSessionTokenService service = new(_authManager, _authTokenStore);
        Guid userId = Guid.NewGuid();
        Ulid deviceId = Ulid.NewUlid();

        LaunchCustomData? result = await service.MintAsync(
            userId,
            "server-42",
            "https://server42.nomercy.app",
            deviceId,
            CastIntent.PlayVideo("movie", "603"),
            clientLocale: "nl-NL"
        );

        Assert.NotNull(result);
        Assert.Equal("exchanged-access-token", result!.AccessToken);
        Assert.Equal("exchanged-refresh-token", result.RefreshToken);
        Assert.Equal(userId.ToString(), result.UserId);
        Assert.Equal("server-42", result.ServerId);
        Assert.Equal("https://server42.nomercy.app", result.ServerUrl);
        Assert.Equal(deviceId.ToString(), result.DeviceId);
        Assert.Equal("nl-NL", result.ClientLocale);
        Assert.Equal("play_video", result.Intent.Type);
        Assert.NotEmpty(result.CastSessionId);
    }

    [Fact]
    public async Task MintAsync_TokenExchangeReturnsNoRefreshToken_ReturnsNull()
    {
        await SeedRefreshToken("some-refresh-token");
        _authTokenStore.SetAccessToken("placeholder-pre-refresh-token");

        using LoopbackHttpServer server = new();
        string subjectJwt = CreateJwt(issuer: server.BaseUrl);
        server.Handler = req =>
            RouteByGrantType(
                req,
                onRefresh: _ => new(200, RefreshSuccessJson(subjectJwt)),
                onExchange: _ =>
                    new(
                        200,
                        JsonConvert.SerializeObject(
                            new AuthResponse { AccessToken = "exchanged", RefreshToken = null }
                        )
                    )
            );
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        CastSessionTokenService service = new(_authManager, _authTokenStore);

        LaunchCustomData? result = await service.MintAsync(
            Guid.NewGuid(),
            "server-1",
            "https://server1.nomercy.app",
            Ulid.NewUlid(),
            CastIntent.Idle()
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task MintAsync_TokenExchangeHttpFailure_ReturnsNull()
    {
        await SeedRefreshToken("some-refresh-token");
        _authTokenStore.SetAccessToken("placeholder-pre-refresh-token");

        using LoopbackHttpServer server = new();
        string subjectJwt = CreateJwt(issuer: server.BaseUrl);
        server.Handler = req =>
            RouteByGrantType(
                req,
                onRefresh: _ => new(200, RefreshSuccessJson(subjectJwt)),
                onExchange: _ => new(403, "{\"error\":\"not_allowed\"}")
            );
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        CastSessionTokenService service = new(_authManager, _authTokenStore);

        LaunchCustomData? result = await service.MintAsync(
            Guid.NewGuid(),
            "server-1",
            "https://server1.nomercy.app",
            Ulid.NewUlid(),
            CastIntent.Idle()
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task MintAsync_ExchangeConnectionAborted_ReturnsNullWithoutThrowing()
    {
        // The refresh leg must succeed (so the exchange leg is actually reached) while
        // the exchange leg itself hits a real network-level failure — exercising
        // RequestTokenExchangeAsync's own catch(Exception) branch specifically, distinct
        // from a well-formed non-2xx response (see MintAsync_TokenExchangeHttpFailure).
        await SeedRefreshToken("some-refresh-token");
        _authTokenStore.SetAccessToken("placeholder-pre-refresh-token");

        using LoopbackHttpServer server = new();
        string subjectJwt = CreateJwt(issuer: server.BaseUrl);
        server.Handler = req =>
            RouteByGrantType(
                req,
                onRefresh: _ => new(200, RefreshSuccessJson(subjectJwt)),
                onExchange: _ => LoopbackResponse.Aborted()
            );
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        CastSessionTokenService service = new(_authManager, _authTokenStore);

        LaunchCustomData? result = await service.MintAsync(
            Guid.NewGuid(),
            "server-1",
            "https://server1.nomercy.app",
            Ulid.NewUlid(),
            CastIntent.Idle()
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task MintAsync_RefreshLegNetworkUnreachable_ReturnsNullWithoutThrowing()
    {
        // A refresh token exists in the DB (so RefreshAsync actually attempts the HTTP
        // call) but the endpoint is entirely unreachable — proves the whole MintAsync
        // pipeline degrades to null through AuthManager's own exception handling too,
        // not just CastSessionTokenService's.
        await SeedRefreshToken("some-refresh-token");
        _authTokenStore.SetAccessToken(CreateJwt());

        using ExternalServicesConfigScope scope = new(authBaseUrl: "http://127.0.0.1:1/");

        CastSessionTokenService service = new(_authManager, _authTokenStore);

        LaunchCustomData? result = await service.MintAsync(
            Guid.NewGuid(),
            "server-1",
            "https://server1.nomercy.app",
            Ulid.NewUlid(),
            CastIntent.Idle()
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task MintAsync_UnparsableSubjectToken_FailsClosedWithoutThrowing()
    {
        // A subject token that isn't a real JWT: ResolveRequestingClientId/
        // IssuerMatchesConfiguredRealm both catch and fall back (empty issuer never
        // matches the configured realm) — the mint must still fail closed, not throw.
        await SeedRefreshToken("some-refresh-token");
        _authTokenStore.SetAccessToken("placeholder-pre-refresh-token");

        using LoopbackHttpServer server = new();
        server.Handler = req =>
            RouteByGrantType(req, onRefresh: _ => new(200, RefreshSuccessJson("not-a-jwt-at-all")));
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        CastSessionTokenService service = new(_authManager, _authTokenStore);

        LaunchCustomData? result = await service.MintAsync(
            Guid.NewGuid(),
            "server-1",
            "https://server1.nomercy.app",
            Ulid.NewUlid(),
            CastIntent.Idle()
        );

        Assert.Null(result);
    }
}
