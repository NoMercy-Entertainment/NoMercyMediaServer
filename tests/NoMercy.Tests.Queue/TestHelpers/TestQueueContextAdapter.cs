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
        Jobs.Add(job);
    }

    public void RemoveJob(QueueJobModel job)
    {
        Jobs.RemoveAll(j => j.Id == job.Id);
    }

    public QueueJobModel? GetNextJob(
        string queueName,
        byte maxAttempts,
        long? currentJobId,
        DateTime now
    )
    {
        return Jobs.Where(j =>
                j.ReservedAt == null && j.Attempts < maxAttempts && j.AvailableAt <= now
            )
            .Where(j => currentJobId == null)
            .Where(j => string.IsNullOrEmpty(queueName) || j.Queue == queueName)
            .OrderByDescending(j => j.Priority)
            .FirstOrDefault();
    }

    public QueueJobModel? FindJob(int id)
    {
        return Jobs.FirstOrDefault(j => j.Id == id);
    }

    public bool JobExists(string payload)
    {
        return Jobs.Any(j => j.Payload == payload);
    }

    public void UpdateJob(QueueJobModel job)
    {
        int index = Jobs.FindIndex(j => j.Id == job.Id);
        if (index >= 0)
            Jobs[index] = job;
    }

    public void UpdateJobPayload(int jobId, string newPayload, DateTime availableAt)
    {
        QueueJobModel? job = Jobs.FirstOrDefault(j => j.Id == jobId);
        if (job is null)
            return;
        job.Payload = newPayload;
        job.AvailableAt = availableAt;
        job.ReservedAt = null;
        job.Attempts = 0;
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
        return Jobs.Where(j => j.ReservedAt != null && j.ReservedAt < cutoffUtc).ToList();
    }

    public IReadOnlyList<QueueJobModel> GetStrandedJobs(byte maxAttempts)
    {
        return Jobs.Where(j => j.ReservedAt == null && j.Attempts >= maxAttempts).ToList();
    }

    public void AddFailedJob(FailedJobModel failedJob)
    {
        failedJob.Id = _nextFailedJobId++;
        FailedJobs.Add(failedJob);
    }

    public void RemoveFailedJob(FailedJobModel failedJob)
    {
        FailedJobs.RemoveAll(j => j.Id == failedJob.Id);
    }

    public void AddFailedJobAndRemoveJob(FailedJobModel failedJob, QueueJobModel job)
    {
        AddFailedJob(failedJob);
        RemoveJob(job);
    }

    public FailedJobModel? FindFailedJob(int id)
    {
        return FailedJobs.FirstOrDefault(j => j.Id == id);
    }

    public IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null)
    {
        if (failedJobId.HasValue)
            return FailedJobs.Where(j => j.Id == failedJobId.Value).ToList();
        return FailedJobs;
    }

    public IReadOnlyList<CronJobModel> GetEnabledCronJobs()
    {
        return CronJobs.Where(c => c.IsEnabled).ToList();
    }

    public CronJobModel? FindCronJobByName(string name)
    {
        return CronJobs.FirstOrDefault(c => c.Name == name);
    }

    public void AddCronJob(CronJobModel cronJob)
    {
        CronJobs.Add(cronJob);
    }

    public void UpdateCronJob(CronJobModel cronJob)
    {
        int index = CronJobs.FindIndex(c => c.Id == cronJob.Id);
        if (index >= 0)
            CronJobs[index] = cronJob;
    }

    public void RemoveCronJob(CronJobModel cronJob)
    {
        CronJobs.RemoveAll(c => c.Id == cronJob.Id);
    }

    public bool IsParentFailed(int parentJobId) =>
        FailedJobs.Any(f => f.Payload.Contains($"\"Id\":{parentJobId}"));

    public void SaveChanges() { }

    public void Dispose() { }
}
