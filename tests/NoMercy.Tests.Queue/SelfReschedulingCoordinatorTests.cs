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
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Workers;
using Xunit;
using IQueueContext = NoMercyQueue.Core.Interfaces.IQueueContext;

namespace NoMercy.Tests.Queue;

/// <summary>
/// A long-running coordinator wakes many times before its work is done, and it
/// must do so on ONE row. Its queue-job ID is the encode's identity everywhere
/// downstream — printed in the dashboard, used as a list key, and the anchor the
/// queue is ordered by — so a coordinator that enqueues a successor and lets the
/// worker delete the original renumbers itself roughly twice a minute.
///
/// These drive the REAL <see cref="QueueWorker.StartAsync"/> loop and assert on
/// the row that survives it, not on the fact that the machinery ran.
/// </summary>
[Collection("QueueEngine")]
public class SelfReschedulingCoordinatorTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public SelfReschedulingCoordinatorTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        _jobQueue = new(_adapter, maxAttempts: 3);
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
    }

    [Fact]
    public async Task Worker_JobRescheduledItselfInPlace_KeepsTheSameRowAndId()
    {
        SemaphoreSlim ran = new(0, 1);
        string jobKey = Guid.NewGuid().ToString("N");
        SelfReschedulingStubJob.Configure(jobKey, _jobQueue, () => ran.Release());

        SelfReschedulingStubJob stub = new() { JobKey = jobKey };
        _jobQueue.Enqueue(
            new()
            {
                Queue = "self-reschedule",
                Payload = SerializationHelper.Serialize(stub),
                AvailableAt = DateTime.UtcNow,
            }
        );

        int originalId = _context.QueueJobs.Single().Id;

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        QueueWorker worker = new(_jobQueue, "self-reschedule");
        Task workerTask = worker.StartAsync(cts.Token);

        bool signalled = await ran.WaitAsync(TimeSpan.FromSeconds(8));
        worker.Stop();
        await cts.CancelAsync();
        await workerTask;

        signalled.Should().BeTrue("the coordinator must run before the timeout");

        QueueJob[] remaining = [.. _context.QueueJobs];
        remaining.Length.Should().Be(1, "the coordinator rewrote its row, it did not add one");
        remaining[0]
            .Id.Should()
            .Be(
                originalId,
                "the ID is the encode's identity downstream — it must survive a wake-up"
            );
        remaining[0]
            .Payload.Should()
            .Contain(
                SelfReschedulingStubJob.NextPhaseMarker,
                "the rewritten row must carry the next phase's payload"
            );
    }

    [Fact]
    public async Task Worker_OrdinaryJob_StillDeletesItsRow()
    {
        SemaphoreSlim ran = new(0, 1);
        string jobKey = Guid.NewGuid().ToString("N");
        SelfReschedulingStubJob.Configure(
            jobKey,
            _jobQueue,
            () => ran.Release(),
            reschedule: false
        );

        SelfReschedulingStubJob stub = new() { JobKey = jobKey };
        _jobQueue.Enqueue(
            new()
            {
                Queue = "self-reschedule",
                Payload = SerializationHelper.Serialize(stub),
                AvailableAt = DateTime.UtcNow,
            }
        );

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        QueueWorker worker = new(_jobQueue, "self-reschedule");
        Task workerTask = worker.StartAsync(cts.Token);

        bool signalled = await ran.WaitAsync(TimeSpan.FromSeconds(8));
        worker.Stop();
        await cts.CancelAsync();
        await workerTask;

        signalled.Should().BeTrue("the job must run before the timeout");
        _context
            .QueueJobs.Count()
            .Should()
            .Be(0, "a job that did not reschedule itself is finished work and must be removed");
    }

    /// <summary>
    /// Reservation increments Attempts, and a row at maxAttempts stops being
    /// reservable. A coordinator polling on one row crosses that line after three
    /// wake-ups, so an in-place reschedule has to clear the counter — otherwise the
    /// encode stops waking with nothing logged and no failed-job record.
    /// </summary>
    [Fact]
    public void UpdateJobPayload_ClearsAttempts_SoAPollingCoordinatorStaysReservable()
    {
        _jobQueue.Enqueue(
            new()
            {
                Queue = "poll-forever",
                Payload = "{\"phase\":0}",
                AvailableAt = DateTime.UtcNow,
            }
        );

        int jobId = _context.QueueJobs.Single().Id;

        // Four wake-ups on a maxAttempts: 3 queue — one more than the old
        // delete-and-reinsert path could ever have hit on a single row.
        for (int phase = 1; phase <= 4; phase++)
        {
            _jobQueue
                .ReserveJob("poll-forever", null)
                .Should()
                .NotBeNull($"wake-up {phase} must still be able to reserve the coordinator");

            _jobQueue.UpdateJobPayload(jobId, $"{{\"phase\":{phase}}}", TimeSpan.Zero);
        }

        QueueJob row = _context.QueueJobs.Single();
        row.Id.Should().Be(jobId, "every wake-up ran on the same row");
        row.Attempts.Should().Be(0, "a phase advance is new work, not a retry");
    }
}

/// <summary>
/// Stands in for <c>VideoEncodeJob</c>'s coordinator loop: rewrites its own row
/// with the next phase's payload and reports that it did, exactly as
/// <c>ReEnqueueSelf</c> does.
/// </summary>
public class SelfReschedulingStubJob : IShouldQueue, IJobIdReceiver, ISelfRescheduling
{
    public const string NextPhaseMarker = "phase-two";

    private static readonly ConcurrentDictionary<string, JobQueue> Queues = new();
    private static readonly ConcurrentDictionary<string, Action> Callbacks = new();
    private static readonly ConcurrentDictionary<string, bool> Reschedules = new();

    private int _selfJobId;

    public string JobKey { get; set; } = string.Empty;
    public string QueueName => "self-reschedule";
    public int Priority => 0;
    public bool RescheduledInPlace { get; private set; }

    public static void Configure(string key, JobQueue queue, Action onRun, bool reschedule = true)
    {
        Queues[key] = queue;
        Callbacks[key] = onRun;
        Reschedules[key] = reschedule;
    }

    public void ReceiveJobId(int jobId) => _selfJobId = jobId;

    public Task Handle()
    {
        if (
            Reschedules.TryGetValue(JobKey, out bool reschedule)
            && reschedule
            && Queues.TryGetValue(JobKey, out JobQueue? queue)
        )
        {
            queue.UpdateJobPayload(
                _selfJobId,
                $"{{\"JobKey\":\"{JobKey}\",\"phase\":\"{NextPhaseMarker}\"}}",
                TimeSpan.FromMinutes(30)
            );
            RescheduledInPlace = true;
        }

        if (Callbacks.TryGetValue(JobKey, out Action? callback))
            callback();

        return Task.CompletedTask;
    }
}
