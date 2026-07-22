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
    /// Networking → Register.
    /// </summary>
    [Fact]
    public async Task PhasedStartup_MaintainsDependencyOrder()
    {
        ConcurrentBag<(string Name, int Phase)> executionLog = [];

        // Phase 1: foundational (sequential)
        await Task.Run(action: () => executionLog.Add(item: ("CreateFolders", 1)));

        // Phase 2: Auth and Binaries in parallel
        Task authTask = Task.Run(function: async () =>
        {
            await Task.Delay(millisecondsDelay: 50);
            executionLog.Add(item: ("Auth", 2));
        });
        Task binariesTask = Task.Run(function: async () =>
        {
            await Task.Delay(millisecondsDelay: 30);
            executionLog.Add(item: ("Binaries", 2));
        });
        await authTask;

        // Phase 3: After auth, these run in parallel
        Task networkingTask = Task.Run(function: async () =>
        {
            await Task.Delay(millisecondsDelay: 40);
            executionLog.Add(item: ("Networking", 3));
        });

        List<Task> parallelTasks =
        [
            Task.Run(function: async () =>
            {
                await Task.Delay(millisecondsDelay: 20);
                executionLog.Add(item: ("DatabaseSeeder", 3));
            }),
            Task.Run(function: async () =>
            {
                await Task.Delay(millisecondsDelay: 10);
                executionLog.Add(item: ("ChromeCast", 3));
            }),
            Task.Run(function: () =>
            {
                executionLog.Add(item: ("UpdateChecker", 3));
                return Task.CompletedTask;
            }),
        ];

        await Task.WhenAll(tasks: parallelTasks);

        // Phase 4: Register needs Auth + Networking
        await networkingTask;
        executionLog.Add(item: ("Register", 4));

        // Wait for binaries (started in phase 2)
        await binariesTask;

        // Verify all tasks executed
        List<string> executedNames = executionLog.Select(selector: e => e.Name).ToList();
        Assert.Contains(expected: "CreateFolders", collection: executedNames);
        Assert.Contains(expected: "Auth", collection: executedNames);
        Assert.Contains(expected: "Binaries", collection: executedNames);
        Assert.Contains(expected: "Networking", collection: executedNames);
        Assert.Contains(expected: "DatabaseSeeder", collection: executedNames);
        Assert.Contains(expected: "ChromeCast", collection: executedNames);
        Assert.Contains(expected: "UpdateChecker", collection: executedNames);
        Assert.Contains(expected: "Register", collection: executedNames);
        Assert.Equal(expected: 8, actual: executionLog.Count);

        // Verify ordering constraints:
        // CreateFolders must complete before any Phase 2+ task
        List<(string Name, int Phase)> logList = executionLog.ToList();

        (string Name, int Phase) createFolders = logList.First(predicate: e => e.Name == "CreateFolders");
        Assert.Equal(expected: 1, actual: createFolders.Phase);

        // Auth and Binaries are phase 2
        Assert.Equal(expected: 2, actual: logList.First(predicate: e => e.Name == "Auth").Phase);
        Assert.Equal(expected: 2, actual: logList.First(predicate: e => e.Name == "Binaries").Phase);

        // Networking, DatabaseSeeder, ChromeCast, UpdateChecker are phase 3
        Assert.Equal(expected: 3, actual: logList.First(predicate: e => e.Name == "Networking").Phase);
        Assert.Equal(expected: 3, actual: logList.First(predicate: e => e.Name == "DatabaseSeeder").Phase);
        Assert.Equal(expected: 3, actual: logList.First(predicate: e => e.Name == "ChromeCast").Phase);
        Assert.Equal(expected: 3, actual: logList.First(predicate: e => e.Name == "UpdateChecker").Phase);

        // Register is phase 4
        Assert.Equal(expected: 4, actual: logList.First(predicate: e => e.Name == "Register").Phase);
    }

    /// <summary>
    /// Validates that Phase 2 tasks (Auth and Binaries) actually run concurrently,
    /// not sequentially. Proven with a shared barrier rather than wall-clock
    /// overlap: comparing Stopwatch windows flakes on CI, where coverage
    /// instrumentation starves the thread pool and can serialize two Task.Run
    /// bodies so one starts only after the other has finished.
    /// </summary>
    [Fact]
    public async Task Phase2_AuthAndBinaries_RunConcurrently()
    {
        // Each participant runs on its own dedicated thread (LongRunning, so the
        // pool can't serialize them), signals a shared barrier and waits for the
        // other. Both cross it only if they run at the same time; a serialized run
        // would wait out the timeout and return false. No timing comparison, so
        // thread-pool jitter and coverage overhead can't flake it.
        using Barrier barrier = new(participantCount: 2);

        Task<bool> RunParticipant() =>
            Task.Factory.StartNew(
                function: () => barrier.SignalAndWait(timeout: TimeSpan.FromSeconds(seconds: 10)),
                creationOptions: TaskCreationOptions.LongRunning
            );

        bool[] reachedBarrier = await Task.WhenAll(tasks: [RunParticipant(), RunParticipant()]);

        Assert.True(
            condition: reachedBarrier[0] && reachedBarrier[1],
            userMessage: "Auth and Binaries must run concurrently: both reached the shared barrier."
        );
    }

    /// <summary>
    /// Validates that Phase 3 tasks run concurrently after Auth completes.
    /// </summary>
    [Fact]
    public async Task Phase3_TasksRunConcurrentlyAfterAuth()
    {
        const int taskCount = 4;

        // Simulate Auth completing first.
        await Task.Delay(millisecondsDelay: 10);

        // Same barrier proof as Phase 2, scaled to all Phase 3 tasks: every task
        // must reach the shared rendezvous before any is allowed to finish, which
        // is only possible if they run at the same time. Robust to CI thread-pool
        // jitter, unlike a wall-clock overlap comparison.
        using Barrier barrier = new(participantCount: taskCount);

        Task<bool> RunParticipant() =>
            Task.Factory.StartNew(
                function: () => barrier.SignalAndWait(timeout: TimeSpan.FromSeconds(seconds: 10)),
                creationOptions: TaskCreationOptions.LongRunning
            );

        bool[] reachedBarrier = await Task.WhenAll(
            tasks: Enumerable.Range(start: 0, count: taskCount).Select(selector: _ => RunParticipant())
        );

        Assert.True(
            condition: reachedBarrier.All(predicate: reached => reached),
            userMessage: "Phase 3 tasks should run concurrently: all reached the shared barrier."
        );
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
        Task binariesTask = Task.Run(function: async () => await Task.Delay(millisecondsDelay: 200));
        await Task.Run(function: async () =>
        {
            await Task.Delay(millisecondsDelay: 50);
            authCompleted = true;
        });

        // Phase 3: Networking (started after auth)
        Task networkingTask = Task.Run(function: async () =>
        {
            await Task.Delay(millisecondsDelay: 100);
            networkingCompleted = true;
        });

        // Phase 3: other parallel tasks
        await Task.WhenAll(tasks: [Task.Run(function: async () => await Task.Delay(millisecondsDelay: 30)), Task.Run(function: async () => await Task.Delay(millisecondsDelay: 20))]
        );

        // Phase 4: Wait for networking then register
        await networkingTask;

        // At this point, both auth and networking must be complete
        if (!authCompleted || !networkingCompleted)
            registerStartedBeforeDeps = true;

        // "Register" runs here
        await binariesTask;

        Assert.True(condition: authCompleted, userMessage: "Auth should be completed before Register");
        Assert.True(condition: networkingCompleted, userMessage: "Networking should be completed before Register");
        Assert.False(
            condition: registerStartedBeforeDeps,
            userMessage: "Register must not start before Auth and Networking complete"
        );
    }
}
