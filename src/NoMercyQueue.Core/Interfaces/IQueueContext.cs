using NoMercyQueue.Core.Models;

namespace NoMercyQueue.Core.Interfaces;

public interface IQueueContext : IDisposable
{
    void AddJob(QueueJobModel job);
    void RemoveJob(QueueJobModel job);
    QueueJobModel? GetNextJob(string queueName, byte maxAttempts, long? currentJobId);
    QueueJobModel? FindJob(int id);
    bool JobExists(string payload);
    void UpdateJob(QueueJobModel job);

    /// <summary>
    /// Replaces the payload of an existing queue job and resets its
    /// <c>ReservedAt</c> to null with the given <paramref name="availableAt"/>.
    /// Used by coordinator jobs that need to persist phase state and re-queue
    /// themselves without removing and re-inserting (which would trip the
    /// deduplication check in <see cref="JobQueue.Enqueue"/>).
    /// </summary>
    void UpdateJobPayload(int jobId, string newPayload, DateTime availableAt);

    void ResetAllReservedJobs();
    IReadOnlyList<QueueJobModel> GetReservedJobsOlderThan(DateTime cutoffUtc);

    /// <summary>
    /// Returns true when the specified parent job ID is present in the
    /// <see cref="FailedJobModel"/> table — indicating that child jobs with this
    /// <paramref name="parentJobId"/> should be skipped (marked as failed-by-parent).
    /// </summary>
    bool IsParentFailed(int parentJobId);

    void AddFailedJob(FailedJobModel failedJob);
    void RemoveFailedJob(FailedJobModel failedJob);
    FailedJobModel? FindFailedJob(int id);
    IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null);

    IReadOnlyList<CronJobModel> GetEnabledCronJobs();
    CronJobModel? FindCronJobByName(string name);
    void AddCronJob(CronJobModel cronJob);
    void UpdateCronJob(CronJobModel cronJob);
    void RemoveCronJob(CronJobModel cronJob);

    void SaveChanges();
}
