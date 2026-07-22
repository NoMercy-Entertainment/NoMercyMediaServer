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
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Drives the real <see cref="QueueRunner"/> — not a reimplementation of its
/// pause/resume bookkeeping — against a real (in-process) configuration
/// store and real <see cref="NoMercyQueue.Workers.QueueWorker"/> instances,
/// asserting the actual effect: a paused queue does not process work, the
/// paused flag survives being read back from the store (the mechanism that
/// makes "pause survives a restart" true), and increasing a queue's worker
/// count actually raises the number of jobs it runs concurrently.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class QueueRunnerBehaviorTests
{
    /// <summary>
    /// Real read-after-write semantics (not a mock) — Pause/Resume/Initialize
    /// all round-trip through this exactly like <c>MediaConfigurationStore</c>
    /// does against the DB.
    /// </summary>
    private sealed class InMemoryConfigurationStore : IConfigurationStore
    {
        private readonly ConcurrentDictionary<string, string> _values = new();

        public string? GetValue(string key) => _values.GetValueOrDefault(key: key);

        public void SetValue(string key, string value) => _values[key: key] = value;

        public Task SetValueAsync(string key, string value, Guid? modifiedBy = null)
        {
            _values[key: key] = value;
            return Task.CompletedTask;
        }

        public bool HasKey(string key) => _values.ContainsKey(key: key);
    }

    private static QueueRunner BuildRunner(
        TestQueueContextAdapter context,
        InMemoryConfigurationStore configStore,
        string queueName,
        int workerCount = 1
    )
    {
        QueueConfiguration configuration = new()
        {
            WorkerCounts = new() { [key: queueName] = workerCount },
        };
        return new(queueContext: context, configuration: configuration, loggerFactory: NullLoggerFactory.Instance, configurationStore: configStore);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, string failureMessage)
    {
        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 5));
        while (!cts.IsCancellationRequested)
        {
            if (predicate())
                return;
            await Task.Delay(millisecondsDelay: 10);
        }
        throw new TimeoutException(message: failureMessage);
    }

    [Fact]
    public async Task Pause_StopsWorker_JobIsNotReserved_AndIsPausedReflectsIt()
    {
        const string queue = "pause-behavior";
        TestQueueContextAdapter context = new();
        InMemoryConfigurationStore configStore = new();
        QueueRunner runner = BuildRunner(context: context, configStore: configStore, queueName: queue);
        await runner.Initialize();

        await runner.Pause(name: queue);

        runner.IsPaused(name: queue).Should().BeTrue();
        configStore.GetValue(key: $"queue.{queue}.paused").Should().Be(expected: "true");

        runner.Queue.Enqueue(
            queueJob: new QueueJobModel
            {
                Queue = queue,
                Payload = SerializationHelper.Serialize(
                    obj: new TestJob { Message = "should stay queued" }
                ),
                AvailableAt = DateTime.UtcNow,
            }
        );

        await Task.Delay(millisecondsDelay: 300);
        context
            .Jobs.Should()
            .ContainSingle(
                predicate: j => j.ReservedAt == null,
                because: "a paused queue's worker must not reserve new work"
            );

        await runner.StopAll();
    }

    [Fact]
    public async Task Resume_ClearsPausedState_AndWorkerProcessesQueuedJob()
    {
        const string queue = "resume-behavior";
        TestQueueContextAdapter context = new();
        InMemoryConfigurationStore configStore = new();
        QueueRunner runner = BuildRunner(context: context, configStore: configStore, queueName: queue);
        await runner.Initialize();
        await runner.Pause(name: queue);

        runner.Queue.Enqueue(
            queueJob: new QueueJobModel
            {
                Queue = queue,
                Payload = SerializationHelper.Serialize(
                    obj: new TestJob { Message = "runs after resume" }
                ),
                AvailableAt = DateTime.UtcNow,
            }
        );

        await runner.Resume(name: queue);

        runner.IsPaused(name: queue).Should().BeFalse();
        configStore.GetValue(key: $"queue.{queue}.paused").Should().Be(expected: "false");

        await WaitUntilAsync(
            predicate: () => context.Jobs.Count == 0,
            failureMessage: "resuming the queue must let its worker pick up and complete the queued job"
        );

        await runner.StopAll();
    }

    [Fact]
    public async Task Initialize_RestoresPersistedPause_QueueStaysPausedAcrossRestart()
    {
        const string queue = "restore-pause-behavior";
        TestQueueContextAdapter context = new();
        InMemoryConfigurationStore configStore = new();
        // Simulates the state left behind by a PREVIOUS QueueRunner instance
        // that was paused before the process exited.
        configStore.SetValue(key: $"queue.{queue}.paused", value: "true");

        QueueRunner runner = BuildRunner(context: context, configStore: configStore, queueName: queue);
        await runner.Initialize();

        runner.IsPaused(name: queue).Should().BeTrue();

        runner.Queue.Enqueue(
            queueJob: new QueueJobModel
            {
                Queue = queue,
                Payload = SerializationHelper.Serialize(obj: new TestJob { Message = "must not run" }),
                AvailableAt = DateTime.UtcNow,
            }
        );

        await Task.Delay(millisecondsDelay: 300);
        context.Jobs.Should().ContainSingle(predicate: j => j.ReservedAt == null);

        await runner.StopAll();
    }

    [Fact]
    public async Task SetWorkerCount_UnknownQueueName_ReturnsFalse()
    {
        TestQueueContextAdapter context = new();
        InMemoryConfigurationStore configStore = new();
        QueueRunner runner = BuildRunner(context: context, configStore: configStore, queueName: "known-queue");
        await runner.Initialize();

        bool result = await runner.SetWorkerCount(name: "no-such-queue", max: 5, userId: null);

        result.Should().BeFalse();
        await runner.StopAll();
    }

    [Fact]
    public async Task SetWorkerCount_Increase_ActuallyRunsMoreJobsConcurrently()
    {
        const string queue = "concurrency-behavior";
        TestQueueContextAdapter context = new();
        InMemoryConfigurationStore configStore = new();
        // Start with a single worker — if SetWorkerCount's increase never
        // took effect, only one of the three barrier-waiting jobs below
        // would ever run and this test would time out.
        QueueRunner runner = BuildRunner(context: context, configStore: configStore, queueName: queue, workerCount: 1);
        await runner.Initialize();

        bool changed = await runner.SetWorkerCount(name: queue, max: 3, userId: null);
        changed.Should().BeTrue();

        string probeKey = Guid.NewGuid().ToString();
        using CountdownEvent started = new(initialCount: 3);
        using ManualResetEventSlim release = new(initialState: false);
        ConcurrencyProbeJob.Probes[key: probeKey] = (started, release);

        for (int i = 0; i < 3; i++)
        {
            runner.Queue.Enqueue(
                queueJob: new QueueJobModel
                {
                    Queue = queue,
                    Payload = SerializationHelper.Serialize(
                        obj: new ConcurrencyProbeJob { ProbeKey = probeKey }
                    ),
                    AvailableAt = DateTime.UtcNow,
                }
            );
        }

        // If fewer than 3 workers are actually running, the later jobs stay
        // unreserved behind earlier ones still blocked on the unreleased
        // gate below — the start-countdown only reaches zero if 3 jobs are
        // genuinely running AT ONCE.
        bool allStarted = started.Wait(timeout: TimeSpan.FromSeconds(seconds: 10));
        allStarted
            .Should()
            .BeTrue(because: "increasing the worker count to 3 must let 3 jobs run concurrently");
        release.Set();

        await runner.StopAll();
    }
}
