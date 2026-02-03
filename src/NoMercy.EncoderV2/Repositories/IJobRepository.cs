using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Repositories;

/// <summary>
/// Repository for encoding job CRUD operations
/// </summary>
public interface IJobRepository
{
    Task<EncodingJob?> GetJobAsync(string jobId);
    Task<List<EncodingJob>> GetJobsByStateAsync(string state);
    Task<List<EncodingJob>> GetActiveJobsAsync();
    Task<List<EncodingJob>> ListJobsAsync(string? state = null, int limit = 50, int offset = 0);
    Task<int> GetJobCountAsync(string? state = null);
    Task<EncodingJob> CreateJobAsync(EncodingJob job);
    Task UpdateJobAsync(EncodingJob job);
    Task DeleteJobAsync(string jobId);

    Task<EncodingTask?> GetTaskAsync(string taskId);
    Task<List<EncodingTask>> GetTasksByJobAsync(string jobId);
    Task<List<EncodingTask>> GetPendingTasksAsync();
    Task<EncodingTask> CreateTaskAsync(EncodingTask task);
    Task UpdateTaskAsync(EncodingTask task);

    Task<EncodingProgress> AddProgressAsync(EncodingProgress progress);
    Task<List<EncodingProgress>> GetTaskProgressAsync(string taskId, int limit = 100);
}
