namespace NoMercy.EncoderV2.Progress;

/// <summary>
/// Progress information from FFmpeg encoding
/// </summary>
public class EncodingProgressInfo
{
    public string TaskId { get; set; } = string.Empty;
    public double ProgressPercentage { get; set; }
    public long CurrentFrame { get; set; }
    public double Fps { get; set; }
    public double Speed { get; set; }
    public string Bitrate { get; set; } = string.Empty;
    public TimeSpan CurrentTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan EstimatedRemaining { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Service for monitoring and reporting encoding progress
/// </summary>
public interface IProgressMonitor
{
    /// <summary>
    /// Parse FFmpeg progress output and extract progress information
    /// </summary>
    EncodingProgressInfo? ParseProgressOutput(string output, TimeSpan totalDuration);

    /// <summary>
    /// Report progress to the database and SignalR hub
    /// </summary>
    Task ReportProgressAsync(string taskId, EncodingProgressInfo progress);

    /// <summary>
    /// Report progress with job context for SignalR routing
    /// </summary>
    Task ReportProgressAsync(string jobId, string taskId, EncodingProgressInfo progress);

    /// <summary>
    /// Get the latest progress for a task
    /// </summary>
    Task<EncodingProgressInfo?> GetLatestProgressAsync(string taskId);

    /// <summary>
    /// Report job state change to SignalR
    /// </summary>
    Task ReportJobStateChangeAsync(string jobId, string previousState, string newState, string? errorMessage = null);

    /// <summary>
    /// Report task state change to SignalR
    /// </summary>
    Task ReportTaskStateChangeAsync(string jobId, string taskId, string taskType, string previousState, string newState, string? errorMessage = null);
}
