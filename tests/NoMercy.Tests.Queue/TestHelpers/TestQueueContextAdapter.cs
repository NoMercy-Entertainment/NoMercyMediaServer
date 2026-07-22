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

using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;

namespace NoMercy.Tests.Queue.TestHelpers;

public class TestQueueContextAdapter : IQueueContext
{
    public List<QueueJobModel> Jobs { get; } = [];
    public List<FailedJobModel> FailedJobs { get; } = [];
    public List<CronJobModel> CronJobs { get; } = [];

    private int _nextJobId = 1;
    private int _nextFailedJobId = 1;

    public void AddJob(QueueJobModel job)
    {
        job.Id = _nextJobId++;
        Jobs.Add(item: job);
    }

    public void RemoveJob(QueueJobModel job)
    {
        Jobs.RemoveAll(match: j => j.Id == job.Id);
    }

    public QueueJobModel? GetNextJob(
        string queueName,
        byte maxAttempts,
        long? currentJobId,
        DateTime now
    )
    {
        return Jobs.Where(predicate: j =>
                j.ReservedAt == null && j.Attempts < maxAttempts && j.AvailableAt <= now
            )
            .Where(predicate: j => currentJobId == null)
            .Where(predicate: j => string.IsNullOrEmpty(value: queueName) || j.Queue == queueName)
            .OrderByDescending(keySelector: j => j.Priority)
            .FirstOrDefault();
    }

    public QueueJobModel? FindJob(int id)
    {
        return Jobs.FirstOrDefault(predicate: j => j.Id == id);
    }

    public bool JobExists(string payload)
    {
        return Jobs.Any(predicate: j => j.Payload == payload);
    }

    public void UpdateJob(QueueJobModel job)
    {
        int index = Jobs.FindIndex(match: j => j.Id == job.Id);
        if (index >= 0)
            Jobs[index: index] = job;
    }

    public void UpdateJobPayload(int jobId, string newPayload, DateTime availableAt)
    {
        QueueJobModel? job = Jobs.FirstOrDefault(predicate: j => j.Id == jobId);
        if (job is null)
            return;
        job.Payload = newPayload;
        job.AvailableAt = availableAt;
        job.ReservedAt = null;
    }

    public void ResetAllReservedJobs()
    {
        foreach (QueueJobModel job in Jobs)
        {
            job.ReservedAt = null;
        }
    }

    public IReadOnlyList<QueueJobModel> GetReservedJobsOlderThan(DateTime cutoffUtc)
    {
        return Jobs.Where(predicate: j => j.ReservedAt != null && j.ReservedAt < cutoffUtc).ToList();
    }

    public void AddFailedJob(FailedJobModel failedJob)
    {
        failedJob.Id = _nextFailedJobId++;
        FailedJobs.Add(item: failedJob);
    }

    public void RemoveFailedJob(FailedJobModel failedJob)
    {
        FailedJobs.RemoveAll(match: j => j.Id == failedJob.Id);
    }

    public void AddFailedJobAndRemoveJob(FailedJobModel failedJob, QueueJobModel job)
    {
        AddFailedJob(failedJob: failedJob);
        RemoveJob(job: job);
    }

    public FailedJobModel? FindFailedJob(int id)
    {
        return FailedJobs.FirstOrDefault(predicate: j => j.Id == id);
    }

    public IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null)
    {
        if (failedJobId.HasValue)
            return FailedJobs.Where(predicate: j => j.Id == failedJobId.Value).ToList();
        return FailedJobs;
    }

    public IReadOnlyList<CronJobModel> GetEnabledCronJobs()
    {
        return CronJobs.Where(predicate: c => c.IsEnabled).ToList();
    }

    public CronJobModel? FindCronJobByName(string name)
    {
        return CronJobs.FirstOrDefault(predicate: c => c.Name == name);
    }

    public void AddCronJob(CronJobModel cronJob)
    {
        CronJobs.Add(item: cronJob);
    }

    public void UpdateCronJob(CronJobModel cronJob)
    {
        int index = CronJobs.FindIndex(match: c => c.Id == cronJob.Id);
        if (index >= 0)
            CronJobs[index: index] = cronJob;
    }

    public void RemoveCronJob(CronJobModel cronJob)
    {
        CronJobs.RemoveAll(match: c => c.Id == cronJob.Id);
    }

    public bool IsParentFailed(int parentJobId) =>
        FailedJobs.Any(predicate: f => f.Payload.Contains(value: $"\"Id\":{parentJobId}"));

    public void SaveChanges() { }

    public void Dispose() { }
}
