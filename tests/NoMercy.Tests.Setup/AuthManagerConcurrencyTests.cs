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

using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Security;
using NoMercy.Setup.Auth;
using NoMercy.Storage.Drivers.Local;

namespace NoMercy.Tests.Setup;

/// <summary>
/// AuthManager holds a single non-thread-safe <see cref="AppDbContext"/>. Its
/// read path (InitializeAsync → LoadSecureValue) and write path (StoreTokensAsync
/// → UpsertSecureValue) both hit that one context, and at boot they run at the
/// same time (the token-refresh timer while a PKCE callback stores tokens). Unless
/// every access serializes on the same lock, EF throws "a second operation was
/// started on this DbContext before a previous operation completed".
///
/// <para>Each SQL command is delayed by an interceptor so the context's operation
/// lease is genuinely held while a concurrent operation starts — without that
/// window the in-memory commands finish too fast to ever overlap, and the race
/// hides.</para>
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class AuthManagerConcurrencyTests : IDisposable
{
    private readonly AppDbContext _appContext;
    private readonly AuthManager _authManager;
    private readonly AuthTokenStore _authTokenStore = new();

    public AuthManagerConcurrencyTests()
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        ServiceProvider provider = services.BuildServiceProvider();
        TokenStore.Initialize(serviceProvider: provider);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connectionString: "Data Source=:memory:");
        optionsBuilder.AddInterceptors(interceptors: new LeaseHoldingInterceptor());
        _appContext = new(options: optionsBuilder.Options);
        _appContext.Database.OpenConnection();
        _appContext.Database.EnsureCreated();

        _authManager = new(appContext: _appContext, driver: new LocalStorageDriver(), authTokenStore: _authTokenStore);
    }

    public void Dispose()
    {
        _appContext.Database.CloseConnection();
        _appContext.Dispose();
        _authTokenStore.SetAccessToken(token: null);
    }

    [Fact]
    public async Task ConcurrentReadAndWrite_OnSharedContext_DoesNotThrowConcurrencyException()
    {
        await SeedSecureValue(key: "auth_access_token", value: CreateValidJwt(validTo: DateTime.UtcNow.AddHours(value: 2)));
        await SeedSecureValue(
            key: "auth_token_metadata",
            value: $"{{\"expires_at\":\"{DateTime.UtcNow.AddHours(value: 2):O}\",\"token_type\":\"Bearer\"}}"
        );

        // Reads (InitializeAsync) interleaved with writes (StoreTokensAsync) on the
        // one shared context. Without the shared lock the delayed command keeps the
        // context lease held while the next operation starts → InvalidOperationException.
        List<Task> tasks =
        [
            _authManager.InitializeAsync(),
            StoreAsync(),
            _authManager.InitializeAsync(),
            StoreAsync(),
            _authManager.InitializeAsync(),
        ];

        Exception? thrown = await Record.ExceptionAsync(testCode: () => Task.WhenAll(tasks: tasks));

        Assert.Null(@object: thrown);
    }

    private Task StoreAsync() =>
        _authManager.StoreTokensAsync(
            accessToken: CreateValidJwt(validTo: DateTime.UtcNow.AddHours(value: 2)),
            refreshToken: "refresh-token",
            expiresAt: DateTime.UtcNow.AddHours(value: 2),
            tokenType: "Bearer"
        );

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

    private static string CreateValidJwt(DateTime validTo)
    {
        JwtSecurityTokenHandler handler = new();
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

    /// <summary>
    /// Delays every SQL command so the DbContext operation lease is held long
    /// enough for a concurrent operation to collide with it — turning the
    /// otherwise-too-fast in-memory race into a deterministic one.
    /// </summary>
    private sealed class LeaseHoldingInterceptor : DbCommandInterceptor
    {
        private const int DelayMs = 40;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            await Task.Delay(millisecondsDelay: DelayMs, cancellationToken: cancellationToken);
            return await base.ReaderExecutingAsync(command: command, eventData: eventData, result: result, cancellationToken: cancellationToken);
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            await Task.Delay(millisecondsDelay: DelayMs, cancellationToken: cancellationToken);
            return await base.NonQueryExecutingAsync(command: command, eventData: eventData, result: result, cancellationToken: cancellationToken);
        }

        public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default
        )
        {
            await Task.Delay(millisecondsDelay: DelayMs, cancellationToken: cancellationToken);
            return await base.ScalarExecutingAsync(command: command, eventData: eventData, result: result, cancellationToken: cancellationToken);
        }
    }
}
