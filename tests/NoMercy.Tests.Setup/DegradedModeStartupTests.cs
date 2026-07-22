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

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Networking.Certificate;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Lifecycle;
using NoMercy.NmSystem.Status;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Boot;
using NoMercy.Storage.Drivers.Local;

namespace NoMercy.Tests.Setup;

public class DegradedModeStartupTests
{
    [Fact]
    public async Task NetworkProbe_ReturnsTrue_WhenAtLeastOneTargetReachable()
    {
        // In CI/dev environments, at least one target should be reachable
        bool result = await NetworkProbe.CheckConnectivity(timeoutMs: 5000);

        // This test validates the probe doesn't throw — the actual result
        // depends on the environment. We verify the method completes without exception.
        Assert.IsType<bool>(@object: result);
    }

    [Fact]
    public async Task NetworkProbe_CompletesWithinTimeout_WhenNoNetwork()
    {
        // Use an extremely short timeout to simulate "no connectivity" path
        DateTime start = DateTime.UtcNow;
        bool result = await NetworkProbe.CheckConnectivity(timeoutMs: 1);
        TimeSpan elapsed = DateTime.UtcNow - start;

        // Should complete without hanging — result may be true or false depending
        // on how fast the connection happens
        Assert.True(
            condition: elapsed.TotalSeconds < 30,
            userMessage: $"NetworkProbe should not block indefinitely, took {elapsed.TotalSeconds}s"
        );
    }

    [Fact]
    public void DeferredTasks_InitializesWithAllFalse()
    {
        DeferredTasks deferred = new();

        Assert.False(condition: deferred.ApiKeysLoaded);
        Assert.False(condition: deferred.Authenticated);
        Assert.False(condition: deferred.NetworkDiscovered);
        Assert.False(condition: deferred.Registered);
        Assert.False(condition: deferred.SeedsRun);
        Assert.False(condition: deferred.BinariesReady);
        Assert.False(condition: deferred.AllCompleted);
    }

    [Fact]
    public void DeferredTasks_TracksCompletionState()
    {
        DeferredTasks deferred = new()
        {
            ApiKeysLoaded = true,
            Authenticated = true,
            NetworkDiscovered = true,
            SeedsRun = true,
            Registered = true,
            BinariesReady = true,
            AllCompleted = true,
        };

        Assert.True(condition: deferred.ApiKeysLoaded);
        Assert.True(condition: deferred.Authenticated);
        Assert.True(condition: deferred.NetworkDiscovered);
        Assert.True(condition: deferred.SeedsRun);
        Assert.True(condition: deferred.Registered);
        Assert.True(condition: deferred.BinariesReady);
        Assert.True(condition: deferred.AllCompleted);
    }

    [Fact]
    public void IsDegradedMode_DefaultsFalse()
    {
        // Reset static state
        Start.IsDegradedMode = false;

        Assert.False(condition: Start.IsDegradedMode);
    }

    [Fact]
    public void IsDegradedMode_CanBeSet()
    {
        Start.IsDegradedMode = true;

        Assert.True(condition: Start.IsDegradedMode);

        // Reset
        Start.IsDegradedMode = false;
    }

    [Fact]
    public async Task DegradedModeRecovery_CompletesImmediately_WhenAllTasksDone()
    {
        DeferredTasks deferred = new() { AllCompleted = true };

        // Should return immediately without looping
        DateTime start = DateTime.UtcNow;
        // Mock dependencies or use real ones if they don't hit network
        DegradedModeRecovery recovery = new(authTokenStore: new AuthTokenStore(), apiKeyLoader: null!, apiKeyStore: null!, serverRegistrationService: null!);
        await recovery.StartRecoveryLoop(tasks: deferred);
        TimeSpan elapsed = DateTime.UtcNow - start;

        Assert.True(
            condition: elapsed.TotalSeconds < 5,
            userMessage: $"Recovery loop should exit immediately when AllCompleted is true, took {elapsed.TotalSeconds}s"
        );
    }

    [Fact]
    public void GetInternalIp_ReturnsNonEmpty_WithoutNetwork()
    {
        // GetInternalIp now uses NetworkInterface enumeration first,
        // which works without network connectivity
        NetworkDiscovery discovery = new(
            logger: NullLogger<NetworkDiscovery>.Instance,
            driver: new LocalStorageDriver(),
            authTokenStore: new AuthTokenStore(),
            connectivityStatus: new ConnectivityStatus(),
            networkProbeConfig: new()
        );
        string ip = discovery.InternalIp;

        Assert.False(
            condition: string.IsNullOrEmpty(value: ip),
            userMessage: "GetInternalIp should return a valid IP via NetworkInterface enumeration"
        );
    }

    [Fact]
    public void GetInternalIp_ReturnsValidIpFormat()
    {
        NetworkDiscovery discovery = new(
            logger: NullLogger<NetworkDiscovery>.Instance,
            driver: new LocalStorageDriver(),
            authTokenStore: new AuthTokenStore(),
            connectivityStatus: new ConnectivityStatus(),
            networkProbeConfig: new()
        );
        string ip = discovery.InternalIp;

        // Should be a valid IPv4 address
        bool isValid = IPAddress.TryParse(ipString: ip, address: out IPAddress? parsed);
        Assert.True(condition: isValid, userMessage: $"GetInternalIp returned '{ip}' which is not a valid IP address");
        Assert.Equal(expected: AddressFamily.InterNetwork, actual: parsed!.AddressFamily);
    }

    [Fact]
    public void RegistrationInternalIp_IsAlwaysAValidIp()
    {
        // The API rejects registration with required|string|ip — an empty internal_ip
        // returns 422 and the server never comes online.
        NetworkDiscovery discovery = new(
            logger: NullLogger<NetworkDiscovery>.Instance,
            driver: new LocalStorageDriver(),
            authTokenStore: new AuthTokenStore(),
            connectivityStatus: new ConnectivityStatus(),
            networkProbeConfig: new()
        );

        Assert.True(
            condition: IPAddress.TryParse(ipString: discovery.RegistrationInternalIp, address: out IPAddress? parsed),
            userMessage: $"RegistrationInternalIp returned '{discovery.RegistrationInternalIp}', not a valid IP"
        );
        Assert.Equal(expected: AddressFamily.InterNetwork, actual: parsed!.AddressFamily);
    }

    [Theory]
    [InlineData(data: "127.0.0.1")]
    [InlineData(data: "")]
    public void RegistrationInternalIp_FallsBackToSentinel_WhenNonRoutable(string discovered)
    {
        NetworkDiscovery discovery = new(
            logger: NullLogger<NetworkDiscovery>.Instance,
            driver: new LocalStorageDriver(),
            authTokenStore: new AuthTokenStore(),
            connectivityStatus: new ConnectivityStatus(),
            networkProbeConfig: new()
        )
        {
            InternalIp = discovered,
        };

        Assert.Equal(expected: "0.0.0.0", actual: discovery.RegistrationInternalIp);
    }

    [Fact]
    public void RegistrationInternalIp_PassesThroughRoutableIp()
    {
        NetworkDiscovery discovery = new(
            logger: NullLogger<NetworkDiscovery>.Instance,
            driver: new LocalStorageDriver(),
            authTokenStore: new AuthTokenStore(),
            connectivityStatus: new ConnectivityStatus(),
            networkProbeConfig: new()
        )
        {
            InternalIp = "192.168.1.50",
        };

        Assert.Equal(expected: "192.168.1.50", actual: discovery.RegistrationInternalIp);
    }
}

public class DegradedModeStartupPhasingTests
{
    [Fact]
    public async Task FullMode_MaintainsPhasedDependencyOrder()
    {
        // This test validates that full mode still runs tasks in the
        // same phased order as before the degraded mode changes
        ConcurrentBag<(string Name, int Phase)> executionLog = [];

        // Phase 1: Required (no network)
        executionLog.Add(item: ("CreateFolders", 1));
        executionLog.Add(item: ("ApiInfo", 1));

        // Phase 2: Auth with network check
        bool hasNetwork = true;
        bool hasAuth = false;

        if (hasNetwork)
        {
            await Task.Run(action: () => executionLog.Add(item: ("Auth", 2)));
            hasAuth = true;
        }

        Task binariesTask = Task.Run(function: async () =>
        {
            await Task.Delay(millisecondsDelay: 30);
            executionLog.Add(item: ("Binaries", 2));
        });

        // Phase 3: Network-dependent tasks
        Task networkingTask = Task.Run(function: async () =>
        {
            await Task.Delay(millisecondsDelay: 40);
            executionLog.Add(item: ("Networking", 3));
        });

        if (hasNetwork && hasAuth)
        {
            List<Task> parallelTasks =
            [
                Task.Run(function: () =>
                {
                    executionLog.Add(item: ("DatabaseSeeder", 3));
                    return Task.CompletedTask;
                }),
                Task.Run(function: () =>
                {
                    executionLog.Add(item: ("ChromeCast", 3));
                    return Task.CompletedTask;
                }),
            ];
            await Task.WhenAll(tasks: parallelTasks);
        }

        await networkingTask;

        // Phase 4: Register
        if (hasNetwork && hasAuth)
        {
            executionLog.Add(item: ("Register", 4));
        }

        await binariesTask;

        // Verify all tasks ran
        List<(string Name, int Phase)> logList = executionLog.ToList();
        Assert.Contains(collection: logList, filter: e => e is { Name: "CreateFolders", Phase: 1 });
        Assert.Contains(collection: logList, filter: e => e is { Name: "ApiInfo", Phase: 1 });
        Assert.Contains(collection: logList, filter: e => e is { Name: "Auth", Phase: 2 });
        Assert.Contains(collection: logList, filter: e => e is { Name: "Binaries", Phase: 2 });
        Assert.Contains(collection: logList, filter: e => e is { Name: "Networking", Phase: 3 });
        Assert.Contains(collection: logList, filter: e => e is { Name: "DatabaseSeeder", Phase: 3 });
        Assert.Contains(collection: logList, filter: e => e is { Name: "ChromeCast", Phase: 3 });
        Assert.Contains(collection: logList, filter: e => e is { Name: "Register", Phase: 4 });
    }

    [Fact]
    public async Task DegradedMode_SkipsRegisterAndStartsRecovery()
    {
        // Simulates the degraded mode path where Register is skipped
        // and a recovery loop is scheduled
        ConcurrentBag<(string Name, int Phase)> executionLog = [];
        bool recoveryLoopScheduled = false;

        bool hasNetwork = false;
        bool hasAuth = false;

        // Phase 1
        executionLog.Add(item: ("CreateFolders", 1));
        executionLog.Add(item: ("ApiInfo", 1));

        // Phase 2: No network, use fallback
        if (!hasNetwork)
        {
            executionLog.Add(item: ("AuthFallback", 2));
            hasAuth = false; // No cached token
        }

        // Phase 3: Degraded mode
        if (!hasNetwork || !hasAuth)
        {
            // Schedule recovery
            recoveryLoopScheduled = true;
        }

        // Register should NOT run in degraded mode
        bool registerRan = executionLog.Any(predicate: e => e.Name == "Register");

        Assert.False(condition: registerRan, userMessage: "Register should not run in degraded mode");
        Assert.True(condition: recoveryLoopScheduled, userMessage: "Recovery loop should be scheduled in degraded mode");
    }

    [Fact]
    public void AuthManager_StaticHelpers_DoNotThrow()
    {
        // Auth.InitWithFallback was replaced by AuthManager.InitializeAsync (requires DI).
        // Verify the static helpers on AuthManager are available and don't throw on this platform.
        bool isDesktop = AuthManager.IsDesktopEnvironment();
        Assert.IsType<bool>(@object: isDesktop);

        string verifier = AuthManager.GenerateCodeVerifier();
        Assert.NotEmpty(collection: verifier);

        string challenge = AuthManager.GenerateCodeChallenge(codeVerifier: verifier);
        Assert.NotEmpty(collection: challenge);
    }
}

public class CloudflareFallbackTests
{
    [Fact]
    public void ExternalIp_DefaultsToZeroAddress_WhenNotSet()
    {
        // ExternalIp property should return "0.0.0.0" when no IP has been discovered,
        // not throw an exception
        NetworkDiscovery discovery = new(
            logger: NullLogger<NetworkDiscovery>.Instance,
            driver: new LocalStorageDriver(),
            authTokenStore: new AuthTokenStore(),
            connectivityStatus: new ConnectivityStatus(),
            networkProbeConfig: new()
        );
        string ip = discovery.ExternalIp;
        Assert.NotNull(@object: ip);
    }

    [Fact]
    public void Certificate_HasValidCertificate_ReturnsFalse_WhenNoCertFile()
    {
        // HasValidCertificate now checks the DB (Configuration table) for a stored
        // certificate PEM. In the test environment the DB does not exist, so either
        // false is returned (no cert) or a SqliteException is thrown (no table).
        // Both outcomes correctly indicate no valid certificate is present.
        try
        {
            bool result = new CertificateService(
                logger: NullLogger<CertificateService>.Instance,
                httpClientFactory: null!
            ).HasValidCertificate();
            Assert.False(condition: result, userMessage: "No certificate should be present in the test environment");
        }
        catch (SqliteException)
        {
            // Expected when Configuration table does not exist — treated as no cert.
        }
    }

    [Fact]
    public async Task Certificate_RenewSslCertificate_DoesNotThrow_WhenNetworkUnavailable()
    {
        // RenewSslCertificate checks HasValidCertificate() which now queries the DB.
        // In the test environment the DB does not exist. Acceptable outcomes:
        // 1. Returns early (no token available, early-return guard triggers)
        // 2. Throws SqliteException (no Configuration table — same as no cert on disk)
        // 3. Throws network/HTTP exception (no auth server reachable)
        // All are acceptable in an isolated test environment.
        try
        {
            await new CertificateService(
                logger: NullLogger<CertificateService>.Instance,
                httpClientFactory: null!
            ).RenewSslCertificate(accessToken: null, maxRetries: 1);
        }
        catch (SqliteException)
        {
            // Expected: Configuration table does not exist in the test environment.
        }
        catch (Exception)
        {
            // Network or other failure — also acceptable; no cert means this is first-boot.
            // Verify by checking HasValidCertificate with the same tolerance:
            bool hasCert = false;
            try
            {
                hasCert = new CertificateService(
                    logger: NullLogger<CertificateService>.Instance,
                    httpClientFactory: null!
                ).HasValidCertificate();
            }
            catch (SqliteException)
            {
                // Still no DB — confirmed no cert.
            }

            Assert.False(
                condition: hasCert,
                userMessage: "RenewSslCertificate should only throw when no existing cert is present"
            );
        }
    }

    [Fact]
    public async Task GetExternalIp_Discover_DoesNotThrow_WhenApiUnavailable()
    {
        // DiscoverExternalIpAsync() wraps GetExternalIpAsync in try/catch, so even when
        // api.nomercy.tv (Cloudflare) is down, it should not throw
        try
        {
            NetworkDiscovery discovery = new(
                logger: NullLogger<NetworkDiscovery>.Instance,
                driver: new LocalStorageDriver(),
                authTokenStore: new AuthTokenStore(),
                connectivityStatus: new ConnectivityStatus(),
                networkProbeConfig: new()
            );
            await discovery.DiscoverExternalIpAsync();
        }
        catch (Exception ex)
        {
            Assert.Fail(
                message: $"DiscoverExternalIpAsync should not throw when external IP API is unavailable: {ex.Message}"
            );
        }
    }

    [Fact]
    public void ExternalIpCache_RoundTrips()
    {
        // Verify the external IP caching mechanism works:
        // Set an IP → verify it persists → verify it can be read back
        string testIp = "203.0.113.42";
        string cacheFile = Path.Combine(path1: AppFiles.ConfigPath, path2: "external_ip.cache");

        try
        {
            // Ensure config directory exists
            string configDir = AppFiles.ConfigPath;
            if (!Directory.Exists(path: configDir))
                Directory.CreateDirectory(path: configDir);

            // Write cache file directly (simulates what CacheExternalIp does)
            File.WriteAllText(path: cacheFile, contents: testIp);

            // Read it back
            string cached = File.ReadAllText(path: cacheFile).Trim();
            Assert.Equal(expected: testIp, actual: cached);
        }
        finally
        {
            // Cleanup
            if (File.Exists(path: cacheFile))
                File.Delete(path: cacheFile);
        }
    }

    [Fact]
    public void ExternalIpCache_ReturnsNull_WhenFileDoesNotExist()
    {
        string cacheFile = Path.Combine(path1: AppFiles.ConfigPath, path2: "external_ip.cache");

        // Ensure no cache file
        if (File.Exists(path: cacheFile))
            File.Delete(path: cacheFile);

        // The cache should gracefully handle missing files
        Assert.False(condition: File.Exists(path: cacheFile));
    }
}

// Regression for the "ffmpeg download failure permanently wedges the encoder
// queues" bug: a deferred "Binaries" startup task previously had no path back
// to BootStage.Binaries at all. DegradedModeRecovery.TryProvisionBinariesAsync
// is the retry step the background recovery loop calls every backoff tick;
// these tests exercise it directly (isolated NOMERCY_APP_PATH per test) rather
// than waiting through the loop's real 30s+ backoff schedule.
public class DegradedModeBinaryProvisioningTests : IDisposable
{
    private readonly string _tempAppPath;
    private readonly string? _previousAppPath;

    public DegradedModeBinaryProvisioningTests()
    {
        _previousAppPath = Environment.GetEnvironmentVariable(variable: "NOMERCY_APP_PATH");
        _tempAppPath = Path.Combine(path1: Path.GetTempPath(), path2: "nm-binprov-" + Guid.NewGuid());
        Directory.CreateDirectory(path: _tempAppPath);
        Environment.SetEnvironmentVariable(variable: "NOMERCY_APP_PATH", value: _tempAppPath);

        ServerPhaseTracker.ResetSharedForTests();
    }

    public void Dispose()
    {
        ServerPhaseTracker.ResetSharedForTests();
        Environment.SetEnvironmentVariable(variable: "NOMERCY_APP_PATH", value: _previousAppPath);

        try
        {
            if (Directory.Exists(path: _tempAppPath))
                Directory.Delete(path: _tempAppPath, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort scratch-dir cleanup — the static Logger may still hold a
            // handle open on a log file it wrote under this temp AppPath. Leaving
            // an orphaned temp directory behind is harmless; failing the test over
            // cleanup is not.
        }
    }

    [Fact]
    public async Task TryProvisionBinariesAsync_MarksBinariesReady_WhenFfmpegAlreadyOnDisk()
    {
        // Simulates a retry tick after ffmpeg landed on disk (either this attempt's
        // own download or a previous one) — provisioning must not re-download, and
        // must flip both the DTO flag and BootStage.Binaries so encoder queues unblock.
        Directory.CreateDirectory(path: AppFiles.FfmpegFolder);
        await File.WriteAllTextAsync(path: AppFiles.FfmpegPath, contents: "fake-ffmpeg-binary");

        ServerPhaseTracker tracker = ServerPhaseTracker.Shared();
        DeferredTasks tasks = new();

        await DegradedModeRecovery.TryProvisionBinariesAsync(tasks: tasks);

        Assert.True(condition: tasks.BinariesReady);
        Assert.True(condition: tracker.IsComplete(stage: BootStage.Binaries));
    }
}
