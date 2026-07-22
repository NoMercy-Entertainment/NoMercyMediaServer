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
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Sqlite.Entities;

namespace NoMercyQueue.Sqlite;

public class SqliteQueueContext : IQueueContext
{
    private readonly QueueDbContext _context;

    internal static readonly Func<
        QueueDbContext,
        byte,
        string,
        long?,
        DateTime,
        QueueJobEntity?
    > ReserveJobQuery = EF.CompileQuery(
        queryExpression: (QueueDbContext context, byte maxAttempts, string name, long? currentJobId, DateTime now) =>
            context
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

    internal static readonly Func<QueueDbContext, string, bool> ExistsQuery = EF.CompileQuery(
        queryExpression: (QueueDbContext context, string payloadString) =>
            context.QueueJobs.Any(j => j.Payload == payloadString)
    );

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
            CreatedAt = job.CreatedAt,
            ParentJobId = job.ParentJobId,
            GroupTag = job.GroupTag,
        };
        _context.QueueJobs.Add(entity: entity);
        SaveAndClear();
        job.Id = entity.Id;
    }

    public void RemoveJob(QueueJobModel job)
    {
        QueueJobEntity? entity = _context.QueueJobs.Find(keyValues: job.Id);
        if (entity == null)
        {
            entity = new()
            {
                Id = job.Id,
                Payload = job.Payload,
                Queue = job.Queue,
            };
            _context.QueueJobs.Attach(entity: entity);
        }
        _context.QueueJobs.Remove(entity: entity);
        SaveAndClear();
    }

    public QueueJobModel? GetNextJob(
        string queueName,
        byte maxAttempts,
        long? currentJobId,
        DateTime now
    )
    {
        if (string.IsNullOrEmpty(value: queueName))
        {
            QueueJobEntity? anyJob = _context
                .QueueJobs.OrderByDescending(keySelector: j => j.Priority)
                .ThenBy(keySelector: j => j.CreatedAt)
                .ThenBy(keySelector: j => j.Id)
                .FirstOrDefault();
            return anyJob == null ? null : ToModel(entity: anyJob);
        }

        QueueJobEntity? job = ReserveJobQuery(arg1: _context, arg2: maxAttempts, arg3: queueName, arg4: currentJobId, arg5: now);
        if (job == null)
            return null;

        return ToModel(entity: job);
    }

    public QueueJobModel? FindJob(int id)
    {
        QueueJobEntity? job = _context.QueueJobs.Find(keyValues: id);
        return job == null ? null : ToModel(entity: job);
    }

    public bool JobExists(string payload)
    {
        return ExistsQuery(arg1: _context, arg2: payload);
    }

    public void UpdateJob(QueueJobModel job)
    {
        QueueJobEntity? entity = _context.QueueJobs.Find(keyValues: job.Id);
        if (entity == null)
            return;

        entity.Priority = job.Priority;
        entity.Queue = job.Queue;
        entity.Attempts = job.Attempts;
        entity.ReservedAt = job.ReservedAt;
        entity.AvailableAt = job.AvailableAt;
        SaveAndClear();
    }

    public void UpdateJobPayload(int jobId, string newPayload, DateTime availableAt)
    {
        QueueJobEntity? entity = _context.QueueJobs.Find(keyValues: jobId);
        if (entity == null)
            return;

        entity.Payload = newPayload;
        entity.ReservedAt = null;
        entity.AvailableAt = availableAt;
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

    public IReadOnlyList<QueueJobModel> GetReservedJobsOlderThan(DateTime cutoffUtc)
    {
        return _context
            .QueueJobs.AsNoTracking()
            .Where(predicate: j => j.ReservedAt != null && j.ReservedAt < cutoffUtc)
            .ToList()
            .Select(selector: e => new QueueJobModel
            {
                Id = e.Id,
                Priority = e.Priority,
                Queue = e.Queue,
                Payload = e.Payload,
                Attempts = e.Attempts,
                ReservedAt = e.ReservedAt,
                AvailableAt = e.AvailableAt,
                CreatedAt = e.CreatedAt,
                ParentJobId = e.ParentJobId,
                GroupTag = e.GroupTag,
            })
            .ToList();
    }

    public bool IsParentFailed(int parentJobId)
    {
        return _context.FailedJobs.AsNoTracking().Any(predicate: f => f.ParentJobId == parentJobId);
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
            FailedAt = failedJob.FailedAt,
            ParentJobId = failedJob.ParentJobId,
        };
        _context.FailedJobs.Add(entity: entity);
    }

    public void RemoveFailedJob(FailedJobModel failedJob)
    {
        FailedJobEntity? entity = _context.FailedJobs.Find(keyValues: failedJob.Id);
        if (entity != null)
        {
            _context.FailedJobs.Remove(entity: entity);
        }
    }

    public void AddFailedJobAndRemoveJob(FailedJobModel failedJob, QueueJobModel job)
    {
        FailedJobEntity failedEntity = new()
        {
            Uuid = failedJob.Uuid,
            Connection = failedJob.Connection,
            Queue = failedJob.Queue,
            Payload = failedJob.Payload,
            Exception = failedJob.Exception,
            FailedAt = failedJob.FailedAt,
            ParentJobId = failedJob.ParentJobId,
        };
        _context.FailedJobs.Add(entity: failedEntity);

        QueueJobEntity? jobEntity = _context.QueueJobs.Find(keyValues: job.Id);
        if (jobEntity == null)
        {
            jobEntity = new()
            {
                Id = job.Id,
                Payload = job.Payload,
                Queue = job.Queue,
            };
            _context.QueueJobs.Attach(entity: jobEntity);
        }
        _context.QueueJobs.Remove(entity: jobEntity);

        SaveAndClear();
        failedJob.Id = failedEntity.Id;
    }

    public FailedJobModel? FindFailedJob(int id)
    {
        FailedJobEntity? entity = _context.FailedJobs.Find(keyValues: (long)id);
        return entity == null ? null : ToFailedModel(entity: entity);
    }

    public IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null)
    {
        IQueryable<FailedJobEntity> query = _context.FailedJobs;
        if (failedJobId.HasValue)
            query = query.Where(predicate: j => j.Id == failedJobId.Value);

        return query
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
    }

    public IReadOnlyList<CronJobModel> GetEnabledCronJobs()
    {
        return _context
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
            .ToList();
    }

    public CronJobModel? FindCronJobByName(string name)
    {
        CronJobEntity? entity = _context.CronJobs.FirstOrDefault(predicate: c => c.Name == name);
        return entity == null ? null : ToCronModel(entity: entity);
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
            NextRun = cronJob.NextRun,
        };
        _context.CronJobs.Add(entity: entity);
        SaveAndClear();
    }

    public void UpdateCronJob(CronJobModel cronJob)
    {
        CronJobEntity? entity = _context.CronJobs.Find(keyValues: cronJob.Id);
        if (entity == null)
            return;

        entity.CronExpression = cronJob.CronExpression;
        entity.IsEnabled = cronJob.IsEnabled;
        entity.LastRun = cronJob.LastRun;
        entity.NextRun = cronJob.NextRun;
        SaveAndClear();
    }

    public void RemoveCronJob(CronJobModel cronJob)
    {
        CronJobEntity? entity = _context.CronJobs.Find(keyValues: cronJob.Id);
        if (entity != null)
        {
            _context.CronJobs.Remove(entity: entity);
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
            CreatedAt = entity.CreatedAt,
            ParentJobId = entity.ParentJobId,
            GroupTag = entity.GroupTag,
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
            FailedAt = entity.FailedAt,
            ParentJobId = entity.ParentJobId,
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
            NextRun = entity.NextRun,
        };
    }
}
