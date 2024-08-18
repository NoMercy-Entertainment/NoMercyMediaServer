using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using Serilog.Events;

namespace NoMercy.Helpers.system;

public class JobQueue(QueueContext context, byte maxAttempts = 3)
{
    private QueueContext Context { get; } = context;

    public void Enqueue(QueueJob queueJob)
    {
        lock (Context)
        {
            bool exists = Exists(queueJob.Payload);
            if (exists) return;

            Context.QueueJobs.Add(queueJob);

            try
            {
                Context.SaveChanges();
            }
            catch (Exception e)
            {
                Logger.Queue(e, LogEventLevel.Error);
            }
        }
    }

    public QueueJob? Dequeue()
    {
        lock (Context)
        {
            QueueJob? job = Context.QueueJobs.FirstOrDefault();
            if (job == null) return job;

            Context.QueueJobs.Remove(job);

            try
            {
                Context.SaveChanges();
            }
            catch (Exception e)
            {
                Logger.Queue(e, LogEventLevel.Error);
            }

            return job;
        }
    }

    public static readonly Func<QueueContext, byte, string, long?, Task<QueueJob?>> ReserveJobQuery =
        EF.CompileAsyncQuery((QueueContext queueContext, byte maxAttempts, string name, long? currentJobId) =>
            queueContext.QueueJobs
                .Where(j => j.ReservedAt == null && j.Attempts <= maxAttempts)
                .Where(j => currentJobId == null)
                .Where(j => j.Queue == name)
                .OrderByDescending(j => j.Priority)
                .FirstOrDefault());


    public QueueJob? ReserveJob(string name, long? currentJobId)
    {
        lock (Context)
        {
            QueueJob? job = ReserveJobQuery(Context, maxAttempts, name, currentJobId).Result;

            if (job == null) return job;

            job.ReservedAt = DateTime.UtcNow;
            job.Attempts++;

            try
            {
                Context.SaveChanges();
            }
            catch (Exception _)
            {
                ReserveJob(name, currentJobId);
            }

            return job;
        }
    }

    public void FailJob(QueueJob queueJob, Exception exception)
    {
        lock (Context)
        {
            queueJob.ReservedAt = null;

            if (queueJob.Attempts >= maxAttempts)
            {
                FailedJob failedJob = new()
                {
                    Uuid = Guid.NewGuid(),
                    Connection = "default",
                    Queue = queueJob.Queue,
                    Payload = queueJob.Payload,
                    Exception = JsonConvert.SerializeObject(exception.InnerException ?? exception),
                    FailedAt = DateTime.UtcNow
                };

                Context.FailedJobs.Add(failedJob);
                Context.QueueJobs.Remove(queueJob);
            }

            Context.SaveChanges();
        }
    }

    public void DeleteJob(QueueJob queueJob)
    {
        lock (Context)
        {
            Context.QueueJobs.Remove(queueJob);

            try
            {
                Context.SaveChanges();
            }
            catch (Exception e)
            {
                Logger.Queue(e, LogEventLevel.Error);
            }
        }
    }

    public void RequeueFailedJob(int failedJobId)
    {
        lock (Context)
        {
            FailedJob? failedJob = Context.FailedJobs.Find(failedJobId);
            if (failedJob == null) return;

            Context.FailedJobs.Remove(failedJob);
            Context.QueueJobs.Add(new QueueJob
            {
                Queue = failedJob.Queue,
                Payload = failedJob.Payload,
                AvailableAt = DateTime.UtcNow,
                Attempts = 0
            });

            try
            {
                Context.SaveChanges();
            }
            catch (Exception e)
            {
                Logger.Queue(e, LogEventLevel.Error);
            }
        }
    }

    private static readonly Func<QueueContext, string, Task<bool>> ExistsQuery =
        EF.CompileAsyncQuery((QueueContext queueContext, string payloadString) =>
            queueContext.QueueJobs.Any(queueJob => queueJob.Payload == payloadString));

    public bool Exists(string payloadString)
    {
        return ExistsQuery(Context, payloadString).Result;
    }
}