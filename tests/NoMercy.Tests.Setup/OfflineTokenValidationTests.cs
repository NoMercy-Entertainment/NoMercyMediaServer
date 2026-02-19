using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using NoMercy.NmSystem.Information;
using NoMercy.Setup;

namespace NoMercy.Tests.Setup;

[Trait("Category", "Unit")]
public class OfflineTokenValidationTests : IDisposable
{
    private readonly string _testAuthKeysFile;

    public OfflineTokenValidationTests()
    {
        _testAuthKeysFile = AppFiles.AuthKeysFile;

        // Ensure config directory exists
        string configDir = AppFiles.ConfigPath;
        if (!Directory.Exists(configDir))
            Directory.CreateDirectory(configDir);

        // Clean up any previous test cache
        if (File.Exists(_testAuthKeysFile))
            File.Delete(_testAuthKeysFile);
    }

    public void Dispose()
    {
        if (File.Exists(_testAuthKeysFile))
            File.Delete(_testAuthKeysFile);
    }

    [Fact]
    public void CachePublicKey_WritesFileAndSetsKey()
    {
        using RSA rsa = RSA.Create(2048);
        string publicKeyBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());

        OfflineJwksCache.CachePublicKey(publicKeyBase64);

        Assert.True(File.Exists(_testAuthKeysFile),
            "CachePublicKey should create the auth keys cache file");
        Assert.NotNull(OfflineJwksCache.CachedSigningKey);

        string fileContent = File.ReadAllText(_testAuthKeysFile).Trim();
        Assert.Equal(publicKeyBase64, fileContent);
    }

    [Fact]
    public void LoadCachedPublicKey_ReturnsFalse_WhenNoFile()
    {
        if (File.Exists(_testAuthKeysFile))
            File.Delete(_testAuthKeysFile);

        bool result = OfflineJwksCache.LoadCachedPublicKey();

        Assert.False(result, "LoadCachedPublicKey should return false when no cache file exists");
    }

    [Fact]
    public void LoadCachedPublicKey_ReturnsTrue_WhenValidFile()
    {
        using RSA rsa = RSA.Create(2048);
        string publicKeyBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        File.WriteAllText(_testAuthKeysFile, publicKeyBase64);

        bool result = OfflineJwksCache.LoadCachedPublicKey();

        Assert.True(result, "LoadCachedPublicKey should return true with a valid cache file");
        Assert.NotNull(OfflineJwksCache.CachedSigningKey);
    }

    [Fact]
    public void LoadCachedPublicKey_ReturnsFalse_WhenEmptyFile()
    {
        File.WriteAllText(_testAuthKeysFile, "");

        bool result = OfflineJwksCache.LoadCachedPublicKey();

        Assert.False(result, "LoadCachedPublicKey should return false for empty cache file");
    }

    [Fact]
    public void LoadCachedPublicKey_ReturnsFalse_WhenCorruptFile()
    {
        File.WriteAllText(_testAuthKeysFile, "not-valid-base64!@#$");

        bool result = OfflineJwksCache.LoadCachedPublicKey();

        Assert.False(result, "LoadCachedPublicKey should return false for corrupt cache file");
    }

    [Fact]
    public void CreateSecurityKeyFromBase64_ProducesValidRsaKey()
    {
        using RSA rsa = RSA.Create(2048);
        string publicKeyBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());

        RsaSecurityKey key = OfflineJwksCache.CreateSecurityKeyFromBase64(publicKeyBase64);

        Assert.NotNull(key);
        Assert.NotNull(key.Rsa);
    }

    [Fact]
    public void CachedKey_CanValidateJwtSignature()
    {
        // Generate an RSA keypair
        using RSA rsa = RSA.Create(2048);
        RsaSecurityKey signingKey = new(rsa);
        SigningCredentials signingCredentials = new(signingKey, SecurityAlgorithms.RsaSha256);

        // Create a JWT signed with the private key
        JwtSecurityTokenHandler handler = new();
        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new(
            [
                new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new("scope", "openid profile")
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = "https://auth.nomercy.tv/realms/NoMercyTV/",
            Audience = "nomercy-server",
            SigningCredentials = signingCredentials
        };
        string token = handler.WriteToken(handler.CreateToken(descriptor));

        // Cache the public key (simulating what Auth.AuthKeys does)
        string publicKeyBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        OfflineJwksCache.CachePublicKey(publicKeyBase64);

        // Validate the JWT using the cached key (offline validation)
        RsaSecurityKey? cachedKey = OfflineJwksCache.CachedSigningKey;
        Assert.NotNull(cachedKey);

        TokenValidationParameters validationParams = new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = cachedKey,
            ValidIssuer = "https://auth.nomercy.tv/realms/NoMercyTV/",
            ValidAudience = "nomercy-server",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        ClaimsPrincipal principal = handler.ValidateToken(token, validationParams, out SecurityToken validatedToken);

        Assert.NotNull(principal);
        Assert.NotNull(validatedToken);
        Assert.NotNull(principal.FindFirst(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public void CachedKey_RejectsTokenSignedWithDifferentKey()
    {
        // Generate two different RSA keypairs
        using RSA signingRsa = RSA.Create(2048);
        using RSA differentRsa = RSA.Create(2048);

        RsaSecurityKey signingKey = new(signingRsa);
        SigningCredentials signingCredentials = new(signingKey, SecurityAlgorithms.RsaSha256);

        // Create a JWT signed with key A
        JwtSecurityTokenHandler handler = new();
        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new(
            [
                new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = "https://auth.nomercy.tv/realms/NoMercyTV/",
            Audience = "nomercy-server",
            SigningCredentials = signingCredentials
        };
        string token = handler.WriteToken(handler.CreateToken(descriptor));

        // Cache key B (different from signing key A)
        string differentKeyBase64 = Convert.ToBase64String(differentRsa.ExportSubjectPublicKeyInfo());
        OfflineJwksCache.CachePublicKey(differentKeyBase64);

        RsaSecurityKey? cachedKey = OfflineJwksCache.CachedSigningKey;
        Assert.NotNull(cachedKey);

        TokenValidationParameters validationParams = new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = cachedKey,
            ValidIssuer = "https://auth.nomercy.tv/realms/NoMercyTV/",
            ValidAudience = "nomercy-server",
            ValidateLifetime = true
        };

        // Validation should fail — wrong key
        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() =>
            handler.ValidateToken(token, validationParams, out _));
    }

    [Fact]
    public void CacheRoundTrip_PreservesKeyFidelity()
    {
        // Generate key, cache it, load it, verify it can still validate
        using RSA rsa = RSA.Create(2048);
        string publicKeyBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());

        // Cache to file
        OfflineJwksCache.CachePublicKey(publicKeyBase64);

        // Load from file (simulates a server restart)
        bool loaded = OfflineJwksCache.LoadCachedPublicKey();
        Assert.True(loaded);

        // Sign a token with the private key
        RsaSecurityKey signingKey = new(rsa);
        JwtSecurityTokenHandler handler = new();
        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new([new(ClaimTypes.NameIdentifier, "test-user")]),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningCredentials = new(signingKey, SecurityAlgorithms.RsaSha256)
        };
        string token = handler.WriteToken(handler.CreateToken(descriptor));

        // Validate with the loaded cached key
        TokenValidationParameters validationParams = new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = OfflineJwksCache.CachedSigningKey,
            ValidIssuer = "test-issuer",
            ValidAudience = "test-audience"
        };

        ClaimsPrincipal principal = handler.ValidateToken(token, validationParams, out _);
        Assert.NotNull(principal);
        Assert.Equal("test-user", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public void AppFiles_HasAuthKeysFilePath()
    {
        string path = AppFiles.AuthKeysFile;

        Assert.NotNull(path);
        Assert.EndsWith("auth_keys.json", path);
        Assert.Contains("config", path);
    }

    [Fact]
    public void AppFiles_HasJwksCacheFilePath()
    {
        string path = AppFiles.JwksCacheFile;

        Assert.NotNull(path);
        Assert.EndsWith("jwks_cache.json", path);
        Assert.Contains("config", path);
    }
}

[Trait("Category", "Unit")]
public class OfflineTokenValidationIntegrationTests
{
    [Fact]
    public void AuthKeysMethod_CachesPublicKey_WhenSourceHasCode()
    {
        // Verify that Auth.AuthKeys() now calls OfflineJwksCache.CachePublicKey
        // by inspecting source code — same pattern as other characterization tests
        string dir = AppContext.BaseDirectory;
        while (dir != null!)
        {
            string candidate = Path.Combine(dir, "src", "NoMercy.Setup", "Auth.cs");
            if (File.Exists(candidate))
            {
                string source = File.ReadAllText(candidate);
                Assert.Contains("OfflineJwksCache.CachePublicKey", source);
                return;
            }
            dir = Path.GetDirectoryName(dir)!;
        }
        Assert.Fail("Could not find src/NoMercy.Setup/Auth.cs");
    }

    [Fact]
    public void InitWithFallback_LoadsCachedKeys()
    {
        // Verify that Auth.InitWithFallback() loads cached auth keys
        string dir = AppContext.BaseDirectory;
        while (dir != null!)
        {
            string candidate = Path.Combine(dir, "src", "NoMercy.Setup", "Auth.cs");
            if (File.Exists(candidate))
            {
                string source = File.ReadAllText(candidate);
                Assert.Contains("OfflineJwksCache.LoadCachedPublicKey", source);
                return;
            }
            dir = Path.GetDirectoryName(dir)!;
        }
        Assert.Fail("Could not find src/NoMercy.Setup/Auth.cs");
    }

    [Fact]
    public void ServiceConfiguration_UsesIssuerSigningKeyResolver()
    {
        // Verify the JWT bearer config includes the offline key resolver
        string dir = AppContext.BaseDirectory;
        while (dir != null!)
        {
            string candidate = Path.Combine(dir, "src", "NoMercy.Service", "Configuration", "ServiceConfiguration.cs");
            if (File.Exists(candidate))
            {
                string source = File.ReadAllText(candidate);
                Assert.Contains("IssuerSigningKeyResolver", source);
                Assert.Contains("OfflineJwksCache.CachedSigningKey", source);
                return;
            }
            dir = Path.GetDirectoryName(dir)!;
        }
        Assert.Fail("Could not find ServiceConfiguration.cs");
    }
}
