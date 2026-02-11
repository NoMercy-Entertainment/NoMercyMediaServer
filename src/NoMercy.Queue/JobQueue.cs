using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Queue;

public class JobQueue(QueueContext context, byte maxAttempts = 3)
{
    private const int MaxDbRetryAttempts = 5;
    private const int BaseRetryDelayMs = 2000;
    private const int MaxJitterMs = 500;

    private static readonly object _writeLock = new();
    private QueueContext Context { get; } = context;

    public void Enqueue(QueueJob queueJob)
    {
        lock (_writeLock)
        {
            bool exists = Exists(queueJob.Payload);
            if (exists) return;

            Context.QueueJobs.Add(queueJob);

            if (Context.ChangeTracker.HasChanges())
            {
                Context.SaveChanges();
                Context.ChangeTracker.Clear();
            }
        }
    }

    public QueueJob? Dequeue()
    {
        lock (_writeLock)
        {
            QueueJob? job = Context.QueueJobs.FirstOrDefault();
            if (job == null) return job;

            Context.QueueJobs.Remove(job);

            if (Context.ChangeTracker.HasChanges())
            {
                Context.SaveChanges();
                Context.ChangeTracker.Clear();
            }

            return job;
        }
    }

    public static readonly Func<QueueContext, byte, string, long?, QueueJob?> ReserveJobQuery =
        EF.CompileQuery((QueueContext queueContext, byte maxAttempts, string name, long? currentJobId) =>
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
            lock (_writeLock)
            {
                QueueJob? job = ReserveJobQuery(Context, maxAttempts, name, currentJobId);

                if (job == null) return job;

                job.ReservedAt = DateTime.UtcNow;
                job.Attempts++;

                if (Context.ChangeTracker.HasChanges())
                {
                    Context.SaveChanges();
                    Context.ChangeTracker.Clear();
                }

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

    public void FailJob(QueueJob queueJob, Exception exception, int attempt = 0)
    {
        try
        {
            lock (_writeLock)
            {
                if (Context.Entry(queueJob).State == EntityState.Detached)
                    Context.QueueJobs.Attach(queueJob);

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

                if (Context.ChangeTracker.HasChanges())
                {
                    Context.SaveChanges();
                    Context.ChangeTracker.Clear();
                }
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

    public void DeleteJob(QueueJob queueJob, int attempt = 0)
    {
        try
        {
            lock (_writeLock)
            {
                if (Context.Entry(queueJob).State == EntityState.Detached)
                    Context.QueueJobs.Attach(queueJob);

                Context.QueueJobs.Remove(queueJob);

                if (Context.ChangeTracker.HasChanges())
                {
                    Context.SaveChanges();
                    Context.ChangeTracker.Clear();
                }
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
            lock (_writeLock)
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

                if (Context.ChangeTracker.HasChanges())
                {
                    Context.SaveChanges();
                    Context.ChangeTracker.Clear();
                }
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

            if (Context.ChangeTracker.HasChanges())
            {
                Context.SaveChanges();
                Context.ChangeTracker.Clear();
            }
        }
    }

    private static readonly Func<QueueContext, string, bool> ExistsQuery =
        EF.CompileQuery((QueueContext queueContext, string payloadString) =>
            queueContext.QueueJobs.Any(queueJob => queueJob.Payload == payloadString));

    private bool Exists(string payloadString)
    {
        return ExistsQuery(Context, payloadString);
    }
}