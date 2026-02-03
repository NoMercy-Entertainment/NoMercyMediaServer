using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Execution;

/// <summary>
/// Executes encoding jobs end-to-end
/// Orchestrates analysis, task splitting, FFmpeg execution, and progress tracking
/// </summary>
public interface IEncodingJobExecutor
{
    Task<EncodingJob> CreateJobAsync(string inputFile, string outputFolder, Ulid profileId);
    Task<bool> ExecuteJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task CancelJobAsync(string jobId);
    Task<EncodingJobStatus> GetJobStatusAsync(string jobId);
}

/// <summary>
/// Status information for an encoding job
/// </summary>
public class EncodingJobStatus
{
    public string JobId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public double OverallProgress { get; set; }
    public TimeSpan EstimatedRemaining { get; set; }
    public List<EncodingTaskStatus> Tasks { get; set; } = [];
}

/// <summary>
/// Status information for an encoding task
/// </summary>
public class EncodingTaskStatus
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public double Progress { get; set; }
    public string? AssignedNodeName { get; set; }
}
