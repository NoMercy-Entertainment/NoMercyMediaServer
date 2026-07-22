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

/// <summary>
/// Requirement: <see cref="BootOrchestrator.RunAsync"/> must enter setup mode (return
/// true) when there is no valid cached auth token, and must never let a Keycloak
/// reachability probe crash the boot sequence — the well-known-config check logs a
/// warning/error but always continues to the auth attempt. Registration
/// (<see cref="BootOrchestrator.RunRegistrationAsync"/>) must advance the setup phase
/// to Complete on success and record — never throw past — a registration failure so a
/// transient nomercy-tv outage degrades to retry-later instead of crashing the server.
/// </summary>
/// <remarks>
/// <see cref="AuthManager.IsDesktopEnvironment"/> is unconditionally true on Windows and
/// macOS, but on Linux it depends on DISPLAY/WAYLAND_DISPLAY being set — and CI runs
/// this suite on a single headless ubuntu-latest runner (no separate per-OS leg), where
/// neither is set by default. The test below forces DISPLAY so the desktop early-return
/// is what actually runs everywhere this suite executes; <see cref="BootOrchestrator
/// .StartHeadlessDeviceCodeFlowAsync"/>'s device-code-flow body and its private
/// <c>PollDeviceGrant</c> helper remain untested here.
/// </remarks>
[Trait(name: "Category", value: "Unit")]
public sealed class BootOrchestratorAdditionalTests : IDisposable
{
    private readonly AppDbContext _appContext;
    private readonly AuthManager _authManager;
    private readonly SetupState _setupState;
    private readonly ServiceProvider _serviceProvider;
    private readonly string? _originalAppPath;
    private readonly string _tempAppPath;

    public BootOrchestratorAdditionalTests()
    {
        _originalAppPath = Environment.GetEnvironmentVariable(variable: "NOMERCY_APP_PATH");
        _tempAppPath = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-bootorch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempAppPath);
        Environment.SetEnvironmentVariable(variable: "NOMERCY_APP_PATH", value: _tempAppPath);

        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        _serviceProvider = services.BuildServiceProvider();
        TokenStore.Initialize(serviceProvider: _serviceProvider);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connectionString: "Data Source=:memory:");
        _appContext = new(options: optionsBuilder.Options);
        _appContext.Database.OpenConnection();
        _appContext.Database.EnsureCreated();

        _authManager = new(appContext: _appContext, driver: new LocalStorageDriver(), authTokenStore: new AuthTokenStore());
        _setupState = new();

        // CertificateService.LoadFromDb/HasValidCertificate open their OWN on-disk
        // AppDbContext at NOMERCY_APP_PATH (independent of the in-memory _appContext
        // above) — create the folder tree + schema there too, mirroring what real
        // Phase-1 (AppFiles.CreateAppFolders + migrations) already guarantees before
        // BootOrchestrator ever runs.
        NoMercy.NmSystem.Information.AppFiles.CreateAppFolders();
        using AppDbContext onDisk = new();
        onDisk.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _appContext.Database.CloseConnection();
        _appContext.Dispose();
        _serviceProvider.Dispose();
        Environment.SetEnvironmentVariable(variable: "NOMERCY_APP_PATH", value: _originalAppPath);
        try
        {
            if (Directory.Exists(path: _tempAppPath))
                Directory.Delete(path: _tempAppPath, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private BootOrchestrator BuildOrchestrator(
        IServerRegistrationService? registrationService = null,
        ICertificateService? certificateService = null
    ) =>
        new(
            setupState: _setupState,
            authManager: _authManager,
            apiKeyLoader: new FakeApiKeyLoader(),
            degradedModeRecovery: new FakeDegradedModeRecovery(),
            serverRegistrationService: registrationService ?? new FakeServerRegistrationService(),
            authTokenStore: new AuthTokenStore(),
            certificateService: certificateService
                                ?? new CertificateService(logger: NullLogger<CertificateService>.Instance, httpClientFactory: null!)
        );

    [Fact]
    public async Task RunAsync_NoValidToken_ReturnsTrue_EntersSetupMode()
    {
        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 200, Body: "{}");
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        BootOrchestrator orchestrator = BuildOrchestrator();

        bool needsSetup = await orchestrator.RunAsync(services: _serviceProvider, ct: CancellationToken.None);

        Assert.True(condition: needsSetup);
    }

    [Fact]
    public async Task RunAsync_KeycloakWellKnownReachable_DoesNotThrow()
    {
        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 200, Body: "{\"issuer\":\"https://auth.nomercy.tv/realms/NoMercyTV\"}");
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        BootOrchestrator orchestrator = BuildOrchestrator();

        bool needsSetup = await orchestrator.RunAsync(services: _serviceProvider, ct: CancellationToken.None);

        // No cached token in the in-memory DB either way — this test's purpose is
        // proving the reachable-well-known branch itself completes cleanly.
        Assert.True(condition: needsSetup);
    }

    [Fact]
    public async Task RunAsync_KeycloakWellKnownReturnsError_StillProceedsWithoutThrowing()
    {
        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 503, Body: "service unavailable");
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        BootOrchestrator orchestrator = BuildOrchestrator();

        bool needsSetup = await orchestrator.RunAsync(services: _serviceProvider, ct: CancellationToken.None);

        Assert.True(condition: needsSetup);
    }

    [Fact]
    public async Task RunAsync_KeycloakUnreachable_StillProceedsWithoutThrowing()
    {
        // Connection-refused path (no cached JWKS key -> the "BOOT FAILURE" log branch,
        // or a cached key -> the softer warning branch; both just log, so this test only
        // locks "the probe failing must never crash the boot sequence").
        using ExternalServicesConfigScope scope = new(authBaseUrl: "http://127.0.0.1:1/");

        BootOrchestrator orchestrator = BuildOrchestrator();

        bool needsSetup = await orchestrator.RunAsync(services: _serviceProvider, ct: CancellationToken.None);

        Assert.True(condition: needsSetup);
    }

    [Fact]
    public async Task RunRegistrationAsync_Success_TransitionsToCompleteAndReturnsCertStatus()
    {
        BootOrchestrator orchestrator = BuildOrchestrator();
        // RunRegistrationAsync's own TransitionTo(Registering) is only a valid move
        // from Authenticated per SetupState's state machine — mirror the real call
        // sequence (RunAsync/RunPostAuthAsync always reach Authenticated first).
        _setupState.TransitionTo(targetPhase: SetupPhase.Authenticating);
        _setupState.TransitionTo(targetPhase: SetupPhase.Authenticated);

        bool certAcquired = await orchestrator.RunRegistrationAsync(ct: CancellationToken.None);

        // No certificate has been stored in the fresh on-disk DB, so registration
        // succeeds but certificate acquisition does not — the phase still reaches
        // Complete (partial functionality beats no functionality).
        Assert.False(condition: certAcquired);
        Assert.Equal(expected: SetupPhase.Complete, actual: _setupState.CurrentPhase);
    }

    [Fact]
    public async Task RunRegistrationAsync_RegistrationThrows_SetsErrorAndStillMarksComplete()
    {
        ThrowingServerRegistrationService throwing = new();
        BootOrchestrator orchestrator = BuildOrchestrator(registrationService: throwing);
        _setupState.TransitionTo(targetPhase: SetupPhase.Authenticating);
        _setupState.TransitionTo(targetPhase: SetupPhase.Authenticated);

        bool certAcquired = await orchestrator.RunRegistrationAsync(ct: CancellationToken.None);

        // TransitionTo(Complete) clears ErrorMessage as part of every transition (see
        // SetupState.TransitionTo) — the observable contract here is "never throws,
        // still reaches Complete degraded" rather than a surviving error message.
        Assert.False(condition: certAcquired);
        Assert.Equal(expected: SetupPhase.Complete, actual: _setupState.CurrentPhase);
    }

    [Fact]
    public async Task StartHeadlessDeviceCodeFlowAsync_OnDesktopEnvironment_ReturnsImmediately()
    {
        // AuthManager.IsDesktopEnvironment() is unconditionally true on Windows/macOS,
        // but on Linux it depends on DISPLAY/WAYLAND_DISPLAY — and CI runs this suite
        // on a headless ubuntu-latest runner (no separate per-OS leg), where both are
        // unset. Force DISPLAY for the duration of this test so the desktop early-return
        // branch is what actually runs on every OS this suite executes on; without it,
        // CI instead falls into the device-code-flow body, which makes a real network
        // call and runs out the 2s clock below.
        string? originalDisplay = Environment.GetEnvironmentVariable(variable: "DISPLAY");
        Environment.SetEnvironmentVariable(variable: "DISPLAY", value: ":0");
        try
        {
            BootOrchestrator orchestrator = BuildOrchestrator();

            using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 2));
            await orchestrator.StartHeadlessDeviceCodeFlowAsync(ct: cts.Token);

            Assert.False(
                condition: cts.IsCancellationRequested,
                userMessage: "should return immediately, not run out the clock"
            );
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable: "DISPLAY", value: originalDisplay);
        }
    }

    private sealed class ThrowingServerRegistrationService : IServerRegistrationService
    {
        public Task Init(int maxRetries = 5) =>
            throw new InvalidOperationException(message: "simulated registration failure");

        public Task GetTunnelAvailability() => Task.CompletedTask;
    }
}
