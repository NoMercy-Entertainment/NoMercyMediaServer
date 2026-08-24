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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercy.Queue.MediaServer;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Workers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// End-to-end engine tests that drive the REAL <see cref="QueueWorker.StartAsync"/>
/// path rather than simulating the poll loop manually in the test body.
///
/// Each test:
///   1. Injects the failure condition (throwing stub job / seeded orphan row).
///   2. Drives the real engine (worker loop or recovery service).
///   3. Asserts the recovery/state-machine outcome in the DB — not that the
///      machinery "ran".
/// </summary>
[Collection("QueueEngine")]
public class QueueEngineEndToEndTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public QueueEngineEndToEndTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        _jobQueue = new(_adapter, maxAttempts: 3);
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
    }

    // ── Dispatch lands on the named queue ──────────────────────────────────

    [Fact]
    public void Dispatch_EnqueuesJobOntoExactlyTheNamedQueue()
    {
        JobDispatcher dispatcher = new(_jobQueue, NullLogger<JobDispatcher>.Instance);
        NamedQueueStubJob job = new() { TargetQueue = QueueNames.Library };

        dispatcher.Dispatch(job, QueueNames.Library, priority: 5);

        QueueJob? stored = _context.QueueJobs.SingleOrDefault();
        stored.Should().NotBeNull("the job must be persisted");
        stored!.Queue.Should().Be(QueueNames.Library, "queue name must match the dispatch target");
        stored.Priority.Should().Be(5);
        stored.Attempts.Should().Be(0, "a freshly dispatched job has zero attempts");
        stored.ReservedAt.Should().BeNull("a freshly dispatched job is unreserved");
    }

    [Fact]
    public void Dispatch_ToTwoDistinctQueues_EachQueueReceivesExactlyItsOwnJob()
    {
        JobDispatcher dispatcher = new(_jobQueue, NullLogger<JobDispatcher>.Instance);
        NamedQueueStubJob libraryJob = new() { TargetQueue = QueueNames.Library };
        NamedQueueStubJob imageJob = new() { TargetQueue = QueueNames.Image };

        dispatcher.Dispatch(libraryJob, QueueNames.Library, priority: 1);
        dispatcher.Dispatch(imageJob, QueueNames.Image, priority: 1);

        _context.QueueJobs.Count().Should().Be(2);

        QueueJobModel? fromLibrary = _jobQueue.ReserveJob(QueueNames.Library, null);
        fromLibrary.Should().NotBeNull();
        fromLibrary!.Queue.Should().Be(QueueNames.Library);

        QueueJobModel? fromImage = _jobQueue.ReserveJob(QueueNames.Image, null);
        fromImage.Should().NotBeNull();
        fromImage!.Queue.Should().Be(QueueNames.Image);

        QueueJobModel? wrongQueue = _jobQueue.ReserveJob(QueueNames.Music, null);
        wrongQueue.Should().BeNull("no jobs were dispatched to the music queue");
    }

    // ── Real worker: successful execution removes the job ─────────────────

    [Fact]
    public async Task Worker_SuccessfulJob_DeletesJobFromQueueAfterHandle()
    {
        SemaphoreSlim done = new(0, 1);
        string jobKey = Guid.NewGuid().ToString("N");
        CompletionSignalJob.RegisterCompletion(jobKey, () => done.Release());

        CompletionSignalJob stub = new() { JobKey = jobKey };
        _jobQueue.Enqueue(
            new()
            {
                Queue = "worker-success",
                Payload = SerializationHelper.Serialize(stub),
                AvailableAt = DateTime.UtcNow,
            }
        );

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        QueueWorker worker = new(_jobQueue, "worker-success");

        Task workerTask = worker.StartAsync(cts.Token);

        bool signalled = await done.WaitAsync(TimeSpan.FromSeconds(8));
        worker.Stop();
        cts.Cancel();
        await workerTask;

        signalled.Should().BeTrue("the job must execute before the timeout");
        _context.QueueJobs.Count().Should().Be(0, "completed job must be deleted from the queue");
        _context
            .FailedJobs.Count()
            .Should()
            .Be(0, "a successful job produces no failed-job record");
    }

    // ── Real worker: retry on transient failure ────────────────────────────

    /// <summary>
    /// The real engine: job throws on attempt 1 → worker calls FailJob →
    /// job stays in queue with Attempts=1, ReservedAt=null → worker loops →
    /// picks it up again → succeeds on attempt 2 → DeleteJob.
    /// Assert attempt count advanced and final DB state is clean.
    /// </summary>
    [Fact]
    public async Task Worker_JobThrowsOnFirstAttempt_RetrySucceedsAndJobIsDeleted()
    {
        SemaphoreSlim done = new(0, 1);
        string jobKey = Guid.NewGuid().ToString("N");
        FailOnceThenSucceedJob.Configure(jobKey, failCount: 1, onSuccess: () => done.Release());

        FailOnceThenSucceedJob stub = new() { JobKey = jobKey };
        _jobQueue.Enqueue(
            new()
            {
                Queue = "retry-e2e",
                Payload = SerializationHelper.Serialize(stub),
                AvailableAt = DateTime.UtcNow,
            }
        );

        _context.QueueJobs.Count().Should().Be(1, "job must be in queue before worker starts");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        QueueWorker worker = new(_jobQueue, "retry-e2e");

        Task workerTask = worker.StartAsync(cts.Token);

        bool signalled = await done.WaitAsync(TimeSpan.FromSeconds(12));
        worker.Stop();
        cts.Cancel();
        await workerTask;

        signalled.Should().BeTrue("job must succeed on retry before timeout");

        _context
            .QueueJobs.Count()
            .Should()
            .Be(0, "job that succeeded after retry must be deleted from queue");
        _context
            .FailedJobs.Count()
            .Should()
            .Be(0, "a job that succeeded within maxAttempts produces no failed-job record");
    }

    // ── Real worker: exhausted retries → terminal FailedJobs ─────────────

    [Fact]
    public async Task Worker_JobExhaustsRetries_TransitionsToTerminalFailedState()
    {
        string jobKey = Guid.NewGuid().ToString("N");
        FailOnceThenSucceedJob.Configure(jobKey, failCount: 99);

        FailOnceThenSucceedJob stub = new() { JobKey = jobKey };
        _jobQueue.Enqueue(
            new()
            {
                Queue = "exhaust-retries",
                Payload = SerializationHelper.Serialize(stub),
                AvailableAt = DateTime.UtcNow,
            }
        );

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
        QueueWorker worker = new(_jobQueue, "exhaust-retries");
        Task workerTask = worker.StartAsync(cts.Token);

        // Recording the failure and removing the queue row are two writes, so
        // waiting on the first one alone leaves the second in flight and the
        // QueueJobs assertion below racing it. Waiting for both is still a
        // real wait: if the row is never removed the deadline expires and the
        // assertion fails, which is what it is there for.
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (_context.FailedJobs.Count() > 0 && _context.QueueJobs.Count() == 0)
                break;
            await Task.Delay(50, CancellationToken.None);
        }

        worker.Stop();
        cts.Cancel();
        await workerTask;

        _context
            .QueueJobs.Count()
            .Should()
            .Be(0, "a job at max-attempts must be removed from QueueJobs");
        _context
            .FailedJobs.Count()
            .Should()
            .Be(1, "exhausted job must appear in FailedJobs exactly once");

        FailedJob terminal = _context.FailedJobs.Single();
        terminal.Queue.Should().Be("exhaust-retries", "queue name preserved in FailedJob record");
        terminal.Exception.Should().NotBeNullOrEmpty("exception content must be serialized");
        terminal
            .FailedAt.Should()
            .BeBefore(DateTime.UtcNow.AddSeconds(1), "FailedAt must not be in the future");
        terminal
            .FailedAt.Should()
            .BeAfter(DateTime.UtcNow.AddSeconds(-60), "FailedAt must be within this test run");
    }

    // ── Orphan recovery on real EfQueueContextAdapter ─────────────────────

    /// <summary>
    /// Seeds a job row directly into the EF-backed in-memory DB with
    /// ReservedAt set far in the past and Attempts beyond the single-retry
    /// budget orphan recovery grants a first-time orphan.
    /// Runs <see cref="OrphanJobRecoveryHostedService"/> and asserts the row
    /// is moved to FailedJobs — proving recovery works against the real
    /// EfQueueContextAdapter, not the in-memory-list stub.
    /// </summary>
    [Fact]
    public async Task OrphanRecovery_StuckJobInterruptedTooOftenMovedToFailedJobsViaRealAdapter()
    {
        // Attempts alone can no longer retire a boot orphan: the process
        // going away is not the job failing, so the attempt is refunded. Only
        // a job that has been interrupted its whole budget of times — the one
        // that takes the host down every run — is dead-lettered here.
        QueueJob orphan = new()
        {
            Queue = QueueNames.Library,
            Payload = SerializationHelper.Serialize(new TestJob { Message = "orphan" }),
            AvailableAt = DateTime.UtcNow.AddHours(-2),
            ReservedAt = DateTime.UtcNow.AddMinutes(-10),
            Attempts = 2,
            Interruptions = OrphanRecoveryTriage.MaxInterruptions - 1,
        };
        _context.QueueJobs.Add(orphan);
        await _context.SaveChangesAsync();

        ServiceCollection services = new();
        services.AddSingleton<IQueueContext>(_adapter);
        ServiceProvider provider = services.BuildServiceProvider();

        OrphanJobRecoveryHostedService recovery = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OrphanJobRecoveryHostedService>.Instance
        );

        await recovery.StartAsync(CancellationToken.None);
        if (recovery.ExecuteTask is not null)
            await recovery.ExecuteTask;

        _context
            .QueueJobs.Count()
            .Should()
            .Be(0, "an orphan out of interruptions must be removed from QueueJobs");
        _context
            .FailedJobs.Count()
            .Should()
            .Be(1, "an orphan out of interruptions must be moved to FailedJobs");

        FailedJob failed = _context.FailedJobs.Single();
        failed.Queue.Should().Be(QueueNames.Library);
        failed
            .Exception.Should()
            .Be(
                "job.interrupted_no_checkpoint",
                "orphan failure reason must be the sentinel string"
            );
        failed.Uuid.Should().NotBe(Guid.Empty, "orphan FailedJob must get a fresh UUID");
    }

    [Fact]
    public async Task OrphanRecovery_StuckJobOnFirstAttempt_LeftInQueueForRetry()
    {
        QueueJob firstTimeOrphan = new()
        {
            Queue = QueueNames.Library,
            Payload = SerializationHelper.Serialize(new TestJob { Message = "first-time" }),
            AvailableAt = DateTime.UtcNow.AddHours(-1),
            ReservedAt = DateTime.UtcNow.AddMinutes(-5),
            Attempts = 1,
        };
        _context.QueueJobs.Add(firstTimeOrphan);
        await _context.SaveChangesAsync();

        ServiceCollection services = new();
        services.AddSingleton<IQueueContext>(_adapter);
        ServiceProvider provider = services.BuildServiceProvider();

        OrphanJobRecoveryHostedService recovery = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OrphanJobRecoveryHostedService>.Instance
        );

        await recovery.StartAsync(CancellationToken.None);
        if (recovery.ExecuteTask is not null)
            await recovery.ExecuteTask;

        _context
            .QueueJobs.Count()
            .Should()
            .Be(1, "a first-attempt orphan must remain in queue for one retry");
        _context.FailedJobs.Count().Should().Be(0, "first-attempt orphan must not be failed");

        QueueJob survivor = _context.QueueJobs.Single();
        survivor.ReservedAt.Should().BeNull("the clean-retry reset must release the reservation");
        survivor.Attempts.Should().Be(0, "the clean-retry reset must refund the attempt budget");
    }

    [Fact]
    public async Task OrphanRecovery_RecentlyReservedJob_IsNotReclaimed()
    {
        QueueJob activeJob = new()
        {
            Queue = QueueNames.Encoder,
            Payload = SerializationHelper.Serialize(new TestJob { Message = "active" }),
            AvailableAt = DateTime.UtcNow.AddMinutes(-1),
            ReservedAt = DateTime.UtcNow.AddSeconds(-10),
            Attempts = 2,
        };
        _context.QueueJobs.Add(activeJob);
        await _context.SaveChangesAsync();

        ServiceCollection services = new();
        services.AddSingleton<IQueueContext>(_adapter);
        ServiceProvider provider = services.BuildServiceProvider();

        OrphanJobRecoveryHostedService recovery = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OrphanJobRecoveryHostedService>.Instance
        );

        await recovery.StartAsync(CancellationToken.None);
        if (recovery.ExecuteTask is not null)
            await recovery.ExecuteTask;

        _context
            .QueueJobs.Count()
            .Should()
            .Be(
                1,
                "a job reserved less than 30 seconds ago is still running and must not be reclaimed"
            );
        _context.FailedJobs.Count().Should().Be(0);
    }

    /// <summary>
    /// The scenario that motivated this whole file: a server restart mid-scan
    /// leaves a job reserved (e.g. a FileRescanJob) with nothing to ever clear
    /// it. Orphan recovery alone only proves the reservation gets released —
    /// this chains a REAL <see cref="QueueWorker"/> onto the requeued row so
    /// the assertion is "the interrupted job actually resumes and completes",
    /// not merely "its ReservedAt went back to null".
    /// </summary>
    [Fact]
    public async Task OrphanRecovery_RequeuedJob_IsThenPickedUpByARealWorkerAndCompletes()
    {
        SemaphoreSlim done = new(0, 1);
        string jobKey = Guid.NewGuid().ToString("N");
        CompletionSignalJob.RegisterCompletion(jobKey, () => done.Release());

        QueueJob interruptedByRestart = new()
        {
            Queue = "worker-success",
            Payload = SerializationHelper.Serialize(new CompletionSignalJob { JobKey = jobKey }),
            AvailableAt = DateTime.UtcNow.AddHours(-1),
            ReservedAt = DateTime.UtcNow.AddMinutes(-5),
            Attempts = 1,
        };
        _context.QueueJobs.Add(interruptedByRestart);
        await _context.SaveChangesAsync();

        ServiceCollection services = new();
        services.AddSingleton<IQueueContext>(_adapter);
        ServiceProvider provider = services.BuildServiceProvider();
        OrphanJobRecoveryHostedService recovery = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OrphanJobRecoveryHostedService>.Instance
        );
        await recovery.StartAsync(CancellationToken.None);
        if (recovery.ExecuteTask is not null)
            await recovery.ExecuteTask;

        _context
            .QueueJobs.Count()
            .Should()
            .Be(1, "the boot pass grants a first-time orphan one clean retry, not a dead-letter");
        _context.QueueJobs.Single().ReservedAt.Should().BeNull();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        QueueWorker worker = new(_jobQueue, "worker-success");
        Task workerTask = worker.StartAsync(cts.Token);

        bool completed = await done.WaitAsync(TimeSpan.FromSeconds(8));
        worker.Stop();
        cts.Cancel();
        await workerTask;

        completed
            .Should()
            .BeTrue(
                "the job orphan recovery released for retry must actually resume and run to completion"
            );
        _context
            .QueueJobs.Count()
            .Should()
            .Be(0, "the resumed job must be deleted from the queue once it completes");
        _context.FailedJobs.Count().Should().Be(0);
    }

    // ── Orphan recovery: ResetAllReservedJobs on Initialize ───────────────

    [Fact]
    public void ResetAllReservedJobs_ClearsReservedAt_OnAllJobsInQueue()
    {
        QueueJob job1 = new()
        {
            Queue = QueueNames.Library,
            Payload = "reserved-1",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow.AddMinutes(-5),
            Attempts = 1,
        };
        QueueJob job2 = new()
        {
            Queue = QueueNames.Image,
            Payload = "reserved-2",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow.AddMinutes(-3),
            Attempts = 0,
        };
        QueueJob job3 = new()
        {
            Queue = QueueNames.Library,
            Payload = "unreserved-3",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = null,
            Attempts = 0,
        };
        _context.QueueJobs.AddRange(job1, job2, job3);
        _context.SaveChanges();

        _jobQueue.ResetAllReservedJobs();

        List<QueueJob> all = _context.QueueJobs.ToList();
        all.Should()
            .AllSatisfy(j =>
                j.ReservedAt.Should()
                    .BeNull(
                        "ResetAllReservedJobs must clear ReservedAt on every job so they re-enter the pool"
                    )
            );
        all.Count.Should().Be(3, "ResetAllReservedJobs must not delete any jobs");
    }
}

// ── Stub job types (NoMercy.* namespace so the serialization binder permits them) ──

/// <summary>
/// Minimal job that records which queue it's targeting. Used to verify
/// that dispatch lands payloads on the correct named queue.
/// </summary>
public class NamedQueueStubJob : IShouldQueue
{
    public string TargetQueue { get; set; } = "default";
    public string QueueName => TargetQueue;
    public int Priority => 1;

    public Task Handle() => Task.CompletedTask;
}

/// <summary>
/// Signals a semaphore on completion so the test can observe the DB state
/// without wall-clock sleeps.
/// </summary>
public class CompletionSignalJob : IShouldQueue
{
    private static readonly ConcurrentDictionary<string, Action> Completions = new();

    public string JobKey { get; set; } = string.Empty;
    public string QueueName => "worker-success";
    public int Priority => 0;

    public static void RegisterCompletion(string key, Action onComplete)
    {
        Completions[key] = onComplete;
    }

    public Task Handle()
    {
        if (Completions.TryGetValue(JobKey, out Action? cb))
            cb();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Throws on the first N attempts (tracked via a static counter keyed by
/// <see cref="JobKey"/>), then succeeds. The counter survives deserialization
/// because it lives in a static dictionary — the job key is serialized.
/// </summary>
public class FailOnceThenSucceedJob : IShouldQueue
{
    private static readonly ConcurrentDictionary<string, int> AttemptCounts = new();
    private static readonly ConcurrentDictionary<string, int> FailCounts = new();
    private static readonly ConcurrentDictionary<string, Action> SuccessCallbacks = new();

    public string JobKey { get; set; } = string.Empty;
    public string QueueName => "retry-e2e";
    public int Priority => 0;

    public static void Configure(string key, int failCount, Action? onSuccess = null)
    {
        AttemptCounts[key] = 0;
        FailCounts[key] = failCount;
        if (onSuccess is not null)
            SuccessCallbacks[key] = onSuccess;
    }

    public Task Handle()
    {
        int attempt = AttemptCounts.AddOrUpdate(JobKey, 1, (_, v) => v + 1);
        int failUntil = FailCounts.GetValueOrDefault(JobKey, 1);

        if (attempt <= failUntil)
        {
            throw new InvalidOperationException(
                $"FailOnceThenSucceedJob: injected failure on attempt {attempt} of {failUntil}"
            );
        }

        if (SuccessCallbacks.TryGetValue(JobKey, out Action? successCb))
            successCb();

        return Task.CompletedTask;
    }
}
