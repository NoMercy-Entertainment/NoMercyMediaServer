using NoMercy.Queue.Core.Models;

namespace NoMercy.Queue.Core.Interfaces;

public interface IQueueContext : IDisposable
{
    void AddJob(QueueJobModel job);
    void RemoveJob(QueueJobModel job);
    QueueJobModel? GetNextJob(string queueName, byte maxAttempts, long? currentJobId);
    QueueJobModel? FindJob(int id);
    bool JobExists(string payload);
    void UpdateJob(QueueJobModel job);

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
