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
        queryExpression: (
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
        queryExpression: (QueueContext queueContext, string payloadString) =>
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
            return operation(arg: context);
        }
        finally
        {
            ReleaseContext(context: context);
        }
    }

    private void Execute(Action<QueueContext> operation)
    {
        QueueContext context = AcquireContext();
        try
        {
            operation(obj: context);
        }
        finally
        {
            ReleaseContext(context: context);
        }
    }

    public void AddJob(QueueJobModel job)
    {
        Execute(operation: context =>
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
            context.QueueJobs.Add(entity: entity);
            context.SaveChanges();
            context.ChangeTracker.Clear();
            job.Id = entity.Id;
        });
    }

    public void RemoveJob(QueueJobModel job)
    {
        Execute(operation: context =>
        {
            QueueJob? entity = context.QueueJobs.Find(keyValues: job.Id);
            if (entity == null)
            {
                entity = new()
                {
                    Id = job.Id,
                    Payload = job.Payload,
                    Queue = job.Queue,
                };
                context.QueueJobs.Attach(entity: entity);
            }
            context.QueueJobs.Remove(entity: entity);
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
        return Execute<QueueJobModel?>(operation: context =>
        {
            if (string.IsNullOrEmpty(value: queueName))
            {
                QueueJob? anyJob = context
                    .QueueJobs.OrderByDescending(keySelector: j => j.Priority)
                    .ThenBy(keySelector: j => j.CreatedAt)
                    .ThenBy(keySelector: j => j.Id)
                    .FirstOrDefault();
                return anyJob == null ? null : ToModel(entity: anyJob);
            }

            QueueJob? job = ReserveJobQuery(arg1: context, arg2: maxAttempts, arg3: queueName, arg4: currentJobId, arg5: now);
            return job == null ? null : ToModel(entity: job);
        });
    }

    public QueueJobModel? FindJob(int id)
    {
        return Execute<QueueJobModel?>(operation: context =>
        {
            QueueJob? job = context.QueueJobs.Find(keyValues: id);
            return job == null ? null : ToModel(entity: job);
        });
    }

    public bool JobExists(string payload)
    {
        return Execute(operation: context => ExistsQuery(arg1: context, arg2: payload));
    }

    public void UpdateJob(QueueJobModel job)
    {
        Execute(operation: context =>
        {
            QueueJob? entity = context.QueueJobs.Find(keyValues: job.Id);
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
        Execute(operation: context =>
        {
            QueueJob? entity = context.QueueJobs.Find(keyValues: jobId);
            if (entity == null)
                return;

            entity.Payload = newPayload;
            entity.ReservedAt = null;
            entity.AvailableAt = availableAt;
            context.SaveChanges();
            context.ChangeTracker.Clear();
        });
    }

    public void ResetAllReservedJobs()
    {
        Execute(operation: context =>
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
        return Execute(operation: context =>
        {
            List<QueueJob> rows = context
                .QueueJobs.AsNoTracking()
                .Where(predicate: j => j.ReservedAt != null && j.ReservedAt < cutoffUtc)
                .ToList();
            return rows.Select(selector: ToModel).ToList();
        });
    }

    public void AddFailedJob(FailedJobModel failedJob)
    {
        Execute(operation: context =>
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
            context.FailedJobs.Add(entity: entity);
            context.SaveChanges();
            context.ChangeTracker.Clear();
        });
    }

    public void RemoveFailedJob(FailedJobModel failedJob)
    {
        Execute(operation: context =>
        {
            FailedJob? entity = context.FailedJobs.Find(keyValues: failedJob.Id);
            if (entity != null)
            {
                context.FailedJobs.Remove(entity: entity);
                context.SaveChanges();
                context.ChangeTracker.Clear();
            }
        });
    }

    public void AddFailedJobAndRemoveJob(FailedJobModel failedJob, QueueJobModel job)
    {
        Execute(operation: context =>
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
            context.FailedJobs.Add(entity: failedEntity);

            QueueJob? jobEntity = context.QueueJobs.Find(keyValues: job.Id);
            if (jobEntity == null)
            {
                jobEntity = new()
                {
                    Id = job.Id,
                    Payload = job.Payload,
                    Queue = job.Queue,
                };
                context.QueueJobs.Attach(entity: jobEntity);
            }
            context.QueueJobs.Remove(entity: jobEntity);

            context.SaveChanges();
            context.ChangeTracker.Clear();
            failedJob.Id = failedEntity.Id;
        });
    }

    public FailedJobModel? FindFailedJob(int id)
    {
        return Execute<FailedJobModel?>(operation: context =>
        {
            FailedJob? entity = context.FailedJobs.Find(keyValues: (long)id);
            return entity == null ? null : ToFailedModel(entity: entity);
        });
    }

    public IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null)
    {
        return Execute(operation: context =>
        {
            IQueryable<FailedJob> query = context.FailedJobs;
            if (failedJobId.HasValue)
                query = query.Where(predicate: j => j.Id == failedJobId.Value);

            return (IReadOnlyList<FailedJobModel>)
                query
                    .Select(selector: j => new FailedJobModel
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
        return Execute(operation: context =>
            (IReadOnlyList<CronJobModel>)
                context
                    .CronJobs.Where(predicate: c => c.IsEnabled)
                    .Select(selector: c => new CronJobModel
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
        return Execute<CronJobModel?>(operation: context =>
        {
            CronJob? entity = context.CronJobs.FirstOrDefault(predicate: c => c.Name == name);
            return entity == null ? null : ToCronModel(entity: entity);
        });
    }

    public void AddCronJob(CronJobModel cronJob)
    {
        Execute(operation: context =>
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
            context.CronJobs.Add(entity: entity);
            context.SaveChanges();
            context.ChangeTracker.Clear();
        });
    }

    public void UpdateCronJob(CronJobModel cronJob)
    {
        Execute(operation: context =>
        {
            CronJob? entity = context.CronJobs.Find(keyValues: cronJob.Id);
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
        Execute(operation: context =>
        {
            CronJob? entity = context.CronJobs.Find(keyValues: cronJob.Id);
            if (entity != null)
            {
                context.CronJobs.Remove(entity: entity);
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
        return Execute(operation: context =>
        {
            return context.FailedJobs.AsNoTracking().Any(predicate: f => f.ParentJobId == parentJobId);
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
