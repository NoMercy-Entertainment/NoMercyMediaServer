using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;

namespace NoMercy.Queue.MediaServer;

public class EfQueueContextAdapter : IQueueContext
{
    public static readonly Func<QueueContext, byte, string, long?, QueueJob?> ReserveJobQuery =
        EF.CompileQuery((QueueContext queueContext, byte maxAttempts, string name, long? currentJobId) =>
            queueContext.QueueJobs
                .Where(j => j.ReservedAt == null && j.Attempts <= maxAttempts)
                .Where(j => currentJobId == null)
                .Where(j => j.Queue == name)
                .OrderByDescending(j => j.Priority)
                .FirstOrDefault());

    public static readonly Func<QueueContext, string, bool> ExistsQuery =
        EF.CompileQuery((QueueContext queueContext, string payloadString) =>
            queueContext.QueueJobs.Any(queueJob => queueJob.Payload == payloadString));

    private readonly Func<QueueContext> _contextFactory;
    private readonly bool _ownsContext;

    /// <summary>
    /// Creates an adapter that manages its own QueueContext per operation (thread-safe).
    /// </summary>
    public EfQueueContextAdapter()
    {
        _contextFactory = static () => new QueueContext();
        _ownsContext = true;
    }

    /// <summary>
    /// Creates an adapter using a shared QueueContext (for tests with in-memory databases).
    /// The caller is responsible for disposing the context.
    /// </summary>
    public EfQueueContextAdapter(QueueContext context)
    {
        _contextFactory = () => context;
        _ownsContext = false;
    }

    private QueueContext AcquireContext() => _contextFactory();

    private void ReleaseContext(QueueContext context)
    {
        if (_ownsContext) context.Dispose();
    }

    public void AddJob(QueueJobModel job)
    {
        QueueContext context = AcquireContext();
        try
        {
            QueueJob entity = new()
            {
                Priority = job.Priority,
                Queue = job.Queue,
                Payload = job.Payload,
                Attempts = job.Attempts,
                ReservedAt = job.ReservedAt,
                AvailableAt = job.AvailableAt,
                CreatedAt = job.CreatedAt
            };
            context.QueueJobs.Add(entity);
            context.SaveChanges();
            context.ChangeTracker.Clear();
            job.Id = entity.Id;
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public void RemoveJob(QueueJobModel job)
    {
        QueueContext context = AcquireContext();
        try
        {
            QueueJob? entity = context.QueueJobs.Find(job.Id);
            if (entity == null)
            {
                entity = new()
                {
                    Id = job.Id,
                    Payload = job.Payload,
                    Queue = job.Queue
                };
                context.QueueJobs.Attach(entity);
            }
            context.QueueJobs.Remove(entity);
            context.SaveChanges();
            context.ChangeTracker.Clear();
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public QueueJobModel? GetNextJob(string queueName, byte maxAttempts, long? currentJobId)
    {
        QueueContext context = AcquireContext();
        try
        {
            if (string.IsNullOrEmpty(queueName))
            {
                QueueJob? anyJob = context.QueueJobs.FirstOrDefault();
                return anyJob == null ? null : ToModel(anyJob);
            }

            QueueJob? job = ReserveJobQuery(context, maxAttempts, queueName, currentJobId);
            return job == null ? null : ToModel(job);
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public QueueJobModel? FindJob(int id)
    {
        QueueContext context = AcquireContext();
        try
        {
            QueueJob? job = context.QueueJobs.Find(id);
            return job == null ? null : ToModel(job);
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public bool JobExists(string payload)
    {
        QueueContext context = AcquireContext();
        try
        {
            return ExistsQuery(context, payload);
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public void UpdateJob(QueueJobModel job)
    {
        QueueContext context = AcquireContext();
        try
        {
            QueueJob? entity = context.QueueJobs.Find(job.Id);
            if (entity == null) return;

            entity.Priority = job.Priority;
            entity.Queue = job.Queue;
            entity.Attempts = job.Attempts;
            entity.ReservedAt = job.ReservedAt;
            entity.AvailableAt = job.AvailableAt;
            context.SaveChanges();
            context.ChangeTracker.Clear();
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public void ResetAllReservedJobs()
    {
        QueueContext context = AcquireContext();
        try
        {
            foreach (QueueJob job in context.QueueJobs)
            {
                job.ReservedAt = null;
            }
            context.SaveChanges();
            context.ChangeTracker.Clear();
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public void AddFailedJob(FailedJobModel failedJob)
    {
        QueueContext context = AcquireContext();
        try
        {
            FailedJob entity = new()
            {
                Uuid = failedJob.Uuid,
                Connection = failedJob.Connection,
                Queue = failedJob.Queue,
                Payload = failedJob.Payload,
                Exception = failedJob.Exception,
                FailedAt = failedJob.FailedAt
            };
            context.FailedJobs.Add(entity);
            context.SaveChanges();
            context.ChangeTracker.Clear();
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public void RemoveFailedJob(FailedJobModel failedJob)
    {
        QueueContext context = AcquireContext();
        try
        {
            FailedJob? entity = context.FailedJobs.Find(failedJob.Id);
            if (entity != null)
            {
                context.FailedJobs.Remove(entity);
                context.SaveChanges();
                context.ChangeTracker.Clear();
            }
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public FailedJobModel? FindFailedJob(int id)
    {
        QueueContext context = AcquireContext();
        try
        {
            FailedJob? entity = context.FailedJobs.Find((long)id);
            return entity == null ? null : ToFailedModel(entity);
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null)
    {
        QueueContext context = AcquireContext();
        try
        {
            IQueryable<FailedJob> query = context.FailedJobs;
            if (failedJobId.HasValue)
                query = query.Where(j => j.Id == failedJobId.Value);

            return query.Select(j => new FailedJobModel
            {
                Id = j.Id,
                Uuid = j.Uuid,
                Connection = j.Connection,
                Queue = j.Queue,
                Payload = j.Payload,
                Exception = j.Exception,
                FailedAt = j.FailedAt
            }).ToList();
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public IReadOnlyList<CronJobModel> GetEnabledCronJobs()
    {
        QueueContext context = AcquireContext();
        try
        {
            return context.CronJobs
                .Where(c => c.IsEnabled)
                .Select(c => new CronJobModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    CronExpression = c.CronExpression,
                    JobType = c.JobType,
                    Parameters = c.Parameters,
                    IsEnabled = c.IsEnabled,
                    LastRun = c.LastRun,
                    NextRun = c.NextRun
                }).ToList();
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public CronJobModel? FindCronJobByName(string name)
    {
        QueueContext context = AcquireContext();
        try
        {
            CronJob? entity = context.CronJobs.FirstOrDefault(c => c.Name == name);
            return entity == null ? null : ToCronModel(entity);
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public void AddCronJob(CronJobModel cronJob)
    {
        QueueContext context = AcquireContext();
        try
        {
            CronJob entity = new()
            {
                Name = cronJob.Name,
                CronExpression = cronJob.CronExpression,
                JobType = cronJob.JobType,
                Parameters = cronJob.Parameters,
                IsEnabled = cronJob.IsEnabled,
                LastRun = cronJob.LastRun,
                NextRun = cronJob.NextRun
            };
            context.CronJobs.Add(entity);
            context.SaveChanges();
            context.ChangeTracker.Clear();
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public void UpdateCronJob(CronJobModel cronJob)
    {
        QueueContext context = AcquireContext();
        try
        {
            CronJob? entity = context.CronJobs.Find(cronJob.Id);
            if (entity == null) return;

            entity.CronExpression = cronJob.CronExpression;
            entity.IsEnabled = cronJob.IsEnabled;
            entity.LastRun = cronJob.LastRun;
            entity.NextRun = cronJob.NextRun;
            context.SaveChanges();
            context.ChangeTracker.Clear();
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public void RemoveCronJob(CronJobModel cronJob)
    {
        QueueContext context = AcquireContext();
        try
        {
            CronJob? entity = context.CronJobs.Find(cronJob.Id);
            if (entity != null)
            {
                context.CronJobs.Remove(entity);
                context.SaveChanges();
                context.ChangeTracker.Clear();
            }
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public void SaveChanges()
    {
        // No-op: each method now manages its own context and saves immediately
    }

    public void Dispose()
    {
        // No shared context to dispose when using per-operation contexts
    }

    private static QueueJobModel ToModel(QueueJob entity)
    {
        return new()
        {
            Id = entity.Id,
            Priority = entity.Priority,
            Queue = entity.Queue,
            Payload = entity.Payload,
            Attempts = entity.Attempts,
            ReservedAt = entity.ReservedAt,
            AvailableAt = entity.AvailableAt,
            CreatedAt = entity.CreatedAt
        };
    }

    private static FailedJobModel ToFailedModel(FailedJob entity)
    {
        return new()
        {
            Id = entity.Id,
            Uuid = entity.Uuid,
            Connection = entity.Connection,
            Queue = entity.Queue,
            Payload = entity.Payload,
            Exception = entity.Exception,
            FailedAt = entity.FailedAt
        };
    }

    private static CronJobModel ToCronModel(CronJob entity)
    {
        return new()
        {
            Id = entity.Id,
            Name = entity.Name,
            CronExpression = entity.CronExpression,
            JobType = entity.JobType,
            Parameters = entity.Parameters,
            IsEnabled = entity.IsEnabled,
            LastRun = entity.LastRun,
            NextRun = entity.NextRun
        };
    }
}
