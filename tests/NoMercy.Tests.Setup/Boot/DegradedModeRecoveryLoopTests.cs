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
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Auth;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Boot;
using NoMercy.Setup.Server;

namespace NoMercy.Tests.Setup.Boot;

/// <summary>
/// Requirement: <see cref="DegradedModeRecovery.StartRecoveryLoop"/> must keep retrying
/// (never throw, never spin the loop tighter than its backoff) while the network is
/// unavailable, and — once connectivity returns — must actually re-check every
/// deferred task and flip <see cref="DeferredTasks.AllCompleted"/> once all of them
/// report done, so the server genuinely exits degraded mode rather than looping forever
/// even after every dependency has recovered.
/// </summary>
/// <remarks>
/// Uses the internal delay-injecting constructor (see <c>DegradedModeRecovery.cs</c>)
/// to skip the real 30s-30m backoff. The "network available" tests rely on genuine
/// internet reachability via <c>NetworkProbe</c>'s real default targets — the same
/// dependency <c>DegradedModeStartupTests.NetworkProbe_ReturnsTrue_WhenAtLeastOneTargetReachable</c>
/// already has; the "network unavailable" tests point <c>NetworkProbe.ProbeTargets</c>
/// at an RFC 5737 reserved, guaranteed-unroutable address instead of a live network
/// dependency for the negative case.
/// </remarks>
[Trait(name: "Category", value: "Unit")]
public sealed class DegradedModeRecoveryLoopTests : IDisposable
{
    private readonly string[] _originalProbeTargets;

    public DegradedModeRecoveryLoopTests()
    {
        _originalProbeTargets = NetworkProbe.ProbeTargets;
    }

    public void Dispose()
    {
        NetworkProbe.ProbeTargets = _originalProbeTargets;
    }

    private static string CreateValidJwt(TimeSpan validFor)
    {
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken token = new(
            issuer: "https://auth.nomercy.tv/realms/NoMercyTV",
            audience: "nomercy-server",
            claims: [new(type: "sub", value: Guid.NewGuid().ToString())],
            notBefore: DateTime.UtcNow.AddMinutes(value: -5),
            expires: DateTime.UtcNow.Add(value: validFor)
        );
        return handler.WriteToken(token: token);
    }

    private sealed class NoOpApiKeyLoader : IApiKeyLoader
    {
        public Task LoadKeys(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoOpServerRegistrationService : IServerRegistrationService
    {
        public int InitCallCount;

        public Task Init(int maxRetries = 5)
        {
            InitCallCount++;
            return Task.CompletedTask;
        }

        public Task GetTunnelAvailability() => Task.CompletedTask;
    }

    private sealed class CooldownServerRegistrationService : IServerRegistrationService
    {
        public Task Init(int maxRetries = 5) =>
            throw new InvalidOperationException(message: "Registration on cooldown after recent failure");

        public Task GetTunnelAvailability() => Task.CompletedTask;
    }

    private sealed class FailingServerRegistrationService : IServerRegistrationService
    {
        public Task Init(int maxRetries = 5) =>
            throw new HttpRequestException(message: "simulated registration transport failure");

        public Task GetTunnelAvailability() => Task.CompletedTask;
    }

    [Fact]
    public async Task StartRecoveryLoop_NetworkUnavailable_KeepsRetryingWithoutThrowing()
    {
        // RFC 5737 TEST-NET-1: reserved for documentation, guaranteed non-routable —
        // no live network dependency, and CheckConnectivity's own 3s cap keeps each
        // failing attempt bounded.
        NetworkProbe.ProbeTargets = ["192.0.2.1"];

        DeferredTasks tasks = new();
        DegradedModeRecovery recovery = new(
            authTokenStore: new AuthTokenStore(),
            apiKeyLoader: new NoOpApiKeyLoader(),
            apiKeyStore: new ApiKeyStore(),
            serverRegistrationService: new NoOpServerRegistrationService(),
            networkDiscovery: null,
            delay: _ => Task.CompletedTask
        );

        Task loopTask = recovery.StartRecoveryLoop(tasks: tasks);

        // Let at least one real (network-unavailable) NetworkProbe timeout elapse,
        // then signal completion externally — StartRecoveryLoop re-checks
        // tasks.AllCompleted at the top of every iteration.
        await Task.Delay(delay: TimeSpan.FromSeconds(seconds: 4));
        Assert.False(condition: tasks.AllCompleted, userMessage: "should still be retrying — network was never available");
        tasks.AllCompleted = true;

        await loopTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 10));
    }

    [Fact]
    public async Task StartRecoveryLoop_NetworkAvailable_AllTasksSucceed_MarksAllCompleted()
    {
        // Real internet reachability (see class remarks) — every other dependency is
        // faked/pre-satisfied so this single pass through the loop body flips every
        // DeferredTasks flag and exits on its own.
        NetworkProbe.ProbeTargets = _originalProbeTargets;

        ApiKeyStore apiKeyStore = new() { KeysLoaded = true };
        AuthTokenStore authTokenStore = new();
        authTokenStore.SetAccessToken(token: CreateValidJwt(validFor: TimeSpan.FromHours(hours: 1)));
        NoOpServerRegistrationService registration = new();

        DeferredTasks tasks = new() { SeedsRun = true, BinariesReady = true };
        DegradedModeRecovery recovery = new(
            authTokenStore: authTokenStore,
            apiKeyLoader: new NoOpApiKeyLoader(),
            apiKeyStore: apiKeyStore,
            serverRegistrationService: registration,
            networkDiscovery: null,
            delay: _ => Task.CompletedTask
        );

        await recovery.StartRecoveryLoop(tasks: tasks).WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 15));

        Assert.True(condition: tasks.AllCompleted);
        Assert.True(condition: tasks.ApiKeysLoaded);
        Assert.True(condition: tasks.Authenticated);
        Assert.True(condition: tasks.NetworkDiscovered);
        Assert.True(condition: tasks.Registered);
        Assert.Equal(expected: 1, actual: registration.InitCallCount);
    }

    [Fact]
    public async Task StartRecoveryLoop_NoAccessToken_LeavesAuthenticatedFalse_SkipsRegistration()
    {
        NetworkProbe.ProbeTargets = _originalProbeTargets;

        ApiKeyStore apiKeyStore = new() { KeysLoaded = true };
        NoOpServerRegistrationService registration = new();

        DeferredTasks tasks = new()
        {
            SeedsRun = true,
            BinariesReady = true,
            AllCompleted = false,
        };
        DegradedModeRecovery recovery = new(
            authTokenStore: new AuthTokenStore(), // no access token set
            apiKeyLoader: new NoOpApiKeyLoader(),
            apiKeyStore: apiKeyStore,
            serverRegistrationService: registration,
            networkDiscovery: null,
            delay: _ => Task.CompletedTask
        );

        // AllCompleted never flips (Authenticated stays false) — run one iteration's
        // worth of work then stop the loop externally rather than waiting forever.
        Task loopTask = recovery.StartRecoveryLoop(tasks: tasks);
        await Task.Delay(delay: TimeSpan.FromSeconds(seconds: 2));
        tasks.AllCompleted = true;
        await loopTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 10));

        Assert.False(condition: tasks.Authenticated);
        Assert.False(condition: tasks.Registered);
        Assert.Equal(expected: 0, actual: registration.InitCallCount);
    }

    [Fact]
    public async Task StartRecoveryLoop_ExpiredAccessToken_DefersRegistration()
    {
        NetworkProbe.ProbeTargets = _originalProbeTargets;

        ApiKeyStore apiKeyStore = new() { KeysLoaded = true };
        AuthTokenStore authTokenStore = new();
        // Already expired — tokenNeedsRefresh must evaluate true and defer registration
        // rather than calling Init() with a token nomercy-tv would reject.
        authTokenStore.SetAccessToken(token: CreateValidJwt(validFor: TimeSpan.FromSeconds(seconds: -60)));
        NoOpServerRegistrationService registration = new();

        DeferredTasks tasks = new() { SeedsRun = true, BinariesReady = true };
        DegradedModeRecovery recovery = new(
            authTokenStore: authTokenStore,
            apiKeyLoader: new NoOpApiKeyLoader(),
            apiKeyStore: apiKeyStore,
            serverRegistrationService: registration,
            networkDiscovery: null,
            delay: _ => Task.CompletedTask
        );

        Task loopTask = recovery.StartRecoveryLoop(tasks: tasks);
        await Task.Delay(delay: TimeSpan.FromSeconds(seconds: 2));
        tasks.AllCompleted = true;
        await loopTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 10));

        Assert.True(condition: tasks.Authenticated); // token is present, just expired
        Assert.False(condition: tasks.Registered);
        Assert.Equal(expected: 0, actual: registration.InitCallCount);
    }

    [Fact]
    public async Task StartRecoveryLoop_RegistrationOnCooldown_DoesNotThrow_RetriesLater()
    {
        NetworkProbe.ProbeTargets = _originalProbeTargets;

        ApiKeyStore apiKeyStore = new() { KeysLoaded = true };
        AuthTokenStore authTokenStore = new();
        authTokenStore.SetAccessToken(token: CreateValidJwt(validFor: TimeSpan.FromHours(hours: 1)));

        DeferredTasks tasks = new() { SeedsRun = true, BinariesReady = true };
        DegradedModeRecovery recovery = new(
            authTokenStore: authTokenStore,
            apiKeyLoader: new NoOpApiKeyLoader(),
            apiKeyStore: apiKeyStore,
            serverRegistrationService: new CooldownServerRegistrationService(),
            networkDiscovery: null,
            delay: _ => Task.CompletedTask
        );

        Task loopTask = recovery.StartRecoveryLoop(tasks: tasks);
        await Task.Delay(delay: TimeSpan.FromSeconds(seconds: 2));
        tasks.AllCompleted = true;
        await loopTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 10));

        Assert.False(condition: tasks.Registered);
    }

    [Fact]
    public async Task StartRecoveryLoop_RegistrationThrowsOtherException_DoesNotThrow_RetriesLater()
    {
        NetworkProbe.ProbeTargets = _originalProbeTargets;

        ApiKeyStore apiKeyStore = new() { KeysLoaded = true };
        AuthTokenStore authTokenStore = new();
        authTokenStore.SetAccessToken(token: CreateValidJwt(validFor: TimeSpan.FromHours(hours: 1)));

        DeferredTasks tasks = new() { SeedsRun = true, BinariesReady = true };
        DegradedModeRecovery recovery = new(
            authTokenStore: authTokenStore,
            apiKeyLoader: new NoOpApiKeyLoader(),
            apiKeyStore: apiKeyStore,
            serverRegistrationService: new FailingServerRegistrationService(),
            networkDiscovery: null,
            delay: _ => Task.CompletedTask
        );

        Task loopTask = recovery.StartRecoveryLoop(tasks: tasks);
        await Task.Delay(delay: TimeSpan.FromSeconds(seconds: 2));
        tasks.AllCompleted = true;
        await loopTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 10));

        Assert.False(condition: tasks.Registered);
    }

    [Fact]
    public async Task StartRecoveryLoop_ApiKeyLoaderThrows_DoesNotThrow_LeavesApiKeysLoadedFalse()
    {
        NetworkProbe.ProbeTargets = _originalProbeTargets;

        DeferredTasks tasks = new() { SeedsRun = true, BinariesReady = true };
        DegradedModeRecovery recovery = new(
            authTokenStore: new AuthTokenStore(),
            apiKeyLoader: new ThrowingApiKeyLoader(),
            apiKeyStore: new ApiKeyStore(),
            serverRegistrationService: new NoOpServerRegistrationService(),
            networkDiscovery: null,
            delay: _ => Task.CompletedTask
        );

        Task loopTask = recovery.StartRecoveryLoop(tasks: tasks);
        await Task.Delay(delay: TimeSpan.FromSeconds(seconds: 2));
        tasks.AllCompleted = true;
        await loopTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 10));

        Assert.False(condition: tasks.ApiKeysLoaded);
    }

    [Fact]
    public async Task StartRecoveryLoop_NetworkDiscoveryThrows_DoesNotThrow_LeavesNetworkDiscoveredFalse()
    {
        NetworkProbe.ProbeTargets = _originalProbeTargets;

        DeferredTasks tasks = new() { SeedsRun = true, BinariesReady = true };
        DegradedModeRecovery recovery = new(
            authTokenStore: new AuthTokenStore(),
            apiKeyLoader: new NoOpApiKeyLoader(),
            apiKeyStore: new ApiKeyStore(),
            serverRegistrationService: new NoOpServerRegistrationService(),
            networkDiscovery: new ThrowingNetworkDiscovery(),
            delay: _ => Task.CompletedTask
        );

        Task loopTask = recovery.StartRecoveryLoop(tasks: tasks);
        await Task.Delay(delay: TimeSpan.FromSeconds(seconds: 2));
        tasks.AllCompleted = true;
        await loopTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 10));

        Assert.False(condition: tasks.NetworkDiscovered);
    }

    private sealed class ThrowingApiKeyLoader : IApiKeyLoader
    {
        public Task LoadKeys(CancellationToken ct = default) =>
            throw new InvalidOperationException(message: "simulated api key load failure");
    }

    private sealed class ThrowingNetworkDiscovery : INetworkDiscovery
    {
        public string InternalIp { get; set; } = "0.0.0.0";
        public string RegistrationInternalIp => "0.0.0.0";
        public string ExternalIp { get; set; } = "0.0.0.0";
        public string? InternalIpV6 => null;
        public string? ExternalIpV6 { get; set; }
        public string InternalDomain => "local";
        public string InternalAddress => "0.0.0.0";
        public string ExternalDomain => "local";
        public string ExternalAddress => "0.0.0.0";
        public string? ExternalAddressV6 => null;
        public bool Ipv6Enabled => false;

        public Task DiscoverExternalIpAsync() =>
            throw new InvalidOperationException(message: "simulated network discovery failure");

        public Task ForceRediscoveryAsync() => Task.CompletedTask;

        public Task<bool> IsPortOpenAsync() => Task.FromResult(result: false);
    }
}
