using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Queue;

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

            if (Context.ChangeTracker.HasChanges()) Context.SaveChanges();
        }
    }

    public QueueJob? Dequeue()
    {
        lock (Context)
        {
            QueueJob? job = Context.QueueJobs.FirstOrDefault();
            if (job == null) return job;

            Context.QueueJobs.Remove(job);

            if (Context.ChangeTracker.HasChanges()) Context.SaveChanges();

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


    public QueueJob? ReserveJob(string name, long? currentJobId, int attempt = 0)
    {
        try
        {
            lock (Context)
            {
                QueueJob? job = ReserveJobQuery(Context, maxAttempts, name, currentJobId).Result;

                if (job == null) return job;

                job.ReservedAt = DateTime.UtcNow;
                job.Attempts++;

                if (Context.ChangeTracker.HasChanges()) Context.SaveChanges();

                return job;
            }
        }
        catch (Exception e)
        {
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational") return null;
            if (attempt < 10)
            {
                Thread.Sleep(2000);
                ReserveJob(name, currentJobId, attempt + 1);
            }
            else
            {
                Logger.Queue(e.Message, LogEventLevel.Error);
            }
        }

        return null;
    }

    public void FailJob(QueueJob queueJob, Exception exception, int attempt = 0)
    {
        try
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

                if (Context.ChangeTracker.HasChanges()) Context.SaveChanges();
            }
        }
        catch (Exception e)
        {
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational") return;
            if (attempt < 10)
            {
                Thread.Sleep(2000);
                FailJob(queueJob, exception, attempt + 1);
            }
            else
            {
                Logger.Queue(e.Message, LogEventLevel.Error);
            }
        }
    }

    public void DeleteJob(QueueJob queueJob, int attempt = 0)
    {
        try
        {
            lock (Context)
            {
                Context.QueueJobs.Remove(queueJob);

                if (Context.ChangeTracker.HasChanges()) Context.SaveChanges();
            }
        }
        catch (Exception)
        {
            // if (e.Message.Contains("affected 0 row(s)"))
            // {
            //     
            // }
            // else if (attempt < 10)
            // {
            //     Thread.Sleep(2000);
            //     DeleteJob(queueJob, attempt + 1);
            // }
            // else
            // {
            //     Logger.Queue(e.Message, LogEventLevel.Error);
            // }
        }
    }

    public void RequeueFailedJob(int failedJobId, int attempt = 0)
    {
        try
        {
            lock (Context)
            {
                FailedJob? failedJob = Context.FailedJobs.Find(failedJobId);
                if (failedJob == null) return;

                Context.FailedJobs.Remove(failedJob);
                Context.QueueJobs.Add(new()
                {
                    Queue = failedJob.Queue,
                    Payload = failedJob.Payload,
                    AvailableAt = DateTime.UtcNow,
                    Attempts = 0
                });

                if (Context.ChangeTracker.HasChanges()) Context.SaveChanges();
            }
        }
        catch (Exception e)
        {            
            if (e.Source == "Microsoft.EntityFrameworkCore.Relational") return;
            if (attempt < 10)
            {
                Thread.Sleep(2000);
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
        lock (Context)
        {
            IQueryable<FailedJob> failedJobsQuery = Context.FailedJobs;

            if (failedJobId.HasValue) failedJobsQuery = failedJobsQuery.Where(j => j.Id == failedJobId.Value);

            List<FailedJob> failedJobs = failedJobsQuery.ToList();

            foreach (FailedJob failedJob in failedJobs)
            {
                Context.QueueJobs.Add(new()
                {
                    Queue = failedJob.Queue,
                    Payload = failedJob.Payload,
                    AvailableAt = DateTime.UtcNow,
                    Attempts = 0
                });

                Context.FailedJobs.Remove(failedJob);
            }

            if (Context.ChangeTracker.HasChanges()) Context.SaveChanges();
        }
    }

    private static readonly Func<QueueContext, string, Task<bool>> ExistsQuery =
        EF.CompileAsyncQuery((QueueContext queueContext, string payloadString) =>
            queueContext.QueueJobs.Any(queueJob => queueJob.Payload == payloadString));

    private bool Exists(string payloadString)
    {
        return ExistsQuery(Context, payloadString).Result;
    }
}