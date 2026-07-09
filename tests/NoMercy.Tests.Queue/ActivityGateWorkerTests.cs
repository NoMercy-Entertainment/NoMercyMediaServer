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

using FluentAssertions;
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Workers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Verifies the playback-activity gate wired into <see cref="QueueWorker"/> —
/// a deferring gate must never let the worker reserve the next job for that
/// queue, and clearing the gate must let it resume without reconstructing
/// the worker.
/// </summary>
public class ActivityGateWorkerTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public ActivityGateWorkerTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        _jobQueue = new(_adapter);
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
    }

    private sealed class FakeActivityGate : IWorkerActivityGate
    {
        public bool Defer { get; set; }

        public TimeSpan DeferInterval { get; init; } = TimeSpan.FromMilliseconds(50);

        public bool ShouldDefer(string queueName) => Defer;
    }

    [Fact]
    public async Task ActivityGate_WhenDeferring_JobIsNeverReservedOrExecuted()
    {
        FakeActivityGate gate = new() { Defer = true };

        TestJob plainJob = new() { Message = "library job", HasExecuted = false };
        QueueJob queueJob = new()
        {
            Queue = "library",
            Payload = SerializationHelper.Serialize(plainJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
        };
        _context.QueueJobs.Add(queueJob);
        await _context.SaveChangesAsync();

        QueueWorker worker = new(_jobQueue, "library", activityGate: gate);

        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(300));
        try
        {
            await worker.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected once the token fires.
        }

        // Never reserved — the job is untouched in the queue.
        int jobCount = _context.QueueJobs.Count();
        jobCount.Should().Be(1);

        QueueJob? persisted = _context.QueueJobs.Find(queueJob.Id);
        persisted.Should().NotBeNull();
        persisted!.ReservedAt.Should().BeNull();
    }

    [Fact]
    public async Task ActivityGate_OnceCleared_WorkerResumesAndExecutesJob()
    {
        FakeActivityGate gate = new() { Defer = true };

        TestJob plainJob = new() { Message = "library job", HasExecuted = false };
        QueueJob queueJob = new()
        {
            Queue = "library",
            Payload = SerializationHelper.Serialize(plainJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
        };
        _context.QueueJobs.Add(queueJob);
        await _context.SaveChangesAsync();

        QueueWorker worker = new(_jobQueue, "library", activityGate: gate);

        // Signal completion off the worker's own event rather than a fixed
        // sleep: under coverage instrumentation the defer poll + reserve +
        // execute is far slower, and a wall-clock window flakes. The cap is
        // generous; the assertion is that the event fired, not that it fired fast.
        TaskCompletionSource jobDone = new(TaskCreationOptions.RunContinuationsAsynchronously);
        worker.WorkCompleted += (_, _) => jobDone.TrySetResult();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));

        // Run the worker on its own task — StartAsync's loop blocks between
        // reservations, so it must not run on the test thread.
        Task workerTask = Task.Run(async () =>
        {
            try
            {
                await worker.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected once the token fires.
            }
        });

        // Let the worker defer at least once, then lift the gate.
        await Task.Delay(100);
        gate.Defer = false;

        Task finished = await Task.WhenAny(jobDone.Task, Task.Delay(TimeSpan.FromSeconds(20)));
        finished
            .Should()
            .Be(jobDone.Task, "the worker must reserve and execute the job once the gate clears");

        await cts.CancelAsync();
        await workerTask;

        // Job was reserved, executed, and deleted once the gate cleared.
        int jobCount = _context.QueueJobs.Count();
        jobCount.Should().Be(0);
    }
}
