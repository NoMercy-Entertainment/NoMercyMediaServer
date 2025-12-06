
namespace NoMercy.Api.Controllers.V1.Encoder.Services;

/// <summary>
/// Service for reporting job progress and completion status
/// Tracks job execution and status updates
/// </summary>
public interface IJobReportingService
{
    Task<bool> ReportJobProgressAsync(string jobId, string nodeId, int progressPercent, CancellationToken cancellationToken);
    Task<bool> ReportJobCompletionAsync(string jobId, string nodeId, JobCompletionStatus status, CancellationToken cancellationToken);
}

public class JobCompletionStatus
{
    public string Status { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}

