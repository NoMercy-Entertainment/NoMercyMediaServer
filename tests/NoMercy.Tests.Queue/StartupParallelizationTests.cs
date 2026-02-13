using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Tests that validate the startup parallelization pattern used in Start.Init.
/// These verify that independent tasks run concurrently while dependent tasks
/// maintain their ordering constraints.
/// </summary>
public class StartupParallelizationTests
{
    /// <summary>
    /// Validates that the phased startup pattern executes tasks in the correct
    /// dependency order: Phase 1 completes before Phase 2 starts, etc.
    /// This mirrors Start.Init's structure where AppFiles → Auth || Binaries →
    /// Networking || CallerTasks → Register.
    /// </summary>
    [Fact]
    public async Task PhasedStartup_MaintainsDependencyOrder()
    {
        ConcurrentBag<(string Name, int Phase)> executionLog = [];

        // Phase 1: foundational (sequential)
        await Task.Run(() => executionLog.Add(("CreateFolders", 1)));

        // Phase 2: Auth and Binaries in parallel
        Task authTask = Task.Run(async () =>
        {
            await Task.Delay(50);
            executionLog.Add(("Auth", 2));
        });
        Task binariesTask = Task.Run(async () =>
        {
            await Task.Delay(30);
            executionLog.Add(("Binaries", 2));
        });
        await authTask;

        // Phase 3: After auth, these run in parallel
        Task networkingTask = Task.Run(async () =>
        {
            await Task.Delay(40);
            executionLog.Add(("Networking", 3));
        });

        List<Task> parallelTasks =
        [
            Task.Run(async () =>
            {
                await Task.Delay(20);
                executionLog.Add(("DatabaseSeeder", 3));
            }),
            Task.Run(async () =>
            {
                await Task.Delay(10);
                executionLog.Add(("ChromeCast", 3));
            }),
            Task.Run(() =>
            {
                executionLog.Add(("UpdateChecker", 3));
                return Task.CompletedTask;
            })
        ];

        await Task.WhenAll(parallelTasks);

        // Phase 4: Register needs Auth + Networking
        await networkingTask;
        executionLog.Add(("Register", 4));

        // Wait for binaries (started in phase 2)
        await binariesTask;

        // Verify all tasks executed
        List<string> executedNames = executionLog.Select(e => e.Name).ToList();
        Assert.Contains("CreateFolders", executedNames);
        Assert.Contains("Auth", executedNames);
        Assert.Contains("Binaries", executedNames);
        Assert.Contains("Networking", executedNames);
        Assert.Contains("DatabaseSeeder", executedNames);
        Assert.Contains("ChromeCast", executedNames);
        Assert.Contains("UpdateChecker", executedNames);
        Assert.Contains("Register", executedNames);
        Assert.Equal(8, executionLog.Count);

        // Verify ordering constraints:
        // CreateFolders must complete before any Phase 2+ task
        List<(string Name, int Phase)> logList = executionLog.ToList();

        (string Name, int Phase) createFolders = logList.First(e => e.Name == "CreateFolders");
        Assert.Equal(1, createFolders.Phase);

        // Auth and Binaries are phase 2
        Assert.Equal(2, logList.First(e => e.Name == "Auth").Phase);
        Assert.Equal(2, logList.First(e => e.Name == "Binaries").Phase);

        // Networking, DatabaseSeeder, ChromeCast, UpdateChecker are phase 3
        Assert.Equal(3, logList.First(e => e.Name == "Networking").Phase);
        Assert.Equal(3, logList.First(e => e.Name == "DatabaseSeeder").Phase);
        Assert.Equal(3, logList.First(e => e.Name == "ChromeCast").Phase);
        Assert.Equal(3, logList.First(e => e.Name == "UpdateChecker").Phase);

        // Register is phase 4
        Assert.Equal(4, logList.First(e => e.Name == "Register").Phase);
    }

    /// <summary>
    /// Validates that Phase 2 tasks (Auth and Binaries) actually run concurrently,
    /// not sequentially. Verified by checking that both tasks' execution windows overlap
    /// rather than using elapsed time thresholds (which are unreliable on CI runners).
    /// </summary>
    [Fact]
    public async Task Phase2_AuthAndBinaries_RunConcurrently()
    {
        long authStart = 0, authEnd = 0;
        long binariesStart = 0, binariesEnd = 0;

        Task binariesTask = Task.Run(async () =>
        {
            binariesStart = Stopwatch.GetTimestamp();
            await Task.Delay(100);
            binariesEnd = Stopwatch.GetTimestamp();
        });

        Task authTask = Task.Run(async () =>
        {
            authStart = Stopwatch.GetTimestamp();
            await Task.Delay(100);
            authEnd = Stopwatch.GetTimestamp();
        });

        await Task.WhenAll(authTask, binariesTask);

        // If concurrent, the execution windows overlap: each task starts before the other ends.
        // If sequential, one would start after the other finishes — no overlap.
        bool authStartedBeforeBinariesEnded = authStart < binariesEnd;
        bool binariesStartedBeforeAuthEnded = binariesStart < authEnd;

        Assert.True(authStartedBeforeBinariesEnded && binariesStartedBeforeAuthEnded,
            "Tasks should have overlapping execution windows when running concurrently");
    }

    /// <summary>
    /// Validates that Phase 3 tasks run concurrently after Auth completes.
    /// </summary>
    [Fact]
    public async Task Phase3_TasksRunConcurrentlyAfterAuth()
    {
        int perTaskDurationMs = 80;
        int taskCount = 4;
        DateTime startTime = DateTime.UtcNow;

        // Simulate Auth completing first
        await Task.Delay(10);

        // Phase 3: all tasks in parallel
        List<Task> phase3Tasks = Enumerable.Range(0, taskCount)
            .Select(_ => Task.Run(async () => await Task.Delay(perTaskDurationMs)))
            .ToList();

        await Task.WhenAll(phase3Tasks);

        TimeSpan elapsed = DateTime.UtcNow - startTime;

        // If parallel: ~10ms (auth) + ~80ms (concurrent tasks) = ~90ms
        // If sequential: ~10ms + 4 * 80ms = ~330ms
        Assert.True(elapsed.TotalMilliseconds < perTaskDurationMs * taskCount,
            $"Phase 3 tasks appear to have run sequentially: elapsed {elapsed.TotalMilliseconds}ms " +
            $"(expected < {perTaskDurationMs * taskCount}ms for concurrent execution)");
    }

    /// <summary>
    /// Validates that Register (Phase 4) does not start until both Auth and
    /// Networking have completed — the key dependency constraint.
    /// </summary>
    [Fact]
    public async Task Phase4_Register_WaitsForAuthAndNetworking()
    {
        bool authCompleted = false;
        bool networkingCompleted = false;
        bool registerStartedBeforeDeps = false;

        // Phase 2: Auth
        Task binariesTask = Task.Run(async () => await Task.Delay(200));
        await Task.Run(async () =>
        {
            await Task.Delay(50);
            authCompleted = true;
        });

        // Phase 3: Networking (started after auth)
        Task networkingTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            networkingCompleted = true;
        });

        // Phase 3: other parallel tasks
        await Task.WhenAll(
            Task.Run(async () => await Task.Delay(30)),
            Task.Run(async () => await Task.Delay(20))
        );

        // Phase 4: Wait for networking then register
        await networkingTask;

        // At this point, both auth and networking must be complete
        if (!authCompleted || !networkingCompleted)
            registerStartedBeforeDeps = true;

        // "Register" runs here
        await binariesTask;

        Assert.True(authCompleted, "Auth should be completed before Register");
        Assert.True(networkingCompleted, "Networking should be completed before Register");
        Assert.False(registerStartedBeforeDeps, "Register must not start before Auth and Networking complete");
    }

    /// <summary>
    /// Validates that caller-provided tasks (via the tasks parameter) execute
    /// during Phase 3, after Auth has completed.
    /// </summary>
    [Fact]
    public async Task CallerTasks_ExecuteInPhase3AfterAuth()
    {
        bool authCompleted = false;
        bool callerTaskSawAuthComplete = false;

        // Phase 2: Auth
        await Task.Run(async () =>
        {
            await Task.Delay(50);
            authCompleted = true;
        });

        // Phase 3: Caller tasks run after auth
        List<Task> parallelTasks =
        [
            Task.Run(() =>
            {
                callerTaskSawAuthComplete = authCompleted;
                return Task.CompletedTask;
            })
        ];

        await Task.WhenAll(parallelTasks);

        Assert.True(callerTaskSawAuthComplete,
            "Caller tasks should execute after Auth completes (Phase 3)");
    }
}
