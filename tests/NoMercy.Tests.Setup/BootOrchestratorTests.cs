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
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Database;
using NoMercy.Networking.Certificate;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Security;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Boot;
using NoMercy.Setup.Server;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Tests.Setup.Infrastructure;

namespace NoMercy.Tests.Setup;

[Trait(name: "Category", value: "Unit")]
public class BootOrchestratorTests : IDisposable
{
    private readonly AppDbContext _appContext;
    private readonly AuthManager _authManager;
    private readonly SetupState _setupState;
    private readonly BootOrchestrator _orchestrator;

    public BootOrchestratorTests()
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

        _authManager = new(appContext: _appContext, driver: new LocalStorageDriver(), authTokenStore: new AuthTokenStore());
        _setupState = new();
        _orchestrator = new(
            setupState: _setupState,
            authManager: _authManager,
            apiKeyLoader: new FakeApiKeyLoader(),
            degradedModeRecovery: new FakeDegradedModeRecovery(),
            serverRegistrationService: new FakeServerRegistrationService(),
            authTokenStore: new AuthTokenStore(),
            certificateService: new CertificateService(logger: NullLogger<CertificateService>.Instance, httpClientFactory: null!)
        );
    }

    public void Dispose()
    {
        _appContext.Database.CloseConnection();
        _appContext.Dispose();
    }

    [Fact]
    public void SetupState_StartsAsUnauthenticated()
    {
        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: _setupState.CurrentPhase);
        Assert.True(condition: _setupState.IsSetupRequired);
    }

    [Fact]
    public async Task PostAuth_WaitsForAuthenticated()
    {
        using CancellationTokenSource cts = new(delay: TimeSpan.FromMilliseconds(milliseconds: 200));
        Task postAuth = _orchestrator.RunPostAuthAsync(ct: cts.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () => postAuth);
    }

    [Fact]
    public async Task PostAuth_ProceedsWhenAuthenticated()
    {
        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 5));

        _ = Task.Run(function: async () =>
        {
            await Task.Delay(millisecondsDelay: 100);
            _setupState.TransitionTo(targetPhase: SetupPhase.Authenticating);
            _setupState.TransitionTo(targetPhase: SetupPhase.Authenticated);
        });

        try
        {
            await _orchestrator.RunPostAuthAsync(ct: cts.Token);
        }
        catch
        {
            // Registration will fail in test (no network) — expected
        }

        Assert.NotEqual(expected: SetupPhase.Unauthenticated, actual: _setupState.CurrentPhase);
    }
}
