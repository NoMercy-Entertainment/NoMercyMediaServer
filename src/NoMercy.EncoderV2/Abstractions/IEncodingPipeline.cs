namespace NoMercy.EncoderV2.Abstractions;

/// <summary>
/// Represents an encoding job
/// </summary>
public sealed class EncodingJob
{
    /// <summary>
    /// Unique job identifier
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Source media file path
    /// </summary>
    public required string InputPath { get; init; }

    /// <summary>
    /// Output directory or file path
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// The encoding profile to use
    /// </summary>
    public required IEncodingProfile Profile { get; init; }

    /// <summary>
    /// Additional metadata for the job
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Priority (higher = more important)
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// When the job was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Status of an encoding job
/// </summary>
public sealed class EncodingJobStatus
{
    public required string JobId { get; init; }
    public JobState State { get; init; }
    public double Progress { get; init; }
    public string? CurrentStep { get; init; }
    public TimeSpan Elapsed { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<string> CompletedOutputs { get; init; } = [];
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

public enum JobState
{
    Pending,
    Analyzing,
    Encoding,
    Processing,
    Completed,
    Failed,
    Cancelled,
    Paused
}

/// <summary>
/// Result of a completed encoding job
/// </summary>
public sealed class EncodingJobResult
{
    public required string JobId { get; init; }
    public bool Success { get; init; }
    public IReadOnlyList<OutputFile> OutputFiles { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public TimeSpan TotalDuration { get; init; }
    public MediaInfo? SourceInfo { get; init; }
}

/// <summary>
/// Information about a generated output file
/// </summary>
public sealed class OutputFile
{
    public required string Path { get; init; }
    public required string Type { get; init; } // "video", "audio", "subtitle", "thumbnail", "playlist"
    public long Size { get; init; }
    public string? Resolution { get; init; }
    public string? Codec { get; init; }
    public long? Bitrate { get; init; }
}

/// <summary>
/// Main encoding pipeline interface
/// </summary>
public interface IEncodingPipeline
{
    /// <summary>
    /// Submits a job for encoding
    /// </summary>
    Task<string> SubmitJobAsync(EncodingJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a job
    /// </summary>
    Task<EncodingJobStatus?> GetJobStatusAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for a job to complete
    /// </summary>
    Task<EncodingJobResult> WaitForCompletionAsync(
        string jobId,
        IProgress<EncodingJobStatus>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a pending or running job
    /// </summary>
    Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses a running job
    /// </summary>
    Task<bool> PauseJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused job
    /// </summary>
    Task<bool> ResumeJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active jobs
    /// </summary>
    Task<IReadOnlyList<EncodingJobStatus>> GetActiveJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when job progress changes
    /// </summary>
    event EventHandler<EncodingJobStatus>? JobProgressChanged;

    /// <summary>
    /// Event raised when a job completes
    /// </summary>
    event EventHandler<EncodingJobResult>? JobCompleted;
}

/// <summary>
/// Simplified encoding interface for common use cases
/// </summary>
public interface IEncoder
{
    /// <summary>
    /// Encodes a file using the specified profile
    /// </summary>
    Task<EncodingJobResult> EncodeAsync(
        string inputPath,
        string outputPath,
        string profileId,
        IProgress<EncodingProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encodes a file using a custom profile
    /// </summary>
    Task<EncodingJobResult> EncodeAsync(
        string inputPath,
        string outputPath,
        IEncodingProfile profile,
        IProgress<EncodingProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a media file
    /// </summary>
    Task<MediaInfo> AnalyzeAsync(string inputPath, CancellationToken cancellationToken = default);
}
