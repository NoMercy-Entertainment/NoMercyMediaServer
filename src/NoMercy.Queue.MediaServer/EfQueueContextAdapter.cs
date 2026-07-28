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

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;

namespace NoMercy.Queue.MediaServer;

public class EfQueueContextAdapter : IQueueContext
{
    public static readonly Func<
        QueueContext,
        byte,
        string,
        long?,
        DateTime,
        QueueJob?
    > ReserveJobQuery = EF.CompileQuery(
        (
            QueueContext queueContext,
            byte maxAttempts,
            string name,
            long? currentJobId,
            DateTime now
        ) =>
            queueContext
                .QueueJobs.Where(j =>
                    j.ReservedAt == null && j.Attempts < maxAttempts && j.AvailableAt <= now
                )
                .Where(j => currentJobId == null)
                .Where(j => j.Queue == name)
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.CreatedAt)
                .ThenBy(j => j.Id)
                .FirstOrDefault()
    );

    public static readonly Func<QueueContext, string, bool> ExistsQuery = EF.CompileQuery(
        (QueueContext queueContext, string payloadString) =>
            queueContext.QueueJobs.Any(queueJob => queueJob.Payload == payloadString)
    );

    private readonly Func<QueueContext> _contextFactory;
    private readonly bool _ownsContext;

    /// <summary>
    /// Creates an adapter that manages its own QueueContext per operation (thread-safe).
    /// </summary>
    public EfQueueContextAdapter()
    {
        _contextFactory = static () => new();
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
        if (_ownsContext)
            context.Dispose();
    }

    private T Execute<T>(Func<QueueContext, T> operation)
    {
        QueueContext context = AcquireContext();
        try
        {
            return operation(context);
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    private void Execute(Action<QueueContext> operation)
    {
        QueueContext context = AcquireContext();
        try
        {
            operation(context);
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    public void AddJob(QueueJobModel job)
    {
        Execute(context =>
        {
            QueueJob entity = new()
            {
                Priority = job.Priority,
                Queue = job.Queue,
                Payload = job.Payload,
                Attempts = job.Attempts,
                ReservedAt = job.ReservedAt,
                AvailableAt = job.AvailableAt,
                CreatedAt = job.CreatedAt,
                ParentJobId = job.ParentJobId,
                GroupTag = job.GroupTag,
            };
            context.QueueJobs.Add(entity);
            context.SaveChanges();
            context.ChangeTracker.Clear();
            job.Id = entity.Id;
        });
    }

    public void RemoveJob(QueueJobModel job)
    {
        Execute(context =>
        {
            QueueJob? entity = context.QueueJobs.Find(job.Id);
            if (entity == null)
            {
                entity = new()
                {
                    Id = job.Id,
                    Payload = job.Payload,
                    Queue = job.Queue,
                };
                context.QueueJobs.Attach(entity);
            }
            context.QueueJobs.Remove(entity);
            context.SaveChanges();
            context.ChangeTracker.Clear();
        });
    }

    public QueueJobModel? GetNextJob(
        string queueName,
        byte maxAttempts,
        long? currentJobId,
        DateTime now
    )
    {
        return Execute<QueueJobModel?>(context =>
        {
            if (string.IsNullOrEmpty(queueName))
            {
                QueueJob? anyJob = context
                    .QueueJobs.OrderByDescending(j => j.Priority)
                    .ThenBy(j => j.CreatedAt)
                    .ThenBy(j => j.Id)
                    .FirstOrDefault();
                return anyJob == null ? null : ToModel(anyJob);
            }

            QueueJob? job = ReserveJobQuery(context, maxAttempts, queueName, currentJobId, now);
            return job == null ? null : ToModel(job);
        });
    }

    public QueueJobModel? FindJob(int id)
    {
        return Execute<QueueJobModel?>(context =>
        {
            QueueJob? job = context.QueueJobs.Find(id);
            return job == null ? null : ToModel(job);
        });
    }

    public bool JobExists(string payload)
    {
        return Execute(context => ExistsQuery(context, payload));
    }

    public void UpdateJob(QueueJobModel job)
    {
        Execute(context =>
        {
            QueueJob? entity = context.QueueJobs.Find(job.Id);
            if (entity == null)
                return;

            entity.Priority = job.Priority;
            entity.Queue = job.Queue;
            entity.Attempts = job.Attempts;
            entity.ReservedAt = job.ReservedAt;
            entity.AvailableAt = job.AvailableAt;
            context.SaveChanges();
            context.ChangeTracker.Clear();
        });
    }

    public void UpdateJobPayload(int jobId, string newPayload, DateTime availableAt)
    {
        Execute(context =>
        {
            QueueJob? entity = context.QueueJobs.Find(jobId);
            if (entity == null)
                return;

            entity.Payload = newPayload;
            entity.ReservedAt = null;
            entity.AvailableAt = availableAt;
            entity.Attempts = 0;
            context.SaveChanges();
            context.ChangeTracker.Clear();
        });
    }

    public void ResetAllReservedJobs()
    {
        Execute(context =>
        {
            foreach (QueueJob job in context.QueueJobs)
            {
                job.ReservedAt = null;
            }
            context.SaveChanges();
            context.ChangeTracker.Clear();
        });
    }

    public IReadOnlyList<QueueJobModel> GetReservedJobsOlderThan(DateTime cutoffUtc)
    {
        return Execute(context =>
        {
            List<QueueJob> rows = context
                .QueueJobs.AsNoTracking()
                .Where(j => j.ReservedAt != null && j.ReservedAt < cutoffUtc)
                .ToList();
            return rows.Select(ToModel).ToList();
        });
    }

    public IReadOnlyList<QueueJobModel> GetStrandedJobs(byte maxAttempts)
    {
        return Execute(context =>
        {
            List<QueueJob> rows = context
                .QueueJobs.AsNoTracking()
                .Where(j => j.ReservedAt == null && j.Attempts >= maxAttempts)
                .ToList();
            return rows.Select(ToModel).ToList();
        });
    }

    public void AddFailedJob(FailedJobModel failedJob)
    {
        Execute(context =>
        {
            FailedJob entity = new()
            {
                Uuid = failedJob.Uuid,
                Connection = failedJob.Connection,
                Queue = failedJob.Queue,
                Payload = failedJob.Payload,
                Exception = failedJob.Exception,
                FailedAt = failedJob.FailedAt,
                ParentJobId = failedJob.ParentJobId,
            };
            context.FailedJobs.Add(entity);
            context.SaveChanges();
            context.ChangeTracker.Clear();
        });
    }

    public void RemoveFailedJob(FailedJobModel failedJob)
    {
        Execute(context =>
        {
            FailedJob? entity = context.FailedJobs.Find(failedJob.Id);
            if (entity != null)
            {
                context.FailedJobs.Remove(entity);
                context.SaveChanges();
                context.ChangeTracker.Clear();
            }
        });
    }

    public void AddFailedJobAndRemoveJob(FailedJobModel failedJob, QueueJobModel job)
    {
        Execute(context =>
        {
            FailedJob failedEntity = new()
            {
                Uuid = failedJob.Uuid,
                Connection = failedJob.Connection,
                Queue = failedJob.Queue,
                Payload = failedJob.Payload,
                Exception = failedJob.Exception,
                FailedAt = failedJob.FailedAt,
                ParentJobId = failedJob.ParentJobId,
            };
            context.FailedJobs.Add(failedEntity);

            QueueJob? jobEntity = context.QueueJobs.Find(job.Id);
            if (jobEntity == null)
            {
                jobEntity = new()
                {
                    Id = job.Id,
                    Payload = job.Payload,
                    Queue = job.Queue,
                };
                context.QueueJobs.Attach(jobEntity);
            }
            context.QueueJobs.Remove(jobEntity);

            context.SaveChanges();
            context.ChangeTracker.Clear();
            failedJob.Id = failedEntity.Id;
        });
    }

    public FailedJobModel? FindFailedJob(int id)
    {
        return Execute<FailedJobModel?>(context =>
        {
            FailedJob? entity = context.FailedJobs.Find((long)id);
            return entity == null ? null : ToFailedModel(entity);
        });
    }

    public IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null)
    {
        return Execute(context =>
        {
            IQueryable<FailedJob> query = context.FailedJobs;
            if (failedJobId.HasValue)
                query = query.Where(j => j.Id == failedJobId.Value);

            return (IReadOnlyList<FailedJobModel>)
                query
                    .Select(j => new FailedJobModel
                    {
                        Id = j.Id,
                        Uuid = j.Uuid,
                        Connection = j.Connection,
                        Queue = j.Queue,
                        Payload = j.Payload,
                        Exception = j.Exception,
                        FailedAt = j.FailedAt,
                        ParentJobId = j.ParentJobId,
                    })
                    .ToList();
        });
    }

    public IReadOnlyList<CronJobModel> GetEnabledCronJobs()
    {
        return Execute(context =>
            (IReadOnlyList<CronJobModel>)
                context
                    .CronJobs.Where(c => c.IsEnabled)
                    .Select(c => new CronJobModel
                    {
                        Id = c.Id,
                        Name = c.Name,
                        CronExpression = c.CronExpression,
                        JobType = c.JobType,
                        Parameters = c.Parameters,
                        IsEnabled = c.IsEnabled,
                        LastRun = c.LastRun,
                        NextRun = c.NextRun,
                    })
                    .ToList()
        );
    }

    public CronJobModel? FindCronJobByName(string name)
    {
        return Execute<CronJobModel?>(context =>
        {
            CronJob? entity = context.CronJobs.FirstOrDefault(c => c.Name == name);
            return entity == null ? null : ToCronModel(entity);
        });
    }

    public void AddCronJob(CronJobModel cronJob)
    {
        Execute(context =>
        {
            CronJob entity = new()
            {
                Name = cronJob.Name,
                CronExpression = cronJob.CronExpression,
                JobType = cronJob.JobType,
                Parameters = cronJob.Parameters,
                IsEnabled = cronJob.IsEnabled,
                LastRun = cronJob.LastRun,
                NextRun = cronJob.NextRun,
            };
            context.CronJobs.Add(entity);
            context.SaveChanges();
            context.ChangeTracker.Clear();
        });
    }

    public void UpdateCronJob(CronJobModel cronJob)
    {
        Execute(context =>
        {
            CronJob? entity = context.CronJobs.Find(cronJob.Id);
            if (entity == null)
                return;

            entity.CronExpression = cronJob.CronExpression;
            entity.IsEnabled = cronJob.IsEnabled;
            entity.LastRun = cronJob.LastRun;
            entity.NextRun = cronJob.NextRun;
            context.SaveChanges();
            context.ChangeTracker.Clear();
        });
    }

    public void RemoveCronJob(CronJobModel cronJob)
    {
        Execute(context =>
        {
            CronJob? entity = context.CronJobs.Find(cronJob.Id);
            if (entity != null)
            {
                context.CronJobs.Remove(entity);
                context.SaveChanges();
                context.ChangeTracker.Clear();
            }
        });
    }

    // No-op: this adapter persists eagerly inside each Execute(...) unit of work
    // (every mutating method opens a scoped DbContext and calls SaveChanges before
    // returning), so there is no deferred change set to flush here. The method is
    // retained to satisfy IQueueContext, whose callers invoke SaveChanges() after
    // mutations assuming deferred persistence.
    public void SaveChanges() { }

    public void Dispose() { }

    public bool IsParentFailed(int parentJobId)
    {
        return Execute(context =>
        {
            return context.FailedJobs.AsNoTracking().Any(f => f.ParentJobId == parentJobId);
        });
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
            CreatedAt = entity.CreatedAt,
            ParentJobId = entity.ParentJobId,
            GroupTag = entity.GroupTag,
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
            FailedAt = entity.FailedAt,
            ParentJobId = entity.ParentJobId,
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
            NextRun = entity.NextRun,
        };
    }
}
