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
using Newtonsoft.Json;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;

namespace NoMercyQueue;

public class JobQueue(IQueueContext context, byte maxAttempts = 3, ILogger<JobQueue>? logger = null)
{
    private const int MaxDbRetryAttempts = 5;
    private const int BaseRetryDelayMs = 2000;
    private const int MaxJitterMs = 500;

    private readonly object _writeLock = new();

    /// <summary>
    /// Signalled once per <see cref="Enqueue"/> call so idle workers wake
    /// immediately instead of waiting out a fixed poll interval.
    /// </summary>
    internal readonly SemaphoreSlim WorkAvailable = new(initialCount: 0);

    public void ResetAllReservedJobs()
    {
        lock (_writeLock)
        {
            context.ResetAllReservedJobs();
        }
    }

    public void Enqueue(QueueJobModel queueJob)
    {
        lock (_writeLock)
        {
            bool exists = context.JobExists(payload: queueJob.Payload);
            if (exists)
                return;

            context.AddJob(job: queueJob);
        }

        WorkAvailable.Release();
    }

    public QueueJobModel? Dequeue()
    {
        lock (_writeLock)
        {
            QueueJobModel? job = context.GetNextJob(queueName: "", maxAttempts: 255, currentJobId: null, now: DateTime.UtcNow);
            if (job == null)
                return job;

            context.RemoveJob(job: job);

            return job;
        }
    }

    public QueueJobModel? ReserveJob(string name, long? currentJobId, int attempt = 0)
    {
        try
        {
            lock (_writeLock)
            {
                QueueJobModel? job = context.GetNextJob(
                    queueName: name,
                    maxAttempts: maxAttempts,
                    currentJobId: currentJobId,
                    now: DateTime.UtcNow
                );

                if (job == null)
                    return job;

                // Child jobs whose coordinator has already failed should not
                // run — they would produce orphaned output. Move them directly
                // to FailedJobs with a synthetic exception so the dashboard
                // shows "failed-by-parent" rather than silently dropping them.
                if (job.ParentJobId.HasValue && context.IsParentFailed(parentJobId: job.ParentJobId.Value))
                {
                    FailedJobModel skipped = new()
                    {
                        Uuid = Guid.NewGuid(),
                        Connection = "default",
                        Queue = job.Queue,
                        Payload = job.Payload,
                        ParentJobId = job.Id,
                        Exception =
                            $"{{\"Message\":\"Skipped: parent job {job.ParentJobId} failed\"}}",
                        FailedAt = DateTime.UtcNow,
                    };
                    context.AddFailedJobAndRemoveJob(failedJob: skipped, job: job);
                    return null;
                }

                job.ReservedAt = DateTime.UtcNow;
                job.Attempts++;

                context.UpdateJob(job: job);

                return job;
            }
        }
        catch (Exception e)
        {
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational")
            {
                logger?.LogDebug(exception: e, message: "Queue DB contention reserving a job on {Queue}", args: name);
                return null;
            }
            if (attempt < MaxDbRetryAttempts)
            {
                Thread.Sleep(millisecondsTimeout: BaseRetryDelayMs + Random.Shared.Next(maxValue: MaxJitterMs));
                return ReserveJob(name: name, currentJobId: currentJobId, attempt: attempt + 1);
            }

            logger?.LogError(exception: e, message: "Failed to reserve a job on {Queue}", args: name);
        }

        return null;
    }

    public void FailJob(QueueJobModel queueJob, Exception exception, int attempt = 0)
    {
        try
        {
            lock (_writeLock)
            {
                queueJob.ReservedAt = null;

                if (queueJob.Attempts >= maxAttempts)
                {
                    FailedJobModel failedJob = new()
                    {
                        Uuid = Guid.NewGuid(),
                        Connection = "default",
                        Queue = queueJob.Queue,
                        Payload = queueJob.Payload,
                        ParentJobId = queueJob.Id,
                        Exception = JsonConvert.SerializeObject(
                            value: exception.InnerException ?? exception
                        ),
                        FailedAt = DateTime.UtcNow,
                    };

                    context.AddFailedJobAndRemoveJob(failedJob: failedJob, job: queueJob);
                }
                else
                {
                    context.UpdateJob(job: queueJob);
                }

                context.SaveChanges();
            }
        }
        catch (Exception e)
        {
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational")
            {
                logger?.LogDebug(exception: e, message: "Queue DB contention failing job {JobId}", args: queueJob.Id);
                return;
            }
            if (attempt < MaxDbRetryAttempts)
            {
                Thread.Sleep(millisecondsTimeout: BaseRetryDelayMs + Random.Shared.Next(maxValue: MaxJitterMs));
                FailJob(queueJob: queueJob, exception: exception, attempt: attempt + 1);
            }
            else
            {
                logger?.LogError(exception: e, message: "Failed to record job failure for {JobId}", args: queueJob.Id);
            }
        }
    }

    /// <summary>
    /// Returns a reserved job to the available pool without executing it.
    /// Used by resource-gate logic: when the budget is saturated the worker
    /// releases the reservation and bumps <paramref name="availableAfter"/>
    /// so the job is not immediately picked up again.
    /// </summary>
    /// <remarks>
    /// Do NOT signal <see cref="WorkAvailable"/> here. The job is deferred by
    /// <paramref name="availableAfter"/> — there is no new work for OTHER
    /// workers to wake up for, and the calling worker is about to sleep its
    /// own retry interval. Releasing the semaphore woke every worker on the
    /// shared queue runner, which then burnt through the rest of the queue's
    /// deferred jobs in a tight loop (per-job DB query + JSON deserialize +
    /// budget probe) before any of them landed under the headroom threshold.
    /// </remarks>
    public void ReleaseReservation(QueueJobModel job, TimeSpan availableAfter, int attempt = 0)
    {
        try
        {
            lock (_writeLock)
            {
                job.ReservedAt = null;
                job.AvailableAt = DateTime.UtcNow + availableAfter;
                job.Attempts = (byte)Math.Max(val1: 0, val2: job.Attempts - 1);
                context.UpdateJob(job: job);
                context.SaveChanges();
            }
        }
        catch (Exception e)
        {
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational")
            {
                logger?.LogDebug(
                    exception: e,
                    message: "Queue DB contention releasing reservation for job {JobId}",
                    args: job.Id
                );
                return;
            }
            if (attempt < MaxDbRetryAttempts)
            {
                Thread.Sleep(millisecondsTimeout: BaseRetryDelayMs + Random.Shared.Next(maxValue: MaxJitterMs));
                ReleaseReservation(job: job, availableAfter: availableAfter, attempt: attempt + 1);
            }
            else
            {
                logger?.LogError(exception: e, message: "Failed to release reservation for job {JobId}", args: job.Id);
            }
        }
    }

    /// <summary>
    /// Replaces the serialized payload of a coordinator job in place and
    /// resets its reservation so it re-enters the queue after
    /// <paramref name="availableAfter"/>. The job's existing ID and queue
    /// slot are preserved — no deduplication check fires.
    /// </summary>
    public void UpdateJobPayload(
        int jobId,
        string newPayload,
        TimeSpan availableAfter,
        int attempt = 0
    )
    {
        try
        {
            lock (_writeLock)
            {
                context.UpdateJobPayload(jobId: jobId, newPayload: newPayload, availableAt: DateTime.UtcNow + availableAfter);
            }

            WorkAvailable.Release();
        }
        catch (Exception e)
        {
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational")
            {
                logger?.LogDebug(exception: e, message: "Queue DB contention updating payload for job {JobId}", args: jobId);
                return;
            }
            if (attempt < MaxDbRetryAttempts)
            {
                Thread.Sleep(millisecondsTimeout: BaseRetryDelayMs + Random.Shared.Next(maxValue: MaxJitterMs));
                UpdateJobPayload(jobId: jobId, newPayload: newPayload, availableAfter: availableAfter, attempt: attempt + 1);
            }
            else
            {
                logger?.LogError(exception: e, message: "Failed to update payload for job {JobId}", args: jobId);
            }
        }
    }

    /// <summary>
    /// Moves a reserved job onto a different queue with a replaced payload
    /// and resets its reservation so it re-enters immediately. Used by the
    /// GPU-budget safety net: a job pinned to a GPU device key that will
    /// never be satisfiable (the vendor isn't physically present on this
    /// host) is re-planned onto a software <c>ResourceRequirement</c> and
    /// rerouted to its now-CPU queue instead of looping at the budget gate
    /// forever. Attempts reset to 0 — the degraded requirement is a
    /// different, previously-unattempted shape of the job.
    /// </summary>
    public void Requeue(QueueJobModel job, string newQueue, string newPayload, int attempt = 0)
    {
        try
        {
            lock (_writeLock)
            {
                job.Queue = newQueue;
                job.ReservedAt = null;
                job.AvailableAt = DateTime.UtcNow;
                job.Attempts = 0;
                context.UpdateJob(job: job);
                context.UpdateJobPayload(jobId: job.Id, newPayload: newPayload, availableAt: DateTime.UtcNow);
                context.SaveChanges();
            }

            WorkAvailable.Release();
        }
        catch (Exception e)
        {
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational")
            {
                logger?.LogDebug(exception: e, message: "Queue DB contention requeuing job {JobId}", args: job.Id);
                return;
            }
            if (attempt < MaxDbRetryAttempts)
            {
                Thread.Sleep(millisecondsTimeout: BaseRetryDelayMs + Random.Shared.Next(maxValue: MaxJitterMs));
                Requeue(job: job, newQueue: newQueue, newPayload: newPayload, attempt: attempt + 1);
            }
            else
            {
                logger?.LogError(exception: e, message: "Failed to requeue job {JobId}", args: job.Id);
            }
        }
    }

    public void DeleteJob(QueueJobModel queueJob, int attempt = 0)
    {
        try
        {
            lock (_writeLock)
            {
                context.RemoveJob(job: queueJob);
            }
        }
        catch (Exception e)
        {
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational")
            {
                logger?.LogDebug(exception: e, message: "Queue DB contention deleting job {JobId}", args: queueJob.Id);
                return;
            }
            if (attempt < MaxDbRetryAttempts)
            {
                Thread.Sleep(millisecondsTimeout: BaseRetryDelayMs + Random.Shared.Next(maxValue: MaxJitterMs));
                DeleteJob(queueJob: queueJob, attempt: attempt + 1);
                return;
            }

            logger?.LogError(exception: e, message: "Failed to delete queue job {JobId}", args: queueJob.Id);
        }
    }

    public void RequeueFailedJob(int failedJobId, int attempt = 0)
    {
        try
        {
            lock (_writeLock)
            {
                FailedJobModel? failedJob = context.FindFailedJob(id: failedJobId);
                if (failedJob == null)
                    return;

                context.RemoveFailedJob(failedJob: failedJob);
                context.AddJob(
                    job: new()
                    {
                        Queue = failedJob.Queue,
                        Payload = failedJob.Payload,
                        AvailableAt = DateTime.UtcNow,
                        Attempts = 0,
                    }
                );

                context.SaveChanges();
            }
        }
        catch (Exception e)
        {
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational")
            {
                logger?.LogDebug(
                    exception: e,
                    message: "Queue DB contention requeuing failed job {FailedJobId}",
                    args: failedJobId
                );
                return;
            }
            if (attempt < MaxDbRetryAttempts)
            {
                Thread.Sleep(millisecondsTimeout: BaseRetryDelayMs + Random.Shared.Next(maxValue: MaxJitterMs));
                RequeueFailedJob(failedJobId: failedJobId, attempt: attempt + 1);
            }
            else
            {
                logger?.LogError(exception: e, message: "Failed to requeue failed job {FailedJobId}", args: failedJobId);
            }
        }
    }

    public void RetryFailedJobs(long? failedJobId = null)
    {
        lock (_writeLock)
        {
            IReadOnlyList<FailedJobModel> failedJobs = context.GetFailedJobs(failedJobId: failedJobId);

            foreach (FailedJobModel failedJob in failedJobs)
            {
                context.AddJob(
                    job: new()
                    {
                        Queue = failedJob.Queue,
                        Payload = failedJob.Payload,
                        AvailableAt = DateTime.UtcNow,
                        Attempts = 0,
                    }
                );

                context.RemoveFailedJob(failedJob: failedJob);
            }

            context.SaveChanges();
        }
    }
}
