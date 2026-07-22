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
        Dispatch(job: job, onQueue: job.QueueName, priority: job.Priority);
    }

    public void Dispatch(IShouldQueue job, string onQueue, int priority)
    {
        QueueJobModel jobData = new()
        {
            Queue = onQueue,
            Payload = SerializationHelper.Serialize(obj: job),
            AvailableAt = DateTime.UtcNow,
            Priority = priority,
        };

        try
        {
            _queue.Enqueue(queueJob: jobData);
        }
        catch (Exception e)
        {
            _logger.LogError(message: "{Message}", args: e.Message);
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
            Payload = SerializationHelper.Serialize(obj: job),
            AvailableAt = DateTime.UtcNow,
            Priority = priority,
            ParentJobId = parentJobId,
            GroupTag = groupTag,
        };

        try
        {
            _queue.Enqueue(queueJob: jobData);
        }
        catch (Exception e)
        {
            _logger.LogError(message: "{Message}", args: e.Message);
        }
    }
}
