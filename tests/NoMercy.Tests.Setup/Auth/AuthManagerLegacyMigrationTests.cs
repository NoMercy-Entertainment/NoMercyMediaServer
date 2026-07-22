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
[Trait(name: "Category", value: "Unit")]
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
        TokenStore.Initialize(serviceProvider: provider);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connectionString: "Data Source=:memory:");
        _appContext = new(options: optionsBuilder.Options);
        _appContext.Database.OpenConnection();
        _appContext.Database.EnsureCreated();

        _authManager = new(appContext: _appContext, driver: new LocalStorageDriver(), authTokenStore: _authTokenStore);

        Directory.CreateDirectory(path: AppFiles.ConfigPath);
        DeleteTokenFileIfPresent();
    }

    public void Dispose()
    {
        _appContext.Database.CloseConnection();
        _appContext.Dispose();
        _authTokenStore.SetAccessToken(token: null);
        DeleteTokenFileIfPresent();
    }

#pragma warning disable CS0618 // TokenFile is [Obsolete] — migration-detection only, by design
    private static void DeleteTokenFileIfPresent()
    {
        if (File.Exists(path: AppFiles.TokenFile))
            File.Delete(path: AppFiles.TokenFile);
    }

    private static void WriteTokenFile(string content) =>
        File.WriteAllText(path: AppFiles.TokenFile, contents: content);

    private static bool TokenFileExists() => File.Exists(path: AppFiles.TokenFile);
#pragma warning restore CS0618

    private async Task<Configuration?> ReadConfig(string key) =>
        await _appContext.Configuration.AsNoTracking().FirstOrDefaultAsync(predicate: c => c.Key == key);

    private static string CreateValidJwt(DateTime validTo)
    {
        System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler handler = new();
        System.IdentityModel.Tokens.Jwt.JwtSecurityToken token = new(
            issuer: "https://auth.nomercy.tv/realms/NoMercyTV",
            audience: "nomercy-server",
            claims:
            [
                new(type: System.Security.Claims.ClaimTypes.NameIdentifier, value: Guid.NewGuid().ToString()),
            ],
            notBefore: DateTime.UtcNow.AddMinutes(value: -5),
            expires: validTo
        );
        return handler.WriteToken(token: token);
    }

    [Fact]
    public async Task InitializeAsync_NoLegacyFile_DoesNotThrowAndReturnsFalse()
    {
        bool result = await _authManager.InitializeAsync();

        Assert.False(condition: result);
    }

    [Fact]
    public async Task InitializeAsync_EmptyLegacyFile_DeletesFileWithoutMigrating()
    {
        WriteTokenFile(content: string.Empty);

        await _authManager.InitializeAsync();

        Assert.False(condition: TokenFileExists());
        Assert.Null(@object: await ReadConfig(key: "auth_access_token"));
    }

    [Fact]
    public async Task InitializeAsync_EmptyObjectLegacyFile_DeletesFileWithoutMigrating()
    {
        WriteTokenFile(content: "{}");

        await _authManager.InitializeAsync();

        Assert.False(condition: TokenFileExists());
        Assert.Null(@object: await ReadConfig(key: "auth_access_token"));
    }

    [Fact]
    public async Task InitializeAsync_LegacyFileMissingAccessToken_DeletesFileWithoutMigrating()
    {
        WriteTokenFile(content: "{\"refresh_token\":\"r1\"}");

        await _authManager.InitializeAsync();

        Assert.False(condition: TokenFileExists());
        Assert.Null(@object: await ReadConfig(key: "auth_access_token"));
    }

    [Fact]
    public async Task InitializeAsync_ValidLegacyFile_MigratesToDbAndDeletesFile()
    {
        // Must be a real JWT whose issuer matches the configured realm: InitializeAsync
        // validates the migrated token immediately afterward (TokenIssuerMatchesConfiguredRealm)
        // and wipes anything that doesn't parse or doesn't match — a plain opaque string
        // would be migrated then instantly discarded, masking whether migration itself worked.
        string legacyJwt = CreateValidJwt(validTo: DateTime.UtcNow.AddHours(value: 2));
        WriteTokenFile(
            content: JsonConvert.SerializeObject(
                value: new
                {
                    access_token = legacyJwt,
                    refresh_token = "legacy-refresh-token",
                    token_type = "Bearer",
                    expires_in = 3600,
                }
            )
        );

        await _authManager.InitializeAsync();

        Assert.False(condition: TokenFileExists());
        Configuration? accessRow = await ReadConfig(key: "auth_access_token");
        Assert.Equal(expected: legacyJwt, actual: accessRow?.SecureValue);
    }

    [Fact]
    public async Task InitializeAsync_MalformedJsonLegacyFile_LeavesFileIntact()
    {
        WriteTokenFile(content: "{not-valid-json-at-all");

        await _authManager.InitializeAsync();

        // Malformed JSON is a parse exception, not an empty/garbage payload — the
        // migration code path explicitly leaves the file for manual inspection
        // rather than silently discarding a file it could not understand.
        Assert.True(condition: TokenFileExists());
        Assert.Null(@object: await ReadConfig(key: "auth_access_token"));
    }
}
