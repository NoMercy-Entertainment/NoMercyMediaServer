using Microsoft.EntityFrameworkCore;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Sqlite.Entities;

namespace NoMercyQueue.Sqlite;

public class SqliteQueueContext : IQueueContext
{
    private readonly QueueDbContext _context;

    internal static readonly Func<QueueDbContext, byte, string, long?, QueueJobEntity?> ReserveJobQuery =
        EF.CompileQuery((QueueDbContext context, byte maxAttempts, string name, long? currentJobId) =>
            context.QueueJobs
                .Where(j => j.ReservedAt == null && j.Attempts <= maxAttempts)
                .Where(j => currentJobId == null)
                .Where(j => j.Queue == name)
                .OrderByDescending(j => j.Priority)
                .FirstOrDefault());

    internal static readonly Func<QueueDbContext, string, bool> ExistsQuery =
        EF.CompileQuery((QueueDbContext context, string payloadString) =>
            context.QueueJobs.Any(j => j.Payload == payloadString));

    internal SqliteQueueContext(QueueDbContext context)
    {
        _context = context;
    }

    public void AddJob(QueueJobModel job)
    {
        QueueJobEntity entity = new()
        {
            Priority = job.Priority,
            Queue = job.Queue,
            Payload = job.Payload,
            Attempts = job.Attempts,
            ReservedAt = job.ReservedAt,
            AvailableAt = job.AvailableAt,
            CreatedAt = job.CreatedAt
        };
        _context.QueueJobs.Add(entity);
        SaveAndClear();
        job.Id = entity.Id;
    }

    public void RemoveJob(QueueJobModel job)
    {
        QueueJobEntity? entity = _context.QueueJobs.Find(job.Id);
        if (entity == null)
        {
            entity = new()
            {
                Id = job.Id,
                Payload = job.Payload,
                Queue = job.Queue
            };
            _context.QueueJobs.Attach(entity);
        }
        _context.QueueJobs.Remove(entity);
        SaveAndClear();
    }

    public QueueJobModel? GetNextJob(string queueName, byte maxAttempts, long? currentJobId)
    {
        if (string.IsNullOrEmpty(queueName))
        {
            QueueJobEntity? anyJob = _context.QueueJobs.FirstOrDefault();
            return anyJob == null ? null : ToModel(anyJob);
        }

        QueueJobEntity? job = ReserveJobQuery(_context, maxAttempts, queueName, currentJobId);
        if (job == null) return null;

        return ToModel(job);
    }

    public QueueJobModel? FindJob(int id)
    {
        QueueJobEntity? job = _context.QueueJobs.Find(id);
        return job == null ? null : ToModel(job);
    }

    public bool JobExists(string payload)
    {
        return ExistsQuery(_context, payload);
    }

    public void UpdateJob(QueueJobModel job)
    {
        QueueJobEntity? entity = _context.QueueJobs.Find(job.Id);
        if (entity == null) return;

        entity.Priority = job.Priority;
        entity.Queue = job.Queue;
        entity.Attempts = job.Attempts;
        entity.ReservedAt = job.ReservedAt;
        entity.AvailableAt = job.AvailableAt;
        SaveAndClear();
    }

    public void ResetAllReservedJobs()
    {
        foreach (QueueJobEntity job in _context.QueueJobs)
        {
            job.ReservedAt = null;
        }
        SaveAndClear();
    }

    public void AddFailedJob(FailedJobModel failedJob)
    {
        FailedJobEntity entity = new()
        {
            Uuid = failedJob.Uuid,
            Connection = failedJob.Connection,
            Queue = failedJob.Queue,
            Payload = failedJob.Payload,
            Exception = failedJob.Exception,
            FailedAt = failedJob.FailedAt
        };
        _context.FailedJobs.Add(entity);
    }

    public void RemoveFailedJob(FailedJobModel failedJob)
    {
        FailedJobEntity? entity = _context.FailedJobs.Find(failedJob.Id);
        if (entity != null)
        {
            _context.FailedJobs.Remove(entity);
        }
    }

    public FailedJobModel? FindFailedJob(int id)
    {
        FailedJobEntity? entity = _context.FailedJobs.Find((long)id);
        return entity == null ? null : ToFailedModel(entity);
    }

    public IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null)
    {
        IQueryable<FailedJobEntity> query = _context.FailedJobs;
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

    public IReadOnlyList<CronJobModel> GetEnabledCronJobs()
    {
        return _context.CronJobs
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

    public CronJobModel? FindCronJobByName(string name)
    {
        CronJobEntity? entity = _context.CronJobs.FirstOrDefault(c => c.Name == name);
        return entity == null ? null : ToCronModel(entity);
    }

    public void AddCronJob(CronJobModel cronJob)
    {
        CronJobEntity entity = new()
        {
            Name = cronJob.Name,
            CronExpression = cronJob.CronExpression,
            JobType = cronJob.JobType,
            Parameters = cronJob.Parameters,
            IsEnabled = cronJob.IsEnabled,
            LastRun = cronJob.LastRun,
            NextRun = cronJob.NextRun
        };
        _context.CronJobs.Add(entity);
        SaveAndClear();
    }

    public void UpdateCronJob(CronJobModel cronJob)
    {
        CronJobEntity? entity = _context.CronJobs.Find(cronJob.Id);
        if (entity == null) return;

        entity.CronExpression = cronJob.CronExpression;
        entity.IsEnabled = cronJob.IsEnabled;
        entity.LastRun = cronJob.LastRun;
        entity.NextRun = cronJob.NextRun;
        SaveAndClear();
    }

    public void RemoveCronJob(CronJobModel cronJob)
    {
        CronJobEntity? entity = _context.CronJobs.Find(cronJob.Id);
        if (entity != null)
        {
            _context.CronJobs.Remove(entity);
            SaveAndClear();
        }
    }

    public void SaveChanges()
    {
        SaveAndClear();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private void SaveAndClear()
    {
        if (_context.ChangeTracker.HasChanges())
        {
            _context.SaveChanges();
            _context.ChangeTracker.Clear();
        }
    }

    private static QueueJobModel ToModel(QueueJobEntity entity)
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

    private static FailedJobModel ToFailedModel(FailedJobEntity entity)
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

    private static CronJobModel ToCronModel(CronJobEntity entity)
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
