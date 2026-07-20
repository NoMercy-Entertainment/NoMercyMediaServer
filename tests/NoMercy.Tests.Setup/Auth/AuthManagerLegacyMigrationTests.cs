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

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Security;
using NoMercy.Setup.Auth;
using NoMercy.Storage.Drivers.Local;

namespace NoMercy.Tests.Setup.Auth;

/// <summary>
/// Requirement: on every <see cref="AuthManager.InitializeAsync"/> call, a legacy
/// <c>token.json</c> left over from before tokens moved into the encrypted
/// Configuration table must be migrated (valid data) or discarded (garbage/empty) —
/// and in both cases the plaintext file must never survive the call, since a stale
/// access/refresh token pair sitting in cleartext on disk is exactly what the
/// DB-encrypted storage was introduced to eliminate.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuthManagerLegacyMigrationTests : IDisposable
{
    private readonly AppDbContext _appContext;
    private readonly AuthManager _authManager;
    private readonly AuthTokenStore _authTokenStore = new();

    public AuthManagerLegacyMigrationTests()
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

        Directory.CreateDirectory(AppFiles.ConfigPath);
        DeleteTokenFileIfPresent();
    }

    public void Dispose()
    {
        _appContext.Database.CloseConnection();
        _appContext.Dispose();
        _authTokenStore.SetAccessToken(null);
        DeleteTokenFileIfPresent();
    }

#pragma warning disable CS0618 // TokenFile is [Obsolete] — migration-detection only, by design
    private static void DeleteTokenFileIfPresent()
    {
        if (File.Exists(AppFiles.TokenFile))
            File.Delete(AppFiles.TokenFile);
    }

    private static void WriteTokenFile(string content) =>
        File.WriteAllText(AppFiles.TokenFile, content);

    private static bool TokenFileExists() => File.Exists(AppFiles.TokenFile);
#pragma warning restore CS0618

    private async Task<Configuration?> ReadConfig(string key) =>
        await _appContext.Configuration.AsNoTracking().FirstOrDefaultAsync(c => c.Key == key);

    private static string CreateValidJwt(DateTime validTo)
    {
        System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler handler = new();
        System.IdentityModel.Tokens.Jwt.JwtSecurityToken token = new(
            issuer: "https://auth.nomercy.tv/realms/NoMercyTV",
            audience: "nomercy-server",
            claims:
            [
                new(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: validTo
        );
        return handler.WriteToken(token);
    }

    [Fact]
    public async Task InitializeAsync_NoLegacyFile_DoesNotThrowAndReturnsFalse()
    {
        bool result = await _authManager.InitializeAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task InitializeAsync_EmptyLegacyFile_DeletesFileWithoutMigrating()
    {
        WriteTokenFile(string.Empty);

        await _authManager.InitializeAsync();

        Assert.False(TokenFileExists());
        Assert.Null(await ReadConfig("auth_access_token"));
    }

    [Fact]
    public async Task InitializeAsync_EmptyObjectLegacyFile_DeletesFileWithoutMigrating()
    {
        WriteTokenFile("{}");

        await _authManager.InitializeAsync();

        Assert.False(TokenFileExists());
        Assert.Null(await ReadConfig("auth_access_token"));
    }

    [Fact]
    public async Task InitializeAsync_LegacyFileMissingAccessToken_DeletesFileWithoutMigrating()
    {
        WriteTokenFile("{\"refresh_token\":\"r1\"}");

        await _authManager.InitializeAsync();

        Assert.False(TokenFileExists());
        Assert.Null(await ReadConfig("auth_access_token"));
    }

    [Fact]
    public async Task InitializeAsync_ValidLegacyFile_MigratesToDbAndDeletesFile()
    {
        // Must be a real JWT whose issuer matches the configured realm: InitializeAsync
        // validates the migrated token immediately afterward (TokenIssuerMatchesConfiguredRealm)
        // and wipes anything that doesn't parse or doesn't match — a plain opaque string
        // would be migrated then instantly discarded, masking whether migration itself worked.
        string legacyJwt = CreateValidJwt(DateTime.UtcNow.AddHours(2));
        WriteTokenFile(
            JsonConvert.SerializeObject(
                new
                {
                    access_token = legacyJwt,
                    refresh_token = "legacy-refresh-token",
                    token_type = "Bearer",
                    expires_in = 3600,
                }
            )
        );

        await _authManager.InitializeAsync();

        Assert.False(TokenFileExists());
        Configuration? accessRow = await ReadConfig("auth_access_token");
        Assert.Equal(legacyJwt, accessRow?.SecureValue);
    }

    [Fact]
    public async Task InitializeAsync_MalformedJsonLegacyFile_LeavesFileIntact()
    {
        WriteTokenFile("{not-valid-json-at-all");

        await _authManager.InitializeAsync();

        // Malformed JSON is a parse exception, not an empty/garbage payload — the
        // migration code path explicitly leaves the file for manual inspection
        // rather than silently discarding a file it could not understand.
        Assert.True(TokenFileExists());
        Assert.Null(await ReadConfig("auth_access_token"));
    }
}
