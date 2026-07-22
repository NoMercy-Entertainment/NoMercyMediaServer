// -----------------------------------------------------------------------------
//  Copyright (c) NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy and is proprietary and confidential.
//  Unauthorized copying, distribution, or use is prohibited. See LICENSE.
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
[Collection(name: "QueueEngine")]
public class QueueEngineEndToEndTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public QueueEngineEndToEndTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        _jobQueue = new(context: _adapter, maxAttempts: 3);
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
        JobDispatcher dispatcher = new(queue: _jobQueue, logger: NullLogger<JobDispatcher>.Instance);
        NamedQueueStubJob job = new() { TargetQueue = QueueNames.Library };

        dispatcher.Dispatch(job: job, onQueue: QueueNames.Library, priority: 5);

        QueueJob? stored = _context.QueueJobs.SingleOrDefault();
        stored.Should().NotBeNull(because: "the job must be persisted");
        stored!.Queue.Should().Be(expected: QueueNames.Library, because: "queue name must match the dispatch target");
        stored.Priority.Should().Be(expected: 5);
        stored.Attempts.Should().Be(expected: 0, because: "a freshly dispatched job has zero attempts");
        stored.ReservedAt.Should().BeNull(because: "a freshly dispatched job is unreserved");
    }

    [Fact]
    public void Dispatch_ToTwoDistinctQueues_EachQueueReceivesExactlyItsOwnJob()
    {
        JobDispatcher dispatcher = new(queue: _jobQueue, logger: NullLogger<JobDispatcher>.Instance);
        NamedQueueStubJob libraryJob = new() { TargetQueue = QueueNames.Library };
        NamedQueueStubJob imageJob = new() { TargetQueue = QueueNames.Image };

        dispatcher.Dispatch(job: libraryJob, onQueue: QueueNames.Library, priority: 1);
        dispatcher.Dispatch(job: imageJob, onQueue: QueueNames.Image, priority: 1);

        _context.QueueJobs.Count().Should().Be(expected: 2);

        QueueJobModel? fromLibrary = _jobQueue.ReserveJob(name: QueueNames.Library, currentJobId: null);
        fromLibrary.Should().NotBeNull();
        fromLibrary!.Queue.Should().Be(expected: QueueNames.Library);

        QueueJobModel? fromImage = _jobQueue.ReserveJob(name: QueueNames.Image, currentJobId: null);
        fromImage.Should().NotBeNull();
        fromImage!.Queue.Should().Be(expected: QueueNames.Image);

        QueueJobModel? wrongQueue = _jobQueue.ReserveJob(name: QueueNames.Music, currentJobId: null);
        wrongQueue.Should().BeNull(because: "no jobs were dispatched to the music queue");
    }

    // ── Real worker: successful execution removes the job ─────────────────

    [Fact]
    public async Task Worker_SuccessfulJob_DeletesJobFromQueueAfterHandle()
    {
        SemaphoreSlim done = new(initialCount: 0, maxCount: 1);
        string jobKey = Guid.NewGuid().ToString(format: "N");
        CompletionSignalJob.RegisterCompletion(key: jobKey, onComplete: () => done.Release());

        CompletionSignalJob stub = new() { JobKey = jobKey };
        _jobQueue.Enqueue(
            queueJob: new()
            {
                Queue = "worker-success",
                Payload = SerializationHelper.Serialize(obj: stub),
                AvailableAt = DateTime.UtcNow,
            }
        );

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 10));
        QueueWorker worker = new(queue: _jobQueue, name: "worker-success");

        Task workerTask = worker.StartAsync(stopToken: cts.Token);

        bool signalled = await done.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 8));
        worker.Stop();
        cts.Cancel();
        await workerTask;

        signalled.Should().BeTrue(because: "the job must execute before the timeout");
        _context.QueueJobs.Count().Should().Be(expected: 0, because: "completed job must be deleted from the queue");
        _context
            .FailedJobs.Count()
            .Should()
            .Be(expected: 0, because: "a successful job produces no failed-job record");
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
        SemaphoreSlim done = new(initialCount: 0, maxCount: 1);
        string jobKey = Guid.NewGuid().ToString(format: "N");
        FailOnceThenSucceedJob.Configure(key: jobKey, failCount: 1, onSuccess: () => done.Release());

        FailOnceThenSucceedJob stub = new() { JobKey = jobKey };
        _jobQueue.Enqueue(
            queueJob: new()
            {
                Queue = "retry-e2e",
                Payload = SerializationHelper.Serialize(obj: stub),
                AvailableAt = DateTime.UtcNow,
            }
        );

        _context.QueueJobs.Count().Should().Be(expected: 1, because: "job must be in queue before worker starts");

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 15));
        QueueWorker worker = new(queue: _jobQueue, name: "retry-e2e");

        Task workerTask = worker.StartAsync(stopToken: cts.Token);

        bool signalled = await done.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 12));
        worker.Stop();
        cts.Cancel();
        await workerTask;

        signalled.Should().BeTrue(because: "job must succeed on retry before timeout");

        _context
            .QueueJobs.Count()
            .Should()
            .Be(expected: 0, because: "job that succeeded after retry must be deleted from queue");
        _context
            .FailedJobs.Count()
            .Should()
            .Be(expected: 0, because: "a job that succeeded within maxAttempts produces no failed-job record");
    }

    // ── Real worker: exhausted retries → terminal FailedJobs ─────────────

    [Fact]
    public async Task Worker_JobExhaustsRetries_TransitionsToTerminalFailedState()
    {
        string jobKey = Guid.NewGuid().ToString(format: "N");
        FailOnceThenSucceedJob.Configure(key: jobKey, failCount: 99);

        FailOnceThenSucceedJob stub = new() { JobKey = jobKey };
        _jobQueue.Enqueue(
            queueJob: new()
            {
                Queue = "exhaust-retries",
                Payload = SerializationHelper.Serialize(obj: stub),
                AvailableAt = DateTime.UtcNow,
            }
        );

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 20));
        QueueWorker worker = new(queue: _jobQueue, name: "exhaust-retries");
        Task workerTask = worker.StartAsync(stopToken: cts.Token);

        DateTime deadline = DateTime.UtcNow.AddSeconds(value: 15);
        while (DateTime.UtcNow < deadline)
        {
            if (_context.FailedJobs.Count() > 0)
                break;
            await Task.Delay(millisecondsDelay: 50, cancellationToken: CancellationToken.None);
        }

        worker.Stop();
        cts.Cancel();
        await workerTask;

        _context
            .QueueJobs.Count()
            .Should()
            .Be(expected: 0, because: "a job at max-attempts must be removed from QueueJobs");
        _context
            .FailedJobs.Count()
            .Should()
            .Be(expected: 1, because: "exhausted job must appear in FailedJobs exactly once");

        FailedJob terminal = _context.FailedJobs.Single();
        terminal.Queue.Should().Be(expected: "exhaust-retries", because: "queue name preserved in FailedJob record");
        terminal.Exception.Should().NotBeNullOrEmpty(because: "exception content must be serialized");
        terminal
            .FailedAt.Should()
            .BeBefore(expected: DateTime.UtcNow.AddSeconds(value: 1), because: "FailedAt must not be in the future");
        terminal
            .FailedAt.Should()
            .BeAfter(expected: DateTime.UtcNow.AddSeconds(value: -60), because: "FailedAt must be within this test run");
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
    public async Task OrphanRecovery_StuckJobWithRepeatAttempts_MovedToFailedJobsViaRealAdapter()
    {
        QueueJob orphan = new()
        {
            Queue = QueueNames.Library,
            Payload = SerializationHelper.Serialize(obj: new TestJob { Message = "orphan" }),
            AvailableAt = DateTime.UtcNow.AddHours(value: -2),
            ReservedAt = DateTime.UtcNow.AddMinutes(value: -10),
            Attempts = 2,
        };
        _context.QueueJobs.Add(entity: orphan);
        await _context.SaveChangesAsync();

        ServiceCollection services = new();
        services.AddSingleton<IQueueContext>(implementationInstance: _adapter);
        ServiceProvider provider = services.BuildServiceProvider();

        OrphanJobRecoveryHostedService recovery = new(
            scopeFactory: provider.GetRequiredService<IServiceScopeFactory>(),
            logger: NullLogger<OrphanJobRecoveryHostedService>.Instance
        );

        await recovery.StartAsync(cancellationToken: CancellationToken.None);
        if (recovery.ExecuteTask is not null)
            await recovery.ExecuteTask;

        _context
            .QueueJobs.Count()
            .Should()
            .Be(expected: 0, because: "orphaned job with prior attempts must be removed from QueueJobs");
        _context
            .FailedJobs.Count()
            .Should()
            .Be(expected: 1, because: "orphaned job with prior attempts must be moved to FailedJobs");

        FailedJob failed = _context.FailedJobs.Single();
        failed.Queue.Should().Be(expected: QueueNames.Library);
        failed
            .Exception.Should()
            .Be(
                expected: "job.interrupted_no_checkpoint",
                because: "orphan failure reason must be the sentinel string"
            );
        failed.Uuid.Should().NotBe(unexpected: Guid.Empty, because: "orphan FailedJob must get a fresh UUID");
    }

    [Fact]
    public async Task OrphanRecovery_StuckJobOnFirstAttempt_LeftInQueueForRetry()
    {
        QueueJob firstTimeOrphan = new()
        {
            Queue = QueueNames.Library,
            Payload = SerializationHelper.Serialize(obj: new TestJob { Message = "first-time" }),
            AvailableAt = DateTime.UtcNow.AddHours(value: -1),
            ReservedAt = DateTime.UtcNow.AddMinutes(value: -5),
            Attempts = 1,
        };
        _context.QueueJobs.Add(entity: firstTimeOrphan);
        await _context.SaveChangesAsync();

        ServiceCollection services = new();
        services.AddSingleton<IQueueContext>(implementationInstance: _adapter);
        ServiceProvider provider = services.BuildServiceProvider();

        OrphanJobRecoveryHostedService recovery = new(
            scopeFactory: provider.GetRequiredService<IServiceScopeFactory>(),
            logger: NullLogger<OrphanJobRecoveryHostedService>.Instance
        );

        await recovery.StartAsync(cancellationToken: CancellationToken.None);
        if (recovery.ExecuteTask is not null)
            await recovery.ExecuteTask;

        _context
            .QueueJobs.Count()
            .Should()
            .Be(expected: 1, because: "a first-attempt orphan must remain in queue for one retry");
        _context.FailedJobs.Count().Should().Be(expected: 0, because: "first-attempt orphan must not be failed");

        QueueJob survivor = _context.QueueJobs.Single();
        survivor.ReservedAt.Should().BeNull(because: "the clean-retry reset must release the reservation");
        survivor.Attempts.Should().Be(expected: 0, because: "the clean-retry reset must refund the attempt budget");
    }

    [Fact]
    public async Task OrphanRecovery_RecentlyReservedJob_IsNotReclaimed()
    {
        QueueJob activeJob = new()
        {
            Queue = QueueNames.Encoder,
            Payload = SerializationHelper.Serialize(obj: new TestJob { Message = "active" }),
            AvailableAt = DateTime.UtcNow.AddMinutes(value: -1),
            ReservedAt = DateTime.UtcNow.AddSeconds(value: -10),
            Attempts = 2,
        };
        _context.QueueJobs.Add(entity: activeJob);
        await _context.SaveChangesAsync();

        ServiceCollection services = new();
        services.AddSingleton<IQueueContext>(implementationInstance: _adapter);
        ServiceProvider provider = services.BuildServiceProvider();

        OrphanJobRecoveryHostedService recovery = new(
            scopeFactory: provider.GetRequiredService<IServiceScopeFactory>(),
            logger: NullLogger<OrphanJobRecoveryHostedService>.Instance
        );

        await recovery.StartAsync(cancellationToken: CancellationToken.None);
        if (recovery.ExecuteTask is not null)
            await recovery.ExecuteTask;

        _context
            .QueueJobs.Count()
            .Should()
            .Be(
                expected: 1,
                because: "a job reserved less than 30 seconds ago is still running and must not be reclaimed"
            );
        _context.FailedJobs.Count().Should().Be(expected: 0);
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
        SemaphoreSlim done = new(initialCount: 0, maxCount: 1);
        string jobKey = Guid.NewGuid().ToString(format: "N");
        CompletionSignalJob.RegisterCompletion(key: jobKey, onComplete: () => done.Release());

        QueueJob interruptedByRestart = new()
        {
            Queue = "worker-success",
            Payload = SerializationHelper.Serialize(obj: new CompletionSignalJob { JobKey = jobKey }),
            AvailableAt = DateTime.UtcNow.AddHours(value: -1),
            ReservedAt = DateTime.UtcNow.AddMinutes(value: -5),
            Attempts = 1,
        };
        _context.QueueJobs.Add(entity: interruptedByRestart);
        await _context.SaveChangesAsync();

        ServiceCollection services = new();
        services.AddSingleton<IQueueContext>(implementationInstance: _adapter);
        ServiceProvider provider = services.BuildServiceProvider();
        OrphanJobRecoveryHostedService recovery = new(
            scopeFactory: provider.GetRequiredService<IServiceScopeFactory>(),
            logger: NullLogger<OrphanJobRecoveryHostedService>.Instance
        );
        await recovery.StartAsync(cancellationToken: CancellationToken.None);
        if (recovery.ExecuteTask is not null)
            await recovery.ExecuteTask;

        _context
            .QueueJobs.Count()
            .Should()
            .Be(expected: 1, because: "the boot pass grants a first-time orphan one clean retry, not a dead-letter");
        _context.QueueJobs.Single().ReservedAt.Should().BeNull();

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 10));
        QueueWorker worker = new(queue: _jobQueue, name: "worker-success");
        Task workerTask = worker.StartAsync(stopToken: cts.Token);

        bool completed = await done.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 8));
        worker.Stop();
        cts.Cancel();
        await workerTask;

        completed
            .Should()
            .BeTrue(
                because: "the job orphan recovery released for retry must actually resume and run to completion"
            );
        _context
            .QueueJobs.Count()
            .Should()
            .Be(expected: 0, because: "the resumed job must be deleted from the queue once it completes");
        _context.FailedJobs.Count().Should().Be(expected: 0);
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
            ReservedAt = DateTime.UtcNow.AddMinutes(value: -5),
            Attempts = 1,
        };
        QueueJob job2 = new()
        {
            Queue = QueueNames.Image,
            Payload = "reserved-2",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow.AddMinutes(value: -3),
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
        _context.QueueJobs.AddRange(entities: [job1, job2, job3]);
        _context.SaveChanges();

        _jobQueue.ResetAllReservedJobs();

        List<QueueJob> all = _context.QueueJobs.ToList();
        all.Should()
            .AllSatisfy(expected: j =>
                j.ReservedAt.Should()
                    .BeNull(
                        because: "ResetAllReservedJobs must clear ReservedAt on every job so they re-enter the pool"
                    )
            );
        all.Count.Should().Be(expected: 3, because: "ResetAllReservedJobs must not delete any jobs");
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
        Completions[key: key] = onComplete;
    }

    public Task Handle()
    {
        if (Completions.TryGetValue(key: JobKey, value: out Action? cb))
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
        AttemptCounts[key: key] = 0;
        FailCounts[key: key] = failCount;
        if (onSuccess is not null)
            SuccessCallbacks[key: key] = onSuccess;
    }

    public Task Handle()
    {
        int attempt = AttemptCounts.AddOrUpdate(key: JobKey, addValue: 1, updateValueFactory: (_, v) => v + 1);
        int failUntil = FailCounts.GetValueOrDefault(key: JobKey, defaultValue: 1);

        if (attempt <= failUntil)
        {
            throw new InvalidOperationException(
                message: $"FailOnceThenSucceedJob: injected failure on attempt {attempt} of {failUntil}"
            );
        }

        if (SuccessCallbacks.TryGetValue(key: JobKey, value: out Action? successCb))
            successCb();

        return Task.CompletedTask;
    }
}
