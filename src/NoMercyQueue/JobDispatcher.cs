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

using Microsoft.Extensions.Logging;
using NoMercyQueue.Core;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;

namespace NoMercyQueue;

public class JobDispatcher : IJobDispatcher
{
    private readonly JobQueue _queue;
    private readonly ILogger<JobDispatcher> _logger;

    public JobDispatcher(JobQueue queue, ILogger<JobDispatcher> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public void Dispatch(IShouldQueue job)
    {
        Dispatch(job, job.QueueName, job.Priority);
    }

    /// <summary>
    /// Queue a job and hand back a handle that survives it.
    ///
    /// <para>
    /// The payload hash, not the row id. The row id does not last: a job that
    /// succeeds is deleted from the queue, and a job that fails is deleted and
    /// rewritten into the failed table under a new identity, so a caller holding
    /// a row id can never be told which of the two happened. The payload is what
    /// both rows have in common.
    /// </para>
    ///
    /// <para>
    /// Null when an identical payload is already queued, or when the queue
    /// refused it.
    /// </para>
    /// </summary>
    public string? DispatchTracked(IShouldQueue job, string onQueue, int priority)
    {
        string payload = SerializationHelper.Serialize(job);

        QueueJobModel jobData = new()
        {
            Queue = onQueue,
            Payload = payload,
            AvailableAt = DateTime.UtcNow,
            Priority = priority,
            SharedInputKey = (job as IJobWithSharedInput)?.SharedInputKey,
        };

        try
        {
            return _queue.Enqueue(jobData) is null ? null : QueuePayloadHash.For(payload);
        }
        catch (Exception e)
        {
            _logger.LogError("{Message}", e.Message);
            return null;
        }
    }

    public void Dispatch(IShouldQueue job, string onQueue, int priority)
    {
        QueueJobModel jobData = new()
        {
            Queue = onQueue,
            Payload = SerializationHelper.Serialize(job),
            AvailableAt = DateTime.UtcNow,
            Priority = priority,
            SharedInputKey = (job as IJobWithSharedInput)?.SharedInputKey,
        };

        try
        {
            _queue.Enqueue(jobData);
        }
        catch (Exception e)
        {
            _logger.LogError("{Message}", e.Message);
        }
    }

    public void DispatchChild(
        IShouldQueue job,
        string onQueue,
        int priority,
        int parentJobId,
        string groupTag
    )
    {
        QueueJobModel jobData = new()
        {
            Queue = onQueue,
            Payload = SerializationHelper.Serialize(job),
            AvailableAt = DateTime.UtcNow,
            Priority = priority,
            SharedInputKey = (job as IJobWithSharedInput)?.SharedInputKey,
            ParentJobId = parentJobId,
            GroupTag = groupTag,
        };

        try
        {
            _queue.Enqueue(jobData);
        }
        catch (Exception e)
        {
            _logger.LogError("{Message}", e.Message);
        }
    }
}
