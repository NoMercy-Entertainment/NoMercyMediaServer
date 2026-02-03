namespace NoMercy.EncoderV2.FFmpeg;

/// <summary>
/// Result of an FFmpeg execution
/// </summary>
public class FFmpegExecutionResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; }
    public string? ErrorMessage { get; set; }

    public static FFmpegExecutionResult Failure(string errorMessage)
    {
        return new FFmpegExecutionResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ExitCode = -1
        };
    }
}

/// <summary>
/// Service for executing FFmpeg commands
/// </summary>
public interface IFFmpegService
{
    /// <summary>
    /// Execute an FFmpeg command asynchronously
    /// </summary>
    Task<FFmpegExecutionResult> ExecuteAsync(
        string command,
        string workingDirectory,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the path to the FFmpeg executable
    /// </summary>
    string GetFFmpegPath();

    /// <summary>
    /// Verify FFmpeg is available and working
    /// </summary>
    Task<bool> VerifyFFmpegAsync();
}
