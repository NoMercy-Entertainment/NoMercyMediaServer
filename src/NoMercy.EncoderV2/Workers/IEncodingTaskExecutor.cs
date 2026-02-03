using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Workers;

/// <summary>
/// Interface for executing individual encoding tasks.
/// Provides a unified way to execute any task type with progress reporting.
/// </summary>
public interface IEncodingTaskExecutor
{
    /// <summary>
    /// Execute an encoding task
    /// </summary>
    /// <param name="task">The task to execute</param>
    /// <param name="progressCallback">Callback for progress updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Output file path if applicable, null otherwise</returns>
    Task<string?> ExecuteAsync(
        EncodingTask task,
        Func<EncodingProgress, Task> progressCallback,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if this executor can handle the given task type
    /// </summary>
    bool CanHandle(string taskType);
}
