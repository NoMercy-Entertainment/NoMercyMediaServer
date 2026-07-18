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
/// A transient SQLite contention error from ReleaseReservation / Requeue /
/// UpdateJobPayload used to propagate out of JobQueue — and because those calls
/// sit outside the QueueWorker loop's try, it ended the worker Task permanently
/// (never respawned), stalling the queue. These ops must now match their
/// Reserve/Fail/Delete siblings: never throw on DB contention.
/// </summary>
[Trait("Category", "Unit")]
public class JobQueueContentionRetryTests
{
    private static Exception DbContention() =>
        new("database is locked") { Source = "Microsoft.EntityFrameworkCore.Relational" };

    private static QueueJobModel Job() =>
        new()
        {
            Id = 1,
            Queue = "encoder-gpu",
            Payload = "{}",
            Attempts = 1,
        };

    [Fact]
    public void ReleaseReservation_OnDbContention_DoesNotThrow_AndSwallows()
    {
        Mock<IQueueContext> context = new();
        context.Setup(c => c.UpdateJob(It.IsAny<QueueJobModel>())).Throws(DbContention());
        JobQueue queue = new(context.Object);

        Action act = () => queue.ReleaseReservation(Job(), TimeSpan.FromSeconds(1));

        act.Should().NotThrow();
        context.Verify(c => c.SaveChanges(), Times.Never);
    }

    [Fact]
    public void Requeue_OnDbContention_DoesNotThrow()
    {
        Mock<IQueueContext> context = new();
        context.Setup(c => c.UpdateJob(It.IsAny<QueueJobModel>())).Throws(DbContention());
        JobQueue queue = new(context.Object);

        Action act = () => queue.Requeue(Job(), "encoder-cpu", "{}");

        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateJobPayload_OnDbContention_DoesNotThrow()
    {
        Mock<IQueueContext> context = new();
        context
            .Setup(c =>
                c.UpdateJobPayload(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>())
            )
            .Throws(DbContention());
        JobQueue queue = new(context.Object);

        Action act = () => queue.UpdateJobPayload(1, "{}", TimeSpan.FromSeconds(1));

        act.Should().NotThrow();
    }

    [Fact]
    public void ReleaseReservation_OnTransientNonRelationalError_RetriesThenSucceeds()
    {
        Mock<IQueueContext> context = new();
        int updateJobCalls = 0;
        context
            .Setup(c => c.UpdateJob(It.IsAny<QueueJobModel>()))
            .Callback(() =>
            {
                updateJobCalls++;
                if (updateJobCalls == 1)
                    throw new InvalidOperationException("transient");
            });
        JobQueue queue = new(context.Object);

        Action act = () => queue.ReleaseReservation(Job(), TimeSpan.FromSeconds(1));

        act.Should().NotThrow();
        context.Verify(c => c.SaveChanges(), Times.Once);
    }
}
