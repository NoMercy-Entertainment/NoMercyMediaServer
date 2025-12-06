namespace NoMercy.Api.Controllers.V1.Encoder.Services;

/// <summary>
/// Service for executing encoding jobs
/// Dispatches jobs to the local queue system for processing
/// </summary>
public interface IJobExecutorService
{
    Task<JobExecutionResult> ExecuteJobAsync(EncodingJobRequest request, CancellationToken cancellationToken);
}

public class EncodingJobRequest
{
    public string JobId { get; set; } = string.Empty;
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string FfmpegCommand { get; set; } = string.Empty;
}

public class JobExecutionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

