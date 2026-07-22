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
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using NoMercy.NmSystem.Information;
using NoMercy.Setup.Auth;

namespace NoMercy.Tests.Setup;

[Trait(name: "Category", value: "Unit")]
public class OfflineTokenValidationTests : IDisposable
{
    private readonly string _testAuthKeysFile;

    public OfflineTokenValidationTests()
    {
        _testAuthKeysFile = AppFiles.AuthKeysFile;

        // Ensure config directory exists
        string configDir = AppFiles.ConfigPath;
        if (!Directory.Exists(path: configDir))
            Directory.CreateDirectory(path: configDir);

        // Clean up any previous test cache
        if (File.Exists(path: _testAuthKeysFile))
            File.Delete(path: _testAuthKeysFile);
    }

    public void Dispose()
    {
        if (File.Exists(path: _testAuthKeysFile))
            File.Delete(path: _testAuthKeysFile);
    }

    [Fact]
    public void CachePublicKey_WritesFileAndSetsKey()
    {
        using RSA rsa = RSA.Create(keySizeInBits: 2048);
        string publicKeyBase64 = Convert.ToBase64String(inArray: rsa.ExportSubjectPublicKeyInfo());

        OfflineJwksCache.CachePublicKey(publicKeyBase64: publicKeyBase64);

        Assert.True(
            condition: File.Exists(path: _testAuthKeysFile),
            userMessage: "CachePublicKey should create the auth keys cache file"
        );
        Assert.NotNull(@object: OfflineJwksCache.CachedSigningKey);

        string fileContent = File.ReadAllText(path: _testAuthKeysFile).Trim();
        Assert.Equal(expected: publicKeyBase64, actual: fileContent);
    }

    [Fact]
    public void LoadCachedPublicKey_ReturnsFalse_WhenNoFile()
    {
        if (File.Exists(path: _testAuthKeysFile))
            File.Delete(path: _testAuthKeysFile);

        bool result = OfflineJwksCache.LoadCachedPublicKey();

        Assert.False(condition: result, userMessage: "LoadCachedPublicKey should return false when no cache file exists");
    }

    [Fact]
    public void LoadCachedPublicKey_ReturnsTrue_WhenValidFile()
    {
        using RSA rsa = RSA.Create(keySizeInBits: 2048);
        string publicKeyBase64 = Convert.ToBase64String(inArray: rsa.ExportSubjectPublicKeyInfo());
        File.WriteAllText(path: _testAuthKeysFile, contents: publicKeyBase64);

        bool result = OfflineJwksCache.LoadCachedPublicKey();

        Assert.True(condition: result, userMessage: "LoadCachedPublicKey should return true with a valid cache file");
        Assert.NotNull(@object: OfflineJwksCache.CachedSigningKey);
    }

    [Fact]
    public void LoadCachedPublicKey_ReturnsFalse_WhenEmptyFile()
    {
        File.WriteAllText(path: _testAuthKeysFile, contents: "");

        bool result = OfflineJwksCache.LoadCachedPublicKey();

        Assert.False(condition: result, userMessage: "LoadCachedPublicKey should return false for empty cache file");
    }

    [Fact]
    public void LoadCachedPublicKey_ReturnsFalse_WhenCorruptFile()
    {
        File.WriteAllText(path: _testAuthKeysFile, contents: "not-valid-base64!@#$");

        bool result = OfflineJwksCache.LoadCachedPublicKey();

        Assert.False(condition: result, userMessage: "LoadCachedPublicKey should return false for corrupt cache file");
    }

    [Fact]
    public void CreateSecurityKeyFromBase64_ProducesValidRsaKey()
    {
        using RSA rsa = RSA.Create(keySizeInBits: 2048);
        string publicKeyBase64 = Convert.ToBase64String(inArray: rsa.ExportSubjectPublicKeyInfo());

        RsaSecurityKey key = OfflineJwksCache.CreateSecurityKeyFromBase64(publicKeyBase64: publicKeyBase64);

        Assert.NotNull(@object: key);
        Assert.NotNull(@object: key.Rsa);
    }

    [Fact]
    public void CachedKey_CanValidateJwtSignature()
    {
        // Generate an RSA keypair
        using RSA rsa = RSA.Create(keySizeInBits: 2048);
        RsaSecurityKey signingKey = new(rsa: rsa);
        SigningCredentials signingCredentials = new(key: signingKey, algorithm: SecurityAlgorithms.RsaSha256);

        // Create a JWT signed with the private key
        JwtSecurityTokenHandler handler = new();
        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new(claims:
            [
                new(type: ClaimTypes.NameIdentifier, value: Guid.NewGuid().ToString()),
                new(type: "scope", value: "openid profile"),
            ]),
            Expires = DateTime.UtcNow.AddHours(value: 1),
            Issuer = "https://auth.nomercy.tv/realms/NoMercyTV/",
            Audience = "nomercy-server",
            SigningCredentials = signingCredentials,
        };
        string token = handler.WriteToken(token: handler.CreateToken(tokenDescriptor: descriptor));

        // Cache the public key (simulating what Auth.AuthKeys does)
        string publicKeyBase64 = Convert.ToBase64String(inArray: rsa.ExportSubjectPublicKeyInfo());
        OfflineJwksCache.CachePublicKey(publicKeyBase64: publicKeyBase64);

        // Validate the JWT using the cached key (offline validation)
        RsaSecurityKey? cachedKey = OfflineJwksCache.CachedSigningKey;
        Assert.NotNull(@object: cachedKey);

        TokenValidationParameters validationParams = new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = cachedKey,
            ValidIssuer = "https://auth.nomercy.tv/realms/NoMercyTV/",
            ValidAudience = "nomercy-server",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(minutes: 5),
        };

        ClaimsPrincipal principal = handler.ValidateToken(
            token: token,
            validationParameters: validationParams,
            validatedToken: out SecurityToken validatedToken
        );

        Assert.NotNull(@object: principal);
        Assert.NotNull(@object: validatedToken);
        Assert.NotNull(@object: principal.FindFirst(type: ClaimTypes.NameIdentifier));
    }

    [Fact]
    public void CachedKey_RejectsTokenSignedWithDifferentKey()
    {
        // Generate two different RSA keypairs
        using RSA signingRsa = RSA.Create(keySizeInBits: 2048);
        using RSA differentRsa = RSA.Create(keySizeInBits: 2048);

        RsaSecurityKey signingKey = new(rsa: signingRsa);
        SigningCredentials signingCredentials = new(key: signingKey, algorithm: SecurityAlgorithms.RsaSha256);

        // Create a JWT signed with key A
        JwtSecurityTokenHandler handler = new();
        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new(claims: [new(type: ClaimTypes.NameIdentifier, value: Guid.NewGuid().ToString())]),
            Expires = DateTime.UtcNow.AddHours(value: 1),
            Issuer = "https://auth.nomercy.tv/realms/NoMercyTV/",
            Audience = "nomercy-server",
            SigningCredentials = signingCredentials,
        };
        string token = handler.WriteToken(token: handler.CreateToken(tokenDescriptor: descriptor));

        // Cache key B (different from signing key A)
        string differentKeyBase64 = Convert.ToBase64String(
            inArray: differentRsa.ExportSubjectPublicKeyInfo()
        );
        OfflineJwksCache.CachePublicKey(publicKeyBase64: differentKeyBase64);

        RsaSecurityKey? cachedKey = OfflineJwksCache.CachedSigningKey;
        Assert.NotNull(@object: cachedKey);

        TokenValidationParameters validationParams = new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = cachedKey,
            ValidIssuer = "https://auth.nomercy.tv/realms/NoMercyTV/",
            ValidAudience = "nomercy-server",
            ValidateLifetime = true,
        };

        // Validation should fail — wrong key
        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(testCode: () =>
            handler.ValidateToken(token: token, validationParameters: validationParams, validatedToken: out _)
        );
    }

    [Fact]
    public void CacheRoundTrip_PreservesKeyFidelity()
    {
        // Generate key, cache it, load it, verify it can still validate
        using RSA rsa = RSA.Create(keySizeInBits: 2048);
        string publicKeyBase64 = Convert.ToBase64String(inArray: rsa.ExportSubjectPublicKeyInfo());

        // Cache to file
        OfflineJwksCache.CachePublicKey(publicKeyBase64: publicKeyBase64);

        // Load from file (simulates a server restart)
        bool loaded = OfflineJwksCache.LoadCachedPublicKey();
        Assert.True(condition: loaded);

        // Sign a token with the private key
        RsaSecurityKey signingKey = new(rsa: rsa);
        JwtSecurityTokenHandler handler = new();
        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new(claims: [new(type: ClaimTypes.NameIdentifier, value: "test-user")]),
            Expires = DateTime.UtcNow.AddHours(value: 1),
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningCredentials = new(key: signingKey, algorithm: SecurityAlgorithms.RsaSha256),
        };
        string token = handler.WriteToken(token: handler.CreateToken(tokenDescriptor: descriptor));

        // Validate with the loaded cached key
        TokenValidationParameters validationParams = new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = OfflineJwksCache.CachedSigningKey,
            ValidIssuer = "test-issuer",
            ValidAudience = "test-audience",
        };

        ClaimsPrincipal principal = handler.ValidateToken(token: token, validationParameters: validationParams, validatedToken: out _);
        Assert.NotNull(@object: principal);
        Assert.Equal(expected: "test-user", actual: principal.FindFirst(type: ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public void AppFiles_HasAuthKeysFilePath()
    {
        string path = AppFiles.AuthKeysFile;

        Assert.NotNull(@object: path);
        Assert.EndsWith(expectedEndString: "auth_keys.json", actualString: path);
        Assert.Contains(expectedSubstring: "config", actualString: path);
    }

    [Fact]
    public void AppFiles_HasJwksCacheFilePath()
    {
        string path = AppFiles.JwksCacheFile;

        Assert.NotNull(@object: path);
        Assert.EndsWith(expectedEndString: "jwks_cache.json", actualString: path);
        Assert.Contains(expectedSubstring: "config", actualString: path);
    }
}

[Trait(name: "Category", value: "Unit")]
public class OfflineTokenValidationIntegrationTests
{
    [Fact]
    public void AuthKeysMethod_CachesPublicKey_WhenSourceHasCode()
    {
        // Auth.cs was replaced by AuthManager.cs — verify OfflineJwksCache.LoadCachedPublicKey
        // is still called during token initialization (AuthManager.InitializeAsync).
        string dir = AppContext.BaseDirectory;
        while (dir != null!)
        {
            string candidate = Path.Combine(paths: [dir, "src", "NoMercy.Setup", "Auth", "AuthManager.cs"]);
            if (File.Exists(path: candidate))
            {
                string source = File.ReadAllText(path: candidate);
                Assert.Contains(expectedSubstring: "OfflineJwksCache.LoadCachedPublicKey", actualString: source);
                return;
            }
            dir = Path.GetDirectoryName(path: dir)!;
        }
        Assert.Fail(message: "Could not find src/NoMercy.Setup/Auth/AuthManager.cs");
    }

    [Fact]
    public void InitWithFallback_LoadsCachedKeys()
    {
        // Auth.cs was replaced by AuthManager.cs — verify OfflineJwksCache.LoadCachedPublicKey
        // is called during token initialization in the new implementation.
        string dir = AppContext.BaseDirectory;
        while (dir != null!)
        {
            string candidate = Path.Combine(paths: [dir, "src", "NoMercy.Setup", "Auth", "AuthManager.cs"]);
            if (File.Exists(path: candidate))
            {
                string source = File.ReadAllText(path: candidate);
                Assert.Contains(expectedSubstring: "OfflineJwksCache.LoadCachedPublicKey", actualString: source);
                return;
            }
            dir = Path.GetDirectoryName(path: dir)!;
        }
        Assert.Fail(message: "Could not find src/NoMercy.Setup/Auth/AuthManager.cs");
    }

    [Fact]
    public void ServiceConfiguration_UsesIssuerSigningKeyResolver()
    {
        // Verify the JWT bearer config includes the offline key resolver
        string dir = AppContext.BaseDirectory;
        while (dir != null!)
        {
            string configDir = Path.Combine(path1: dir, path2: "src", path3: "NoMercy.Service", path4: "Configuration");
            if (Directory.Exists(path: configDir))
            {
                // ServiceConfiguration is split into partial files (ServiceConfiguration*.cs);
                // the JWT bearer setup lives in ServiceConfiguration.Auth.cs.
                string source = string.Empty;
                foreach (string file in Directory.GetFiles(path: configDir, searchPattern: "ServiceConfiguration*.cs"))
                    source += File.ReadAllText(path: file);

                Assert.Contains(expectedSubstring: "IssuerSigningKeyResolver", actualString: source);
                Assert.Contains(expectedSubstring: "OfflineJwksCache.CachedSigningKey", actualString: source);
                return;
            }
            dir = Path.GetDirectoryName(path: dir)!;
        }
        Assert.Fail(message: "Could not find the NoMercy.Service Configuration directory");
    }
}
