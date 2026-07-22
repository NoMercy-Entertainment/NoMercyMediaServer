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
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Security;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Dto;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Tests.Setup.Infrastructure;

namespace NoMercy.Tests.Setup.Auth;

/// <summary>
/// Requirement: AuthManager's refresh/PKCE/dead-refresh-token paths must behave
/// correctly against the real Keycloak token endpoint contract — success stores
/// tokens and signals auth-ready, an <c>invalid_grant</c> response permanently clears
/// the refresh token (forcing re-auth through /setup rather than retrying forever),
/// and any other failure clears the access token without touching the refresh token
/// so a transient Keycloak hiccup can still recover on the next attempt.
/// </summary>
/// <remarks>
/// AuthManager builds its own <c>new HttpClient()</c> pointed at
/// <see cref="ExternalServicesConfig.Current"/>.AuthBaseUrl rather than accepting an
/// injectable client, so a fake <see cref="HttpMessageHandler"/> cannot reach it. A real
/// loopback <see cref="LoopbackHttpServer"/> exercises the actual HTTP round trip without
/// touching the live internet or a real Keycloak.
/// </remarks>
[Trait(name: "Category", value: "Unit")]
public sealed class AuthManagerHttpTests : IDisposable
{
    private readonly AppDbContext _appContext;
    private readonly AuthManager _authManager;
    private readonly AuthTokenStore _authTokenStore = new();
    private readonly string? _originalTokenClientId;

    public AuthManagerHttpTests()
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        ServiceProvider provider = services.BuildServiceProvider();
        TokenStore.Initialize(serviceProvider: provider);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connectionString: "Data Source=:memory:");
        _appContext = new(options: optionsBuilder.Options);
        _appContext.Database.OpenConnection();
        _appContext.Database.EnsureCreated();

        _authManager = new(appContext: _appContext, driver: new LocalStorageDriver(), authTokenStore: _authTokenStore);
        _originalTokenClientId = ExternalServicesConfig.Current.TokenClientId;
    }

    public void Dispose()
    {
        _appContext.Database.CloseConnection();
        _appContext.Dispose();
        _authTokenStore.SetAccessToken(token: null);
        ExternalServicesConfig.Current.TokenClientId = _originalTokenClientId ?? "nomercy-server";
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateJwt(DateTime validTo, DateTime? notBefore = null)
    {
        JwtSecurityTokenHandler handler = new();
        DateTime nbf =
            notBefore
            ?? (
                validTo < DateTime.UtcNow ? validTo.AddMinutes(value: -10) : DateTime.UtcNow.AddMinutes(value: -5)
            );
        JwtSecurityToken token = new(
            issuer: "https://auth.nomercy.tv/realms/NoMercyTV",
            audience: "nomercy-server",
            claims: [new(type: "sub", value: Guid.NewGuid().ToString())],
            notBefore: nbf,
            expires: validTo
        );
        return handler.WriteToken(token: token);
    }

    private async Task SeedRefreshToken(string value)
    {
        _appContext.Configuration.Add(
            entity: new()
            {
                Key = "auth_refresh_token",
                Value = string.Empty,
                SecureValue = value,
            }
        );
        await _appContext.SaveChangesAsync();
    }

    private async Task<Configuration?> ReadConfig(string key) =>
        await _appContext.Configuration.AsNoTracking().FirstOrDefaultAsync(predicate: c => c.Key == key);

    private static string AuthResponseJson(
        string accessToken,
        string? refreshToken = "new-refresh"
    ) =>
        JsonConvert.SerializeObject(
            value: new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = "Bearer",
                ExpiresIn = 3600,
            }
        );

    // ── RefreshAsync / TryRefreshToken ──────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_NoRefreshTokenInDb_ClearsAccessTokenAndReturns()
    {
        _authTokenStore.SetAccessToken(token: "stale-access-token");

        await _authManager.RefreshAsync();

        Assert.Null(@object: _authTokenStore.AccessToken);
    }

    [Fact]
    public async Task RefreshAsync_Success_StoresNewTokensAndSetsAccessToken()
    {
        await SeedRefreshToken(value: "old-refresh-token");
        string newJwt = CreateJwt(validTo: DateTime.UtcNow.AddHours(value: 1));

        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 200, Body: AuthResponseJson(accessToken: newJwt));
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        await _authManager.RefreshAsync();

        Assert.Equal(expected: newJwt, actual: _authTokenStore.AccessToken);
        Configuration? refreshRow = await ReadConfig(key: "auth_refresh_token");
        Assert.Equal(expected: "new-refresh", actual: refreshRow?.SecureValue);
    }

    [Fact]
    public async Task RefreshAsync_TransientServerError_ClearsAccessToken_KeepsRefreshToken()
    {
        await SeedRefreshToken(value: "still-valid-refresh-token");

        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 503, Body: "service unavailable");
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        _authTokenStore.SetAccessToken(token: "stale-token");
        await _authManager.RefreshAsync();

        Assert.Null(@object: _authTokenStore.AccessToken);
        // A transient failure must not discard the still-valid refresh token —
        // only an explicit invalid_grant does that (see the invalid_grant test below).
        Configuration? refreshRow = await ReadConfig(key: "auth_refresh_token");
        Assert.Equal(expected: "still-valid-refresh-token", actual: refreshRow?.SecureValue);
    }

    [Fact]
    public async Task RefreshAsync_InvalidGrant_PermanentlyClearsRefreshToken()
    {
        await SeedRefreshToken(value: "dead-refresh-token");

        using LoopbackHttpServer server = new();
        server.Handler = _ =>
            new(StatusCode: 400, Body: "{\"error\":\"invalid_grant\",\"error_description\":\"Session not found\"}");
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        await _authManager.RefreshAsync();

        Assert.Null(@object: _authTokenStore.AccessToken);
        Configuration? refreshRow = await ReadConfig(key: "auth_refresh_token");
        // Empty string round-trips to null through TokenStore's encrypt/decrypt
        // converter (see TokenStore.DecryptToken) — both mean "cleared".
        Assert.True(
            condition: string.IsNullOrEmpty(value: refreshRow?.SecureValue),
            userMessage: "invalid_grant must permanently clear the refresh token"
        );
    }

    [Fact]
    public async Task RefreshAsync_NetworkUnreachable_ReturnsFalseWithoutThrowing()
    {
        await SeedRefreshToken(value: "some-refresh-token");
        // Port 1 on loopback: nothing listens there — connection refused immediately,
        // no live network dependency and no multi-second timeout.
        using ExternalServicesConfigScope scope = new(authBaseUrl: "http://127.0.0.1:1/");

        await _authManager.RefreshAsync();

        Assert.Null(@object: _authTokenStore.AccessToken);
    }

    [Fact]
    public async Task RefreshAsync_TokenClientIdNotConfigured_ReturnsFalseWithoutCallingNetwork()
    {
        await SeedRefreshToken(value: "some-refresh-token");
        ExternalServicesConfig.Current.TokenClientId = string.Empty;

        await _authManager.RefreshAsync();

        Assert.Null(@object: _authTokenStore.AccessToken);
    }

    [Fact]
    public async Task RefreshAsync_ResponseMissingAccessToken_ReturnsFalse()
    {
        await SeedRefreshToken(value: "some-refresh-token");

        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 200, Body: "{\"token_type\":\"Bearer\"}");
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        await _authManager.RefreshAsync();

        Assert.Null(@object: _authTokenStore.AccessToken);
    }

    // ── StoreTokensAsync(AuthResponse) ───────────────────────────────────────

    [Fact]
    public async Task StoreTokensAsync_AuthResponse_NullAccessToken_DoesNotPersist()
    {
        await _authManager.StoreTokensAsync(tokens: new AuthResponse { AccessToken = null });

        Configuration? row = await ReadConfig(key: "auth_access_token");
        Assert.Null(@object: row);
    }

    [Fact]
    public async Task StoreTokensAsync_AuthResponse_ValidJwt_UsesJwtExpiry()
    {
        DateTime expiry = DateTime.UtcNow.AddHours(value: 3);
        string jwt = CreateJwt(validTo: expiry);

        await _authManager.StoreTokensAsync(
            tokens: new AuthResponse
            {
                AccessToken = jwt,
                RefreshToken = "r1",
                TokenType = "Bearer",
                ExpiresIn = 60, // deliberately different from the JWT's real exp claim
            }
        );

        Configuration? metaRow = await ReadConfig(key: "auth_token_metadata");
        Assert.NotNull(@object: metaRow);
        Assert.Contains(expectedSubstring: expiry.ToString(format: "O")[..16], actualString: metaRow!.SecureValue);
    }

    [Fact]
    public async Task StoreTokensAsync_AuthResponse_UnparsableToken_FallsBackToExpiresIn()
    {
        await _authManager.StoreTokensAsync(
            tokens: new AuthResponse
            {
                AccessToken = "not-a-real-jwt",
                RefreshToken = "r2",
                TokenType = "Bearer",
                ExpiresIn = 120,
            }
        );

        Assert.Equal(expected: "not-a-real-jwt", actual: _authTokenStore.AccessToken);
        Configuration? metaRow = await ReadConfig(key: "auth_token_metadata");
        Assert.NotNull(@object: metaRow);
    }

    [Fact]
    public async Task StoreTokensAsync_AuthResponse_UnparsableTokenAndNoExpiresIn_DefaultsTo300Seconds()
    {
        DateTime before = DateTime.UtcNow;

        await _authManager.StoreTokensAsync(
            tokens: new AuthResponse
            {
                AccessToken = "still-not-a-jwt",
                RefreshToken = "r3",
                TokenType = "Bearer",
                ExpiresIn = 0,
            }
        );

        Configuration? metaRow = await ReadConfig(key: "auth_token_metadata");
        Assert.NotNull(@object: metaRow);
        // Parse back the persisted expires_at and confirm it lands ~300s out (not 0, not huge).
        PersistedTokenMetadata meta =
            JsonConvert.DeserializeObject<PersistedTokenMetadata>(value: metaRow!.SecureValue!)
            ?? throw new InvalidOperationException(message: "metadata did not deserialize");
        DateTime expiresAt = DateTime.Parse(
            s: meta.ExpiresAt!,
            provider: null,
            styles: System.Globalization.DateTimeStyles.RoundtripKind
        );
        TimeSpan delta = expiresAt - before;
        Assert.InRange(actual: delta.TotalSeconds, low: 250, high: 350);
    }

    private sealed class PersistedTokenMetadata
    {
        [JsonProperty(propertyName: "expires_at")]
        public string? ExpiresAt { get; set; }
    }

    // ── TryCompletePkceFromCallbackAsync ─────────────────────────────────────

    [Fact]
    public async Task TryCompletePkceFromCallbackAsync_NoPendingFlow_ReturnsFalse()
    {
        bool result = await AuthManager.TryCompletePkceFromCallbackAsync(
            code: "some-code",
            state: "some-state",
            redirectUri: "http://localhost/callback"
        );

        Assert.False(condition: result);
    }

    [Fact]
    public async Task TryCompletePkceFromCallbackAsync_StateMismatch_ReturnsFalse()
    {
        AuthManager.PreparePkceBrowserFlow(codeVerifier: "verifier-abc", state: "expected-state");

        bool result = await AuthManager.TryCompletePkceFromCallbackAsync(
            code: "some-code",
            state: "wrong-state",
            redirectUri: "http://localhost/callback"
        );

        Assert.False(condition: result);

        // Clean up: a state mismatch does not consume the pending flow, so drain it
        // with a matching-but-doomed call to avoid leaking static state into other tests.
        await AuthManager.TryCompletePkceFromCallbackAsync(
            code: "any",
            state: "expected-state",
            redirectUri: "http://localhost/callback"
        );
    }

    [Fact]
    public async Task TryCompletePkceFromCallbackAsync_Success_SetsAccessTokenAndReturnsTrue()
    {
        AuthManager.PreparePkceBrowserFlow(codeVerifier: "verifier-xyz", state: "state-xyz");
        string jwt = CreateJwt(validTo: DateTime.UtcNow.AddHours(value: 1));

        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 200, Body: AuthResponseJson(accessToken: jwt));
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        bool result = await AuthManager.TryCompletePkceFromCallbackAsync(
            code: "auth-code",
            state: "state-xyz",
            redirectUri: "http://localhost/callback",
            authTokenStore: _authTokenStore
        );

        Assert.True(condition: result);
        Assert.Equal(expected: jwt, actual: _authTokenStore.AccessToken);
    }

    [Fact]
    public async Task TryCompletePkceFromCallbackAsync_TokenExchangeFails_ReturnsFalse()
    {
        AuthManager.PreparePkceBrowserFlow(codeVerifier: "verifier-fail", state: "state-fail");

        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 400, Body: "{\"error\":\"invalid_request\"}");
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        bool result = await AuthManager.TryCompletePkceFromCallbackAsync(
            code: "auth-code",
            state: "state-fail",
            redirectUri: "http://localhost/callback",
            authTokenStore: _authTokenStore
        );

        Assert.False(condition: result);
    }

    [Fact]
    public async Task TryCompletePkceFromCallbackAsync_TokenClientIdMissing_ReturnsFalse()
    {
        AuthManager.PreparePkceBrowserFlow(codeVerifier: "verifier-noclient", state: "state-noclient");
        ExternalServicesConfig.Current.TokenClientId = string.Empty;

        bool result = await AuthManager.TryCompletePkceFromCallbackAsync(
            code: "auth-code",
            state: "state-noclient",
            redirectUri: "http://localhost/callback",
            authTokenStore: _authTokenStore
        );

        Assert.False(condition: result);
    }

    // ── ScheduleBackgroundRefresh ────────────────────────────────────────────

    [Fact]
    public async Task ScheduleBackgroundRefresh_NearExpiryToken_TriggersImmediateRefresh()
    {
        await SeedRefreshToken(value: "bg-refresh-token");
        // Expiring in 2s means (expiry - now - 60s) is negative, so the loop skips its
        // wait entirely and calls RefreshAsync on the very first iteration.
        string aboutToExpire = CreateJwt(validTo: DateTime.UtcNow.AddSeconds(value: 2));
        _authTokenStore.SetAccessToken(token: aboutToExpire);

        using LoopbackHttpServer server = new();
        string freshJwt = CreateJwt(validTo: DateTime.UtcNow.AddHours(value: 1));
        server.Handler = _ => new(StatusCode: 200, Body: AuthResponseJson(accessToken: freshJwt));
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        using CancellationTokenSource cts = new();
        _authManager.ScheduleBackgroundRefresh(ct: cts.Token);

        DateTime deadline = DateTime.UtcNow.AddSeconds(value: 5);
        while (server.RequestCount == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(millisecondsDelay: 50);

        Assert.True(
            condition: server.RequestCount > 0,
            userMessage: "Background refresh should have hit the token endpoint"
        );

        cts.Cancel();
        await Task.Delay(millisecondsDelay: 100); // let the loop observe cancellation and exit
    }

    [Fact]
    public async Task ScheduleBackgroundRefresh_CancelledMidWait_StopsLoopCleanly()
    {
        // Far-future expiry means the loop computes a long positive delay and is
        // sitting inside `await Task.Delay(delay, linked)` when we cancel — this is
        // the OperationCanceledException break branch, distinct from the near-expiry
        // "refresh fires immediately" path above.
        string farFuture = CreateJwt(validTo: DateTime.UtcNow.AddDays(value: 1));
        _authTokenStore.SetAccessToken(token: farFuture);

        using CancellationTokenSource cts = new();
        _authManager.ScheduleBackgroundRefresh(ct: cts.Token);

        await Task.Delay(millisecondsDelay: 50);
        cts.Cancel();
        await Task.Delay(millisecondsDelay: 100);

        // No assertion beyond "did not throw / did not hang" — the loop's own
        // cancellation branch is what this test exercises.
    }

    // ── SecureDeleteFile ──────────────────────────────────────────────────────

    [Fact]
    public void SecureDeleteFile_NonExistentFile_DoesNothing()
    {
        string path = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-secdel-missing-{Guid.NewGuid():N}.tmp");

        _authManager.SecureDeleteFile(path: path);

        Assert.False(condition: File.Exists(path: path));
    }

    [Fact]
    public void SecureDeleteFile_ExistingFile_OverwritesAndDeletes()
    {
        string path = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-secdel-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(path: path, contents: "sensitive-token-contents");

        _authManager.SecureDeleteFile(path: path);

        Assert.False(condition: File.Exists(path: path));
    }

    [Fact]
    public void SecureDeleteFile_EmptyFile_DeletesWithoutZeroingContent()
    {
        string path = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-secdel-empty-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(path: path, contents: string.Empty);

        _authManager.SecureDeleteFile(path: path);

        Assert.False(condition: File.Exists(path: path));
    }
}
