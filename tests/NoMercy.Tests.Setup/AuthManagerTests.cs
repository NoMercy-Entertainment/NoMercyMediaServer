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

using NoMercy.NmSystem.Security;
using System.IdentityModel.Tokens.Jwt;
using NoMercy.NmSystem.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Setup.Auth;
using NoMercy.Storage.Drivers.Local;

namespace NoMercy.Tests.Setup;

[Trait(name: "Category", value: "Unit")]
public class AuthManagerTests : IDisposable
{
    private readonly AppDbContext _appContext;
    private readonly AuthManager _authManager;
    private readonly AuthTokenStore _authTokenStore = new();

    public AuthManagerTests()
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
    }

    public void Dispose()
    {
        _appContext.Database.CloseConnection();
        _appContext.Dispose();
        // Reset global access token to avoid state leaking between tests
        _authTokenStore.SetAccessToken(token: null);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static string CreateValidJwt(DateTime validTo)
    {
        JwtSecurityTokenHandler handler = new();
        // notBefore must be strictly before expires — use earliest of (now-5min, expires-1min)
        DateTime notBefore =
            validTo < DateTime.UtcNow ? validTo.AddMinutes(value: -10) : DateTime.UtcNow.AddMinutes(value: -5);
        JwtSecurityToken token = new(
            issuer: "https://auth.nomercy.tv/realms/NoMercyTV",
            audience: "nomercy-server",
            claims: [new(type: "sub", value: Guid.NewGuid().ToString())],
            notBefore: notBefore,
            expires: validTo
        );
        return handler.WriteToken(token: token);
    }

    private async Task SeedSecureValue(string key, string value)
    {
        _appContext.Configuration.Add(
            entity: new()
            {
                Key = key,
                Value = string.Empty,
                SecureValue = value,
            }
        );
        await _appContext.SaveChangesAsync();
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WaitForAuthReady_NotSignaledInitially()
    {
        using CancellationTokenSource cts = new(delay: TimeSpan.FromMilliseconds(milliseconds: 100));
        // TaskCanceledException is a subclass of OperationCanceledException — use ThrowsAnyAsync
        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            _authManager.WaitForAuthReadyAsync(ct: cts.Token)
        );
    }

    [Fact]
    public async Task InitializeAsync_NoTokens_ReturnsFalse()
    {
        bool result = await _authManager.InitializeAsync();

        Assert.False(condition: result);
    }

    [Fact]
    public async Task InitializeAsync_ValidToken_ReturnsTrue()
    {
        string jwt = CreateValidJwt(validTo: DateTime.UtcNow.AddHours(value: 2));
        await SeedSecureValue(key: "auth_access_token", value: jwt);
        await SeedSecureValue(
            key: "auth_token_metadata",
            value: $"{{\"expires_at\":\"{DateTime.UtcNow.AddHours(value: 2):O}\",\"token_type\":\"Bearer\"}}"
        );

        bool result = await _authManager.InitializeAsync();

        Assert.True(condition: result);
    }

    [Fact]
    public async Task InitializeAsync_ValidToken_SignalsAuthReady()
    {
        string jwt = CreateValidJwt(validTo: DateTime.UtcNow.AddHours(value: 2));
        await SeedSecureValue(key: "auth_access_token", value: jwt);
        await SeedSecureValue(
            key: "auth_token_metadata",
            value: $"{{\"expires_at\":\"{DateTime.UtcNow.AddHours(value: 2):O}\",\"token_type\":\"Bearer\"}}"
        );

        await _authManager.InitializeAsync();

        using CancellationTokenSource cts = new(delay: TimeSpan.FromMilliseconds(milliseconds: 500));
        // Should NOT throw — auth is ready
        await _authManager.WaitForAuthReadyAsync(ct: cts.Token);
    }

    [Fact]
    public async Task InitializeAsync_ExpiredToken_NoRefresh_ReturnsFalse()
    {
        string jwt = CreateValidJwt(validTo: DateTime.UtcNow.AddMinutes(value: -10));
        await SeedSecureValue(key: "auth_access_token", value: jwt);
        // No refresh token seeded — so TryRefreshToken cannot be attempted

        bool result = await _authManager.InitializeAsync();

        Assert.False(condition: result);
    }

    [Fact]
    public async Task StoreTokensAsync_PersistsToDb()
    {
        string jwt = CreateValidJwt(validTo: DateTime.UtcNow.AddHours(value: 1));
        DateTime expiresAt = DateTime.UtcNow.AddHours(value: 1);

        await _authManager.StoreTokensAsync(accessToken: jwt, refreshToken: "refresh-xyz", expiresAt: expiresAt, tokenType: "Bearer");

        Configuration? accessRow = await _appContext
            .Configuration.AsNoTracking()
            .FirstOrDefaultAsync(predicate: c => c.Key == "auth_access_token");
        Configuration? refreshRow = await _appContext
            .Configuration.AsNoTracking()
            .FirstOrDefaultAsync(predicate: c => c.Key == "auth_refresh_token");
        Configuration? metaRow = await _appContext
            .Configuration.AsNoTracking()
            .FirstOrDefaultAsync(predicate: c => c.Key == "auth_token_metadata");

        Assert.NotNull(@object: accessRow);
        Assert.Equal(expected: jwt, actual: accessRow.SecureValue);

        Assert.NotNull(@object: refreshRow);
        Assert.Equal(expected: "refresh-xyz", actual: refreshRow.SecureValue);

        Assert.NotNull(@object: metaRow);
        Assert.Contains(expectedSubstring: "expires_at", actualString: metaRow.SecureValue ?? string.Empty);
    }

    [Fact]
    public async Task StoreTokensAsync_SetsAccessToken()
    {
        string jwt = CreateValidJwt(validTo: DateTime.UtcNow.AddHours(value: 1));
        _authTokenStore.SetAccessToken(token: null);

        await _authManager.StoreTokensAsync(accessToken: jwt, refreshToken: null, expiresAt: DateTime.UtcNow.AddHours(value: 1), tokenType: "Bearer");

        Assert.Equal(expected: jwt, actual: _authTokenStore.AccessToken);
    }

    [Fact]
    public async Task StoreTokensAsync_SignalsAuthReady()
    {
        string jwt = CreateValidJwt(validTo: DateTime.UtcNow.AddHours(value: 1));

        await _authManager.StoreTokensAsync(accessToken: jwt, refreshToken: null, expiresAt: DateTime.UtcNow.AddHours(value: 1), tokenType: "Bearer");

        using CancellationTokenSource cts = new(delay: TimeSpan.FromMilliseconds(milliseconds: 500));
        // Should NOT throw — auth is ready after store
        await _authManager.WaitForAuthReadyAsync(ct: cts.Token);
    }

    [Fact]
    public void GenerateCodeVerifier_IsBase64UrlSafe()
    {
        string verifier = AuthManager.GenerateCodeVerifier();

        // Base64url chars: A-Z a-z 0-9 - _   (no + / =)
        Assert.DoesNotContain(expectedSubstring: "+", actualString: verifier);
        Assert.DoesNotContain(expectedSubstring: "/", actualString: verifier);
        Assert.DoesNotContain(expectedSubstring: "=", actualString: verifier);
    }

    [Fact]
    public void GenerateCodeVerifier_HasMinLength43()
    {
        string verifier = AuthManager.GenerateCodeVerifier();

        // 32 bytes → 43 base64url chars (without padding)
        Assert.True(condition: verifier.Length >= 43, userMessage: $"Expected length >= 43 but got {verifier.Length}");
    }

    [Fact]
    public void GenerateCodeChallenge_MatchesRfc7636TestVector()
    {
        // RFC 7636 Appendix B test vector
        string knownVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        string expectedChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        string challenge = AuthManager.GenerateCodeChallenge(codeVerifier: knownVerifier);

        Assert.Equal(expected: expectedChallenge, actual: challenge);
    }

    [Fact]
    public void BuildAuthorizationCodeBody_ContainsAllFields()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildAuthorizationCodeBody(
            clientId: "my-client",
            code: "auth-code-123",
            redirectUri: "http://localhost:7626/sso-callback",
            codeVerifier: "my-verifier"
        );

        Dictionary<string, string> dict = body.ToDictionary(keySelector: kv => kv.Key, elementSelector: kv => kv.Value);

        Assert.Equal(expected: "authorization_code", actual: dict[key: "grant_type"]);
        Assert.Equal(expected: "my-client", actual: dict[key: "client_id"]);
        Assert.Equal(expected: "auth-code-123", actual: dict[key: "code"]);
        Assert.Equal(expected: "http://localhost:7626/sso-callback", actual: dict[key: "redirect_uri"]);
        Assert.Equal(expected: "my-verifier", actual: dict[key: "code_verifier"]);
        Assert.Contains(expectedSubstring: "openid", actualString: dict[key: "scope"]);
    }

    [Fact]
    public void BuildRefreshTokenBody_ContainsRefreshToken()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildRefreshTokenBody(
            clientId: "my-client",
            refreshToken: "my-refresh-token"
        );

        Dictionary<string, string> dict = body.ToDictionary(keySelector: kv => kv.Key, elementSelector: kv => kv.Value);

        Assert.Equal(expected: "refresh_token", actual: dict[key: "grant_type"]);
        Assert.Equal(expected: "my-client", actual: dict[key: "client_id"]);
        Assert.Equal(expected: "my-refresh-token", actual: dict[key: "refresh_token"]);
        Assert.Contains(expectedSubstring: "openid", actualString: dict[key: "scope"]);
    }
}
