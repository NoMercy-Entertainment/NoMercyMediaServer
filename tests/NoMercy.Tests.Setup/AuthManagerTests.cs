using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Setup;

namespace NoMercy.Tests.Setup;

[Trait("Category", "Unit")]
public class AuthManagerTests : IDisposable
{
    private readonly AppDbContext _appContext;
    private readonly AuthManager _authManager;

    public AuthManagerTests()
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        ServiceProvider provider = services.BuildServiceProvider();
        TokenStore.Initialize(provider);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=:memory:");
        _appContext = new AppDbContext(optionsBuilder.Options);
        _appContext.Database.OpenConnection();
        _appContext.Database.EnsureCreated();

        _authManager = new AuthManager(_appContext);
    }

    public void Dispose()
    {
        _appContext.Database.CloseConnection();
        _appContext.Dispose();
        // Reset global access token to avoid state leaking between tests
        Globals.Globals.AccessToken = null;
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static string CreateValidJwt(DateTime validTo)
    {
        JwtSecurityTokenHandler handler = new();
        // notBefore must be strictly before expires — use earliest of (now-5min, expires-1min)
        DateTime notBefore =
            validTo < DateTime.UtcNow ? validTo.AddMinutes(-10) : DateTime.UtcNow.AddMinutes(-5);
        JwtSecurityToken token = new(
            issuer: "https://auth.nomercy.tv/realms/NoMercyTV",
            audience: "nomercy-server",
            claims: [new Claim("sub", Guid.NewGuid().ToString())],
            notBefore: notBefore,
            expires: validTo
        );
        return handler.WriteToken(token);
    }

    private async Task SeedSecureValue(string key, string value)
    {
        _appContext.Configuration.Add(
            new Configuration
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
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(100));
        // TaskCanceledException is a subclass of OperationCanceledException — use ThrowsAnyAsync
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _authManager.WaitForAuthReadyAsync(cts.Token)
        );
    }

    [Fact]
    public async Task InitializeAsync_NoTokens_ReturnsFalse()
    {
        bool result = await _authManager.InitializeAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task InitializeAsync_ValidToken_ReturnsTrue()
    {
        string jwt = CreateValidJwt(DateTime.UtcNow.AddHours(2));
        await SeedSecureValue("auth_access_token", jwt);
        await SeedSecureValue(
            "auth_token_metadata",
            $"{{\"expires_at\":\"{DateTime.UtcNow.AddHours(2):O}\",\"token_type\":\"Bearer\"}}"
        );

        bool result = await _authManager.InitializeAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task InitializeAsync_ValidToken_SignalsAuthReady()
    {
        string jwt = CreateValidJwt(DateTime.UtcNow.AddHours(2));
        await SeedSecureValue("auth_access_token", jwt);
        await SeedSecureValue(
            "auth_token_metadata",
            $"{{\"expires_at\":\"{DateTime.UtcNow.AddHours(2):O}\",\"token_type\":\"Bearer\"}}"
        );

        await _authManager.InitializeAsync();

        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(500));
        // Should NOT throw — auth is ready
        await _authManager.WaitForAuthReadyAsync(cts.Token);
    }

    [Fact]
    public async Task InitializeAsync_ExpiredToken_NoRefresh_ReturnsFalse()
    {
        string jwt = CreateValidJwt(DateTime.UtcNow.AddMinutes(-10));
        await SeedSecureValue("auth_access_token", jwt);
        // No refresh token seeded — so TryRefreshToken cannot be attempted

        bool result = await _authManager.InitializeAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task StoreTokensAsync_PersistsToDb()
    {
        string jwt = CreateValidJwt(DateTime.UtcNow.AddHours(1));
        DateTime expiresAt = DateTime.UtcNow.AddHours(1);

        await _authManager.StoreTokensAsync(jwt, "refresh-xyz", expiresAt, "Bearer");

        Configuration? accessRow = await _appContext
            .Configuration.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "auth_access_token");
        Configuration? refreshRow = await _appContext
            .Configuration.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "auth_refresh_token");
        Configuration? metaRow = await _appContext
            .Configuration.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "auth_token_metadata");

        Assert.NotNull(accessRow);
        Assert.Equal(jwt, accessRow.SecureValue);

        Assert.NotNull(refreshRow);
        Assert.Equal("refresh-xyz", refreshRow.SecureValue);

        Assert.NotNull(metaRow);
        Assert.Contains("expires_at", metaRow.SecureValue ?? string.Empty);
    }

    [Fact]
    public async Task StoreTokensAsync_SetsGlobalsAccessToken()
    {
        string jwt = CreateValidJwt(DateTime.UtcNow.AddHours(1));
        Globals.Globals.AccessToken = null;

        await _authManager.StoreTokensAsync(jwt, null, DateTime.UtcNow.AddHours(1), "Bearer");

        Assert.Equal(jwt, Globals.Globals.AccessToken);
    }

    [Fact]
    public async Task StoreTokensAsync_SignalsAuthReady()
    {
        string jwt = CreateValidJwt(DateTime.UtcNow.AddHours(1));

        await _authManager.StoreTokensAsync(jwt, null, DateTime.UtcNow.AddHours(1), "Bearer");

        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(500));
        // Should NOT throw — auth is ready after store
        await _authManager.WaitForAuthReadyAsync(cts.Token);
    }

    [Fact]
    public void GenerateCodeVerifier_IsBase64UrlSafe()
    {
        string verifier = AuthManager.GenerateCodeVerifier();

        // Base64url chars: A-Z a-z 0-9 - _   (no + / =)
        Assert.DoesNotContain("+", verifier);
        Assert.DoesNotContain("/", verifier);
        Assert.DoesNotContain("=", verifier);
    }

    [Fact]
    public void GenerateCodeVerifier_HasMinLength43()
    {
        string verifier = AuthManager.GenerateCodeVerifier();

        // 32 bytes → 43 base64url chars (without padding)
        Assert.True(verifier.Length >= 43, $"Expected length >= 43 but got {verifier.Length}");
    }

    [Fact]
    public void GenerateCodeChallenge_MatchesRfc7636TestVector()
    {
        // RFC 7636 Appendix B test vector
        string knownVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        string expectedChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        string challenge = AuthManager.GenerateCodeChallenge(knownVerifier);

        Assert.Equal(expectedChallenge, challenge);
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

        Dictionary<string, string> dict = body.ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Equal("authorization_code", dict["grant_type"]);
        Assert.Equal("my-client", dict["client_id"]);
        Assert.Equal("auth-code-123", dict["code"]);
        Assert.Equal("http://localhost:7626/sso-callback", dict["redirect_uri"]);
        Assert.Equal("my-verifier", dict["code_verifier"]);
        Assert.Contains("openid", dict["scope"]);
    }

    [Fact]
    public void BuildRefreshTokenBody_ContainsRefreshToken()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildRefreshTokenBody(
            clientId: "my-client",
            refreshToken: "my-refresh-token"
        );

        Dictionary<string, string> dict = body.ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Equal("refresh_token", dict["grant_type"]);
        Assert.Equal("my-client", dict["client_id"]);
        Assert.Equal("my-refresh-token", dict["refresh_token"]);
        Assert.Contains("openid", dict["scope"]);
    }
}
