using Newtonsoft.Json;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Queue.Core.Interfaces;
using NoMercy.Queue.Core.Models;
using Serilog.Events;

namespace NoMercy.Queue;

public class JobQueue(IQueueContext context, byte maxAttempts = 3)
{
    private const int MaxDbRetryAttempts = 5;
    private const int BaseRetryDelayMs = 2000;
    private const int MaxJitterMs = 500;

    private static readonly object _writeLock = new();

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
            if (exists) return;

            context.AddJob(queueJob);
        }
    }

    public QueueJobModel? Dequeue()
    {
        lock (_writeLock)
        {
            QueueJobModel? job = context.GetNextJob("", 255, null);
            if (job == null) return job;

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

                if (job == null) return job;

                job.ReservedAt = DateTime.UtcNow;
                job.Attempts++;

                context.UpdateJob(job);

                return job;
            }
        }
        catch (Exception e)
        {
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational") return null;
            if (attempt < MaxDbRetryAttempts)
            {
                Thread.Sleep(BaseRetryDelayMs + Random.Shared.Next(MaxJitterMs));
                return ReserveJob(name, currentJobId, attempt + 1);
            }

            Logger.Queue(e.Message, LogEventLevel.Error);
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
                        Exception = JsonConvert.SerializeObject(exception.InnerException ?? exception),
                        FailedAt = DateTime.UtcNow
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
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational") return;
            if (attempt < MaxDbRetryAttempts)
            {
                Thread.Sleep(BaseRetryDelayMs + Random.Shared.Next(MaxJitterMs));
                FailJob(queueJob, exception, attempt + 1);
            }
            else
            {
                Logger.Queue(e.Message, LogEventLevel.Error);
            }
        }
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
        catch (Exception)
        {
        }
    }

    public void RequeueFailedJob(int failedJobId, int attempt = 0)
    {
        try
        {
            lock (_writeLock)
            {
                FailedJobModel? failedJob = context.FindFailedJob(failedJobId);
                if (failedJob == null) return;

                context.RemoveFailedJob(failedJob);
                context.AddJob(new()
                {
                    Queue = failedJob.Queue,
                    Payload = failedJob.Payload,
                    AvailableAt = DateTime.UtcNow,
                    Attempts = 0
                });

                context.SaveChanges();
            }
        }
        catch (Exception e)
        {
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational") return;
            if (attempt < MaxDbRetryAttempts)
            {
                Thread.Sleep(BaseRetryDelayMs + Random.Shared.Next(MaxJitterMs));
                RequeueFailedJob(failedJobId, attempt + 1);
            }
            else
            {
                Logger.Queue(e.Message, LogEventLevel.Error);
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
                context.AddJob(new()
                {
                    Queue = failedJob.Queue,
                    Payload = failedJob.Payload,
                    AvailableAt = DateTime.UtcNow,
                    Attempts = 0
                });

                context.RemoveFailedJob(failedJob);
            }

            context.SaveChanges();
        }
    }
}
