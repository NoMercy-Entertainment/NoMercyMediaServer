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
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Workers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Pins <c>fix(queue): don't dead-letter a job whose scope was disposed by
/// shutdown</c>. <see cref="QueueWorker.StartAsync"/> has two catch clauses
/// (<c>OperationCanceledException</c> and <c>ObjectDisposedException</c>)
/// guarded by <c>when (stopToken.IsCancellationRequested)</c> — the SAME
/// exception type is either a graceful shutdown interruption (release the
/// reservation, no attempt burned, never dead-lettered) or a genuine job
/// fault (count toward the attempt budget, eventually dead-letter),
/// depending entirely on whether the worker's own stop token was already
/// cancelled when the exception surfaced. If that `when` guard regressed
/// (e.g. into an unconditional catch), a job merely interrupted by a server
/// restart would get dead-lettered instead of resumed — exactly the
/// regression this fix closed.
///
/// <see cref="GatedThrowingJob"/> blocks on a test-controlled gate inside
/// <c>Handle()</c> so the test can deterministically reserve the job, THEN
/// flip the worker's shutdown state, THEN release the gate — no race on
/// wall-clock timing.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class QueueWorkerShutdownRecoveryTests
{
    private static async Task<QueueJobModel> WaitUntilReservedAsync(
        TestQueueContextAdapter context,
        string payload
    )
    {
        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 5));
        while (!cts.IsCancellationRequested)
        {
            QueueJobModel? job = context.Jobs.FirstOrDefault(predicate: j =>
                j.Payload == payload && j.ReservedAt != null
            );
            if (job is not null)
                return job;
            await Task.Delay(millisecondsDelay: 10);
        }
        throw new TimeoutException(message: "Job was never reserved within the wait window.");
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

    private static (
        QueueWorker Worker,
        TestQueueContextAdapter Context,
        JobQueue Queue,
        string Payload
    ) Build(string queueName, string exceptionMode, out string gateKey)
    {
        TestQueueContextAdapter context = new();
        JobQueue jobQueue = new(context: context);
        gateKey = Guid.NewGuid().ToString();
        GatedThrowingJob.Gates[key: gateKey] = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
        string payload = SerializationHelper.Serialize(
            obj: new GatedThrowingJob { GateKey = gateKey, ExceptionMode = exceptionMode }
        );
        context.AddJob(
            job: new QueueJobModel
            {
                Queue = queueName,
                Payload = payload,
                AvailableAt = DateTime.UtcNow,
            }
        );
        QueueWorker worker = new(queue: jobQueue, name: queueName);
        return (worker, context, jobQueue, payload);
    }

    [Fact]
    public async Task OperationCanceled_WhileStopTokenCancelled_ReleasesReservation_NeverFails()
    {
        (QueueWorker worker, TestQueueContextAdapter context, _, string payload) = Build(
            queueName: "gated-cancel-shutdown",
            exceptionMode: "cancelled",
            gateKey: out string gateKey
        );
        worker.Start();

        await WaitUntilReservedAsync(context: context, payload: payload);
        worker.Stop();
        GatedThrowingJob.Gates[key: gateKey].SetResult();

        await WaitUntilAsync(
            predicate: () => context.Jobs.Any(predicate: j => j.Payload == payload && j.ReservedAt == null),
            failureMessage: "Job was never released for retry after a shutdown-time OperationCanceledException."
        );

        QueueJobModel survivor = context.Jobs.Single(predicate: j => j.Payload == payload);
        survivor
            .Attempts.Should()
            .Be(expected: 0, because: "the attempt budget must not be burned by a shutdown interruption");
        context.FailedJobs.Should().BeEmpty();
    }

    [Fact]
    public async Task ObjectDisposed_WhileStopTokenCancelled_ReleasesReservation_NeverFails()
    {
        (QueueWorker worker, TestQueueContextAdapter context, _, string payload) = Build(
            queueName: "gated-disposed-shutdown",
            exceptionMode: "disposed",
            gateKey: out string gateKey
        );
        worker.Start();

        await WaitUntilReservedAsync(context: context, payload: payload);
        worker.Stop();
        GatedThrowingJob.Gates[key: gateKey].SetResult();

        await WaitUntilAsync(
            predicate: () => context.Jobs.Any(predicate: j => j.Payload == payload && j.ReservedAt == null),
            failureMessage: "Job was never released for retry after a shutdown-time ObjectDisposedException."
        );

        QueueJobModel survivor = context.Jobs.Single(predicate: j => j.Payload == payload);
        survivor
            .Attempts.Should()
            .Be(expected: 0, because: "the attempt budget must not be burned by a shutdown interruption");
        context.FailedJobs.Should().BeEmpty();
    }

    [Fact]
    public async Task OperationCanceled_WithoutShutdown_IsTreatedAsAGenuineFault_EventuallyDeadLetters()
    {
        (QueueWorker worker, TestQueueContextAdapter context, _, string payload) = Build(
            queueName: "gated-cancel-fault",
            exceptionMode: "cancelled",
            gateKey: out string gateKey
        );
        worker.Start();

        await WaitUntilReservedAsync(context: context, payload: payload);
        // No Stop() here — the worker's stop token stays live, so the same
        // exception type must NOT match the shutdown-recovery `when` guard.
        GatedThrowingJob.Gates[key: gateKey].SetResult();

        await WaitUntilAsync(
            predicate: () => context.FailedJobs.Any(predicate: f => f.Payload == payload),
            failureMessage: "An OperationCanceledException thrown with no shutdown in progress must still "
                            + "count toward the attempt budget and eventually dead-letter."
        );

        context.Jobs.Should().NotContain(predicate: j => j.Payload == payload);
        worker.Stop();
    }

    [Fact]
    public async Task ObjectDisposed_WithoutShutdown_IsTreatedAsAGenuineFault_EventuallyDeadLetters()
    {
        (QueueWorker worker, TestQueueContextAdapter context, _, string payload) = Build(
            queueName: "gated-disposed-fault",
            exceptionMode: "disposed",
            gateKey: out string gateKey
        );
        worker.Start();

        await WaitUntilReservedAsync(context: context, payload: payload);
        GatedThrowingJob.Gates[key: gateKey].SetResult();

        await WaitUntilAsync(
            predicate: () => context.FailedJobs.Any(predicate: f => f.Payload == payload),
            failureMessage: "An ObjectDisposedException thrown with no shutdown in progress must still "
                            + "count toward the attempt budget and eventually dead-letter."
        );

        context.Jobs.Should().NotContain(predicate: j => j.Payload == payload);
        worker.Stop();
    }
}
