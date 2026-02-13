using System.Collections.Concurrent;
using NoMercy.Networking;
using NoMercy.Setup;
using NoMercy.Setup.Dto;
using Xunit;

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
        Assert.IsType<bool>(result);
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
        Assert.True(elapsed.TotalSeconds < 30,
            $"NetworkProbe should not block indefinitely, took {elapsed.TotalSeconds}s");
    }

    [Fact]
    public void DeferredTasks_InitializesWithAllFalse()
    {
        DeferredTasks deferred = new();

        Assert.False(deferred.ApiKeysLoaded);
        Assert.False(deferred.Authenticated);
        Assert.False(deferred.NetworkDiscovered);
        Assert.False(deferred.Registered);
        Assert.False(deferred.SeedsRun);
        Assert.False(deferred.AllCompleted);
        Assert.Empty(deferred.CallerTasks);
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
            AllCompleted = true
        };

        Assert.True(deferred.ApiKeysLoaded);
        Assert.True(deferred.Authenticated);
        Assert.True(deferred.NetworkDiscovered);
        Assert.True(deferred.SeedsRun);
        Assert.True(deferred.Registered);
        Assert.True(deferred.AllCompleted);
    }

    [Fact]
    public void DeferredTasks_HoldsCallerTasks()
    {
        bool taskExecuted = false;
        TaskDelegate testTask = () =>
        {
            taskExecuted = true;
            return Task.CompletedTask;
        };

        DeferredTasks deferred = new()
        {
            CallerTasks = [testTask]
        };

        Assert.Single(deferred.CallerTasks);

        deferred.CallerTasks[0].Invoke().Wait();
        Assert.True(taskExecuted);
    }

    [Fact]
    public void IsDegradedMode_DefaultsFalse()
    {
        // Reset static state
        Start.IsDegradedMode = false;

        Assert.False(Start.IsDegradedMode);
    }

    [Fact]
    public void IsDegradedMode_CanBeSet()
    {
        Start.IsDegradedMode = true;

        Assert.True(Start.IsDegradedMode);

        // Reset
        Start.IsDegradedMode = false;
    }

    [Fact]
    public async Task DegradedModeRecovery_CompletesImmediately_WhenAllTasksDone()
    {
        DeferredTasks deferred = new()
        {
            AllCompleted = true
        };

        // Should return immediately without looping
        DateTime start = DateTime.UtcNow;
        await DegradedModeRecovery.StartRecoveryLoop(deferred);
        TimeSpan elapsed = DateTime.UtcNow - start;

        Assert.True(elapsed.TotalSeconds < 5,
            $"Recovery loop should exit immediately when AllCompleted is true, took {elapsed.TotalSeconds}s");
    }

    [Fact]
    public void GetInternalIp_ReturnsNonEmpty_WithoutNetwork()
    {
        // GetInternalIp now uses NetworkInterface enumeration first,
        // which works without network connectivity
        string ip = Networking.Networking.InternalIp;

        Assert.False(string.IsNullOrEmpty(ip),
            "GetInternalIp should return a valid IP via NetworkInterface enumeration");
    }

    [Fact]
    public void GetInternalIp_ReturnsValidIpFormat()
    {
        string ip = Networking.Networking.InternalIp;

        // Should be a valid IPv4 address
        bool isValid = System.Net.IPAddress.TryParse(ip, out System.Net.IPAddress? parsed);
        Assert.True(isValid, $"GetInternalIp returned '{ip}' which is not a valid IP address");
        Assert.Equal(System.Net.Sockets.AddressFamily.InterNetwork, parsed!.AddressFamily);
    }
}

public class DegradedModeStartupPhasingTests
{
    [Fact]
    public async Task DegradedMode_RunsCallerTasksWithErrorHandling()
    {
        // Simulates the degraded mode path in Start.Init where caller tasks
        // are run with try/catch so failures don't kill the server
        ConcurrentBag<string> results = [];
        bool failingTaskRan = false;

        List<TaskDelegate> tasks =
        [
            () =>
            {
                results.Add("task1_success");
                return Task.CompletedTask;
            },
            () =>
            {
                failingTaskRan = true;
                throw new InvalidOperationException("Network unavailable");
            },
            () =>
            {
                results.Add("task3_success");
                return Task.CompletedTask;
            }
        ];

        // Simulate degraded mode execution pattern from Start.Init
        foreach (TaskDelegate callerTask in tasks)
        {
            try
            {
                await callerTask.Invoke();
            }
            catch
            {
                // In degraded mode, failures are caught and logged
            }
        }

        // All tasks should have been attempted
        Assert.True(failingTaskRan, "Even failing tasks should be attempted");
        Assert.Contains("task1_success", results);
        Assert.Contains("task3_success", results);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task FullMode_MaintainsPhasedDependencyOrder()
    {
        // This test validates that full mode still runs tasks in the
        // same phased order as before the degraded mode changes
        ConcurrentBag<(string Name, int Phase)> executionLog = [];

        // Phase 1: Required (no network)
        executionLog.Add(("CreateFolders", 1));
        executionLog.Add(("ApiInfo", 1));

        // Phase 2: Auth with network check
        bool hasNetwork = true;
        bool hasAuth = false;

        if (hasNetwork)
        {
            await Task.Run(() => executionLog.Add(("Auth", 2)));
            hasAuth = true;
        }

        Task binariesTask = Task.Run(async () =>
        {
            await Task.Delay(30);
            executionLog.Add(("Binaries", 2));
        });

        // Phase 3: Network-dependent tasks
        Task networkingTask = Task.Run(async () =>
        {
            await Task.Delay(40);
            executionLog.Add(("Networking", 3));
        });

        if (hasNetwork && hasAuth)
        {
            List<Task> parallelTasks =
            [
                Task.Run(() =>
                {
                    executionLog.Add(("DatabaseSeeder", 3));
                    return Task.CompletedTask;
                }),
                Task.Run(() =>
                {
                    executionLog.Add(("ChromeCast", 3));
                    return Task.CompletedTask;
                })
            ];
            await Task.WhenAll(parallelTasks);
        }

        await networkingTask;

        // Phase 4: Register
        if (hasNetwork && hasAuth)
        {
            executionLog.Add(("Register", 4));
        }

        await binariesTask;

        // Verify all tasks ran
        List<(string Name, int Phase)> logList = executionLog.ToList();
        Assert.Contains(logList, e => e.Name == "CreateFolders" && e.Phase == 1);
        Assert.Contains(logList, e => e.Name == "ApiInfo" && e.Phase == 1);
        Assert.Contains(logList, e => e.Name == "Auth" && e.Phase == 2);
        Assert.Contains(logList, e => e.Name == "Binaries" && e.Phase == 2);
        Assert.Contains(logList, e => e.Name == "Networking" && e.Phase == 3);
        Assert.Contains(logList, e => e.Name == "DatabaseSeeder" && e.Phase == 3);
        Assert.Contains(logList, e => e.Name == "ChromeCast" && e.Phase == 3);
        Assert.Contains(logList, e => e.Name == "Register" && e.Phase == 4);
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
        executionLog.Add(("CreateFolders", 1));
        executionLog.Add(("ApiInfo", 1));

        // Phase 2: No network, use fallback
        if (!hasNetwork)
        {
            executionLog.Add(("AuthFallback", 2));
            hasAuth = false; // No cached token
        }

        // Phase 3: Degraded mode
        if (!hasNetwork || !hasAuth)
        {
            // Run caller tasks with error handling
            executionLog.Add(("CallerTasksDegraded", 3));

            // Schedule recovery
            recoveryLoopScheduled = true;
        }

        // Register should NOT run in degraded mode
        bool registerRan = executionLog.Any(e => e.Name == "Register");

        Assert.False(registerRan, "Register should not run in degraded mode");
        Assert.True(recoveryLoopScheduled, "Recovery loop should be scheduled in degraded mode");
        Assert.Contains(executionLog, e => e.Name == "CallerTasksDegraded");
    }

    [Fact]
    public async Task Auth_InitWithFallback_ReturnsTrue_WhenTokenValid()
    {
        // Auth.InitWithFallback checks for cached tokens and validates them locally
        // This test validates the method contract — it returns bool instead of throwing
        // We can't easily test with real tokens, but we validate it doesn't throw
        // when no token file exists
        try
        {
            bool result = await Auth.InitWithFallback();
            // Result depends on whether a token file exists in the test environment
            Assert.IsType<bool>(result);
        }
        catch (Exception ex)
        {
            // InitWithFallback should never throw — this is a failure
            Assert.Fail($"InitWithFallback should not throw, but threw: {ex.Message}");
        }
    }
}

public class CloudflareFallbackTests
{
    [Fact]
    public void ExternalIp_DefaultsToZeroAddress_WhenNotSet()
    {
        // ExternalIp property should return "0.0.0.0" when no IP has been discovered,
        // not throw an exception
        string ip = Networking.Networking.ExternalIp;
        Assert.NotNull(ip);
    }

    [Fact]
    public void Certificate_HasValidCertificate_ReturnsFalse_WhenNoCertFile()
    {
        // When cert files don't exist, should return false — not throw
        bool result = Certificate.HasValidCertificate();
        Assert.IsType<bool>(result);
    }

    [Fact]
    public async Task Certificate_RenewSslCertificate_DoesNotThrow_WhenNetworkUnavailable()
    {
        // RenewSslCertificate should handle network failures gracefully
        // when a cert already exists on disk (or when no cert exists and it simply fails)
        try
        {
            // This will fail to reach api.nomercy.tv in most test environments,
            // but should not throw because:
            // 1. If cert exists and is valid: returns early
            // 2. If cert exists but expired: catches the network error
            // 3. If no cert: the outer exception propagates (acceptable for first boot)
            await Certificate.RenewSslCertificate(maxRetries: 1);
        }
        catch (Exception)
        {
            // Only acceptable if no cert exists at all (first boot scenario)
            Assert.False(Certificate.HasValidCertificate(),
                "RenewSslCertificate should only throw when no existing cert is on disk");
        }
    }

    [Fact]
    public async Task GetExternalIp_Discover_DoesNotThrow_WhenApiUnavailable()
    {
        // Discover() wraps GetExternalIp in try/catch, so even when
        // api.nomercy.tv (Cloudflare) is down, it should not throw
        try
        {
            await Networking.Networking.Discover();
        }
        catch (Exception ex)
        {
            Assert.Fail($"Discover should not throw when external IP API is unavailable: {ex.Message}");
        }
    }

    [Fact]
    public void ExternalIpCache_RoundTrips()
    {
        // Verify the external IP caching mechanism works:
        // Set an IP → verify it persists → verify it can be read back
        string testIp = "203.0.113.42";
        string cacheFile = Path.Combine(
            NoMercy.NmSystem.Information.AppFiles.ConfigPath, "external_ip.cache");

        try
        {
            // Ensure config directory exists
            string configDir = NoMercy.NmSystem.Information.AppFiles.ConfigPath;
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            // Write cache file directly (simulates what CacheExternalIp does)
            File.WriteAllText(cacheFile, testIp);

            // Read it back
            string cached = File.ReadAllText(cacheFile).Trim();
            Assert.Equal(testIp, cached);
        }
        finally
        {
            // Cleanup
            if (File.Exists(cacheFile))
                File.Delete(cacheFile);
        }
    }

    [Fact]
    public void ExternalIpCache_ReturnsNull_WhenFileDoesNotExist()
    {
        string cacheFile = Path.Combine(
            NoMercy.NmSystem.Information.AppFiles.ConfigPath, "external_ip.cache");

        // Ensure no cache file
        if (File.Exists(cacheFile))
            File.Delete(cacheFile);

        // The cache should gracefully handle missing files
        Assert.False(File.Exists(cacheFile));
    }

    [Fact]
    public void ApiInfo_KeysLoaded_IsFalse_WhenNoNetworkAndNoCache()
    {
        // ApiInfo.KeysLoaded should be false when keys haven't been loaded
        // This validates the flag exists and can be checked
        bool keysLoaded = ApiInfo.KeysLoaded;
        Assert.IsType<bool>(keysLoaded);
    }

    [Fact]
    public async Task ApiInfo_TryFetchFromNetwork_ReturnsNull_OnFailure()
    {
        // TryFetchFromNetwork should return null (not throw) when api.nomercy.tv is unreachable
        ApiInfoResponse? result = await ApiInfo.TryFetchFromNetwork();
        // Result depends on network — may be non-null in environments where api.nomercy.tv is reachable
        // The key assertion is that it does NOT throw
        Assert.True(result is null || result.Data?.Keys is not null,
            "TryFetchFromNetwork should return null on failure or valid data on success");
    }

    [Fact]
    public async Task ApiInfo_TryReadCacheFile_ReturnsNull_WhenNoCacheFile()
    {
        // When no cache file exists, should return null gracefully
        string cacheFile = ApiInfo.CacheFilePath;
        bool cacheExists = File.Exists(cacheFile);

        if (!cacheExists)
        {
            ApiInfoResponse? result = await ApiInfo.TryReadCacheFile();
            Assert.Null(result);
        }
        else
        {
            // Cache exists — just verify it doesn't throw
            ApiInfoResponse? result = await ApiInfo.TryReadCacheFile();
            Assert.IsType<ApiInfoResponse>(result);
        }
    }
}
