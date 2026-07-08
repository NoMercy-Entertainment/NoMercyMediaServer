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

using NoMercy.Database;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Pins deterministic dequeue order: highest priority first, then FIFO by
/// insertion within a priority. Every encoder job shares Priority 4, so without
/// a stable tiebreak the worker dequeued equal-priority jobs in an undefined
/// order that did not match the dashboard task list — the queue appeared to run
/// "last added first". The tiebreak is (CreatedAt, then Id); Id is the decisive
/// key when bulk-queued episodes share a CreatedAt.
/// </summary>
public class ReserveJobOrderingTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public ReserveJobOrderingTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        _jobQueue = new(_adapter);
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ReserveJob_EqualPriorityAndCreatedAt_DequeuesFifoByInsertion()
    {
        DateTime stamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (int i in Enumerable.Range(0, 4))
            _jobQueue.Enqueue(
                new QueueJobModel
                {
                    Queue = "encoder",
                    Payload = $"episode-{i}",
                    AvailableAt = stamp,
                    CreatedAt = stamp, // identical -> only the Id tiebreak can order these
                    Priority = 4,
                }
            );

        List<string?> dequeued =
        [
            _jobQueue.ReserveJob("encoder", null)?.Payload,
            _jobQueue.ReserveJob("encoder", null)?.Payload,
            _jobQueue.ReserveJob("encoder", null)?.Payload,
            _jobQueue.ReserveJob("encoder", null)?.Payload,
        ];

        Assert.Equal(["episode-0", "episode-1", "episode-2", "episode-3"], dequeued);
    }

    [Fact]
    public void ReserveJob_HigherPriorityFirst_ThenFifo()
    {
        DateTime stamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _jobQueue.Enqueue(
            new QueueJobModel
            {
                Queue = "encoder",
                Payload = "low-first",
                AvailableAt = stamp,
                CreatedAt = stamp,
                Priority = 4,
            }
        );
        _jobQueue.Enqueue(
            new QueueJobModel
            {
                Queue = "encoder",
                Payload = "high-later",
                AvailableAt = stamp,
                CreatedAt = stamp,
                Priority = 9,
            }
        );
        _jobQueue.Enqueue(
            new QueueJobModel
            {
                Queue = "encoder",
                Payload = "low-second",
                AvailableAt = stamp,
                CreatedAt = stamp,
                Priority = 4,
            }
        );

        List<string?> dequeued =
        [
            _jobQueue.ReserveJob("encoder", null)?.Payload,
            _jobQueue.ReserveJob("encoder", null)?.Payload,
            _jobQueue.ReserveJob("encoder", null)?.Payload,
        ];

        Assert.Equal(["high-later", "low-first", "low-second"], dequeued);
    }
}
