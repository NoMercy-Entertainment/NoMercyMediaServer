namespace NoMercy.EncoderV2.Abstractions;

/// <summary>
/// Represents the result of an FFmpeg execution
/// </summary>
public sealed class FFmpegResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public Exception? Exception { get; init; }
}

/// <summary>
/// Progress information during encoding
/// </summary>
public sealed class EncodingProgress
{
    public double Percentage { get; init; }
    public TimeSpan Elapsed { get; init; }
    public TimeSpan? Estimated { get; init; }
    public double? Fps { get; init; }
    public double? Bitrate { get; init; }
    public long? Frame { get; init; }
    public string? Speed { get; init; }
}

/// <summary>
/// Abstraction for FFmpeg process execution
/// </summary>
public interface IFFmpegExecutor
{
    /// <summary>
    /// Executes FFmpeg with the given arguments
    /// </summary>
    Task<FFmpegResult> ExecuteAsync(
        string arguments,
        string? workingDirectory = null,
        IProgress<EncodingProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes FFmpeg and captures output without progress reporting
    /// </summary>
    Task<FFmpegResult> ExecuteSilentAsync(
        string arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses a running encoding process
    /// </summary>
    Task<bool> PauseAsync(int processId);

    /// <summary>
    /// Resumes a paused encoding process
    /// </summary>
    Task<bool> ResumeAsync(int processId);

    /// <summary>
    /// Cancels a running encoding process
    /// </summary>
    Task<bool> CancelAsync(int processId);

    /// <summary>
    /// Gets the path to the FFmpeg executable
    /// </summary>
    string FFmpegPath { get; }

    /// <summary>
    /// Gets the path to the FFprobe executable
    /// </summary>
    string FFprobePath { get; }
}
