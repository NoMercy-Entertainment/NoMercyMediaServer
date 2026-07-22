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
using Moq;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// JobQueue's write operations retry a transient (non-EF-relational-source)
/// exception up to <c>MaxDbRetryAttempts</c> (5) times before giving up. Every
/// public method exposes its recursive <c>attempt</c> counter as an optional
/// parameter, so the exhausted-retry branch (attempt == MaxDbRetryAttempts)
/// can be dialed directly without sleeping through the real 2-2.5s backoff
/// four times per method. These tests assert the actual recovery contract:
/// once retries are exhausted the method gives up SILENTLY — no exception
/// escapes to the worker loop (which would otherwise crash the polling
/// thread), and no partial write is committed. If the exhausted branch
/// regressed into rethrowing, or into looping forever, every test here goes
/// red.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class JobQueueRetryExhaustionTests
{
    private static Exception NonRelational() => new InvalidOperationException(message: "disk full");

    private static QueueJobModel Job() =>
        new()
        {
            Id = 7,
            Queue = "extras",
            Payload = "{}",
            Attempts = 1,
        };

    [Fact]
    public void ReserveJob_RetriesExhausted_ReturnsNull_WithoutThrowing()
    {
        Mock<IQueueContext> context = new();
        context
            .Setup(expression: c =>
                c.GetNextJob(
                    It.IsAny<string>(),
                    It.IsAny<byte>(),
                    It.IsAny<long?>(),
                    It.IsAny<DateTime>()
                )
            )
            .Throws(exception: NonRelational());
        JobQueue queue = new(context: context.Object);

        QueueJobModel? result = queue.ReserveJob(name: "extras", currentJobId: null, attempt: 5);

        result.Should().BeNull();
        context.Verify(expression: c => c.UpdateJob(It.IsAny<QueueJobModel>()), times: Times.Never);
    }

    [Fact]
    public void FailJob_RetriesExhausted_GivesUpSilently_NoPartialWrite()
    {
        Mock<IQueueContext> context = new();
        context.Setup(expression: c => c.UpdateJob(It.IsAny<QueueJobModel>())).Throws(exception: NonRelational());
        JobQueue queue = new(context: context.Object);

        Action act = () => queue.FailJob(queueJob: Job(), exception: new Exception(message: "boom"), attempt: 5);

        act.Should().NotThrow();
        context.Verify(expression: c => c.SaveChanges(), times: Times.Never);
        context.Verify(
            expression: c => c.AddFailedJobAndRemoveJob(It.IsAny<FailedJobModel>(), It.IsAny<QueueJobModel>()),
            times: Times.Never
        );
    }

    [Fact]
    public void ReleaseReservation_RetriesExhausted_GivesUpSilently()
    {
        Mock<IQueueContext> context = new();
        context.Setup(expression: c => c.UpdateJob(It.IsAny<QueueJobModel>())).Throws(exception: NonRelational());
        JobQueue queue = new(context: context.Object);

        Action act = () => queue.ReleaseReservation(job: Job(), availableAfter: TimeSpan.FromSeconds(seconds: 1), attempt: 5);

        act.Should().NotThrow();
        context.Verify(expression: c => c.SaveChanges(), times: Times.Never);
    }

    [Fact]
    public void UpdateJobPayload_RetriesExhausted_GivesUpSilently()
    {
        Mock<IQueueContext> context = new();
        context
            .Setup(expression: c =>
                c.UpdateJobPayload(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>())
            )
            .Throws(exception: NonRelational());
        JobQueue queue = new(context: context.Object);

        Action act = () => queue.UpdateJobPayload(jobId: 1, newPayload: "{}", availableAfter: TimeSpan.FromSeconds(seconds: 1), attempt: 5);

        act.Should().NotThrow();
    }

    [Fact]
    public void Requeue_RetriesExhausted_GivesUpSilently_NoSaveChanges()
    {
        Mock<IQueueContext> context = new();
        context.Setup(expression: c => c.UpdateJob(It.IsAny<QueueJobModel>())).Throws(exception: NonRelational());
        JobQueue queue = new(context: context.Object);

        Action act = () => queue.Requeue(job: Job(), newQueue: "encoder-cpu", newPayload: "{}", attempt: 5);

        act.Should().NotThrow();
        context.Verify(expression: c => c.SaveChanges(), times: Times.Never);
    }

    [Fact]
    public void DeleteJob_RetriesExhausted_GivesUpSilently()
    {
        Mock<IQueueContext> context = new();
        context.Setup(expression: c => c.RemoveJob(It.IsAny<QueueJobModel>())).Throws(exception: NonRelational());
        JobQueue queue = new(context: context.Object);

        Action act = () => queue.DeleteJob(queueJob: Job(), attempt: 5);

        act.Should().NotThrow();
    }

    [Fact]
    public void RequeueFailedJob_RetriesExhausted_GivesUpSilently_DoesNotFindOrRemove()
    {
        Mock<IQueueContext> context = new();
        context.Setup(expression: c => c.FindFailedJob(It.IsAny<int>())).Throws(exception: NonRelational());
        JobQueue queue = new(context: context.Object);

        Action act = () => queue.RequeueFailedJob(failedJobId: 99, attempt: 5);

        act.Should().NotThrow();
        context.Verify(expression: c => c.RemoveFailedJob(It.IsAny<FailedJobModel>()), times: Times.Never);
        context.Verify(expression: c => c.AddJob(It.IsAny<QueueJobModel>()), times: Times.Never);
    }

    /// <summary>
    /// Contrast case: a transient error on an attempt UNDER the ceiling still
    /// recovers (retries once, in real wall-clock time, then succeeds) rather
    /// than giving up early. This is the one test in the file that pays the
    /// real ~2-2.5s backoff cost — deliberately, to prove the ceiling is a
    /// ceiling and not a hair-trigger.
    /// </summary>
    [Fact]
    public void DeleteJob_TransientErrorUnderCeiling_RetriesThenSucceeds()
    {
        Mock<IQueueContext> context = new();
        int calls = 0;
        context
            .Setup(expression: c => c.RemoveJob(It.IsAny<QueueJobModel>()))
            .Callback(action: () =>
            {
                calls++;
                if (calls == 1)
                    throw new InvalidOperationException(message: "transient");
            });
        JobQueue queue = new(context: context.Object);

        Action act = () => queue.DeleteJob(queueJob: Job());

        act.Should().NotThrow();
        calls.Should().Be(expected: 2);
    }
}
