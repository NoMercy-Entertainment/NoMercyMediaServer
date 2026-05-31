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

    private static readonly object _writeLock = new();

    /// <summary>
    /// Signalled once per <see cref="Enqueue"/> call so idle workers wake
    /// immediately instead of waiting out a fixed poll interval.
    /// </summary>
    internal readonly SemaphoreSlim WorkAvailable = new(0);

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
            bool exists = context.JobExists(queueJob.Payload);
            if (exists)
                return;

            context.AddJob(queueJob);
        }

        WorkAvailable.Release();
    }

    public QueueJobModel? Dequeue()
    {
        lock (_writeLock)
        {
            QueueJobModel? job = context.GetNextJob("", 255, null);
            if (job == null)
                return job;

            context.RemoveJob(job);

            return job;
        }
    }

    public QueueJobModel? ReserveJob(string name, long? currentJobId, int attempt = 0)
    {
        try
        {
            lock (_writeLock)
            {
                QueueJobModel? job = context.GetNextJob(name, maxAttempts, currentJobId);

                if (job == null)
                    return job;

                // Child jobs whose coordinator has already failed should not
                // run — they would produce orphaned output. Move them directly
                // to FailedJobs with a synthetic exception so the dashboard
                // shows "failed-by-parent" rather than silently dropping them.
                if (job.ParentJobId.HasValue && context.IsParentFailed(job.ParentJobId.Value))
                {
                    FailedJobModel skipped = new()
                    {
                        Uuid = Guid.NewGuid(),
                        Connection = "default",
                        Queue = job.Queue,
                        Payload = job.Payload,
                        Exception =
                            $"{{\"Message\":\"Skipped: parent job {job.ParentJobId} failed\"}}",
                        FailedAt = DateTime.UtcNow,
                    };
                    context.AddFailedJob(skipped);
                    context.RemoveJob(job);
                    context.SaveChanges();
                    return null;
                }

                job.ReservedAt = DateTime.UtcNow;
                job.Attempts++;

                context.UpdateJob(job);

                return job;
            }
        }
        catch (Exception e)
        {
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational")
                return null;
            if (attempt < MaxDbRetryAttempts)
            {
                Thread.Sleep(BaseRetryDelayMs + Random.Shared.Next(MaxJitterMs));
                return ReserveJob(name, currentJobId, attempt + 1);
            }

            logger?.LogError("{Message}", e.Message);
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
                        Exception = JsonConvert.SerializeObject(
                            exception.InnerException ?? exception
                        ),
                        FailedAt = DateTime.UtcNow,
                    };

                    context.AddFailedJob(failedJob);
                    context.RemoveJob(queueJob);
                }
                else
                {
                    context.UpdateJob(queueJob);
                }

                context.SaveChanges();
            }
        }
        catch (Exception e)
        {
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational")
                return;
            if (attempt < MaxDbRetryAttempts)
            {
                Thread.Sleep(BaseRetryDelayMs + Random.Shared.Next(MaxJitterMs));
                FailJob(queueJob, exception, attempt + 1);
            }
            else
            {
                logger?.LogError("{Message}", e.Message);
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
    public void ReleaseReservation(QueueJobModel job, TimeSpan availableAfter)
    {
        lock (_writeLock)
        {
            job.ReservedAt = null;
            job.AvailableAt = DateTime.UtcNow + availableAfter;
            job.Attempts = (byte)Math.Max(0, job.Attempts - 1);
            context.UpdateJob(job);
            context.SaveChanges();
        }
    }

    /// <summary>
    /// Replaces the serialized payload of a coordinator job in place and
    /// resets its reservation so it re-enters the queue after
    /// <paramref name="availableAfter"/>. The job's existing ID and queue
    /// slot are preserved — no deduplication check fires.
    /// </summary>
    public void UpdateJobPayload(int jobId, string newPayload, TimeSpan availableAfter)
    {
        lock (_writeLock)
        {
            context.UpdateJobPayload(jobId, newPayload, DateTime.UtcNow + availableAfter);
        }

        WorkAvailable.Release();
    }

    public void DeleteJob(QueueJobModel queueJob, int attempt = 0)
    {
        try
        {
            lock (_writeLock)
            {
                context.RemoveJob(queueJob);
            }
        }
        catch (Exception) { }
    }

    public void RequeueFailedJob(int failedJobId, int attempt = 0)
    {
        try
        {
            lock (_writeLock)
            {
                FailedJobModel? failedJob = context.FindFailedJob(failedJobId);
                if (failedJob == null)
                    return;

                context.RemoveFailedJob(failedJob);
                context.AddJob(
                    new()
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
                return;
            if (attempt < MaxDbRetryAttempts)
            {
                Thread.Sleep(BaseRetryDelayMs + Random.Shared.Next(MaxJitterMs));
                RequeueFailedJob(failedJobId, attempt + 1);
            }
            else
            {
                logger?.LogError("{Message}", e.Message);
            }
        }
    }

    public void RetryFailedJobs(long? failedJobId = null)
    {
        lock (_writeLock)
        {
            IReadOnlyList<FailedJobModel> failedJobs = context.GetFailedJobs(failedJobId);

            foreach (FailedJobModel failedJob in failedJobs)
            {
                context.AddJob(
                    new()
                    {
                        Queue = failedJob.Queue,
                        Payload = failedJob.Payload,
                        AvailableAt = DateTime.UtcNow,
                        Attempts = 0,
                    }
                );

                context.RemoveFailedJob(failedJob);
            }

            context.SaveChanges();
        }
    }
}
