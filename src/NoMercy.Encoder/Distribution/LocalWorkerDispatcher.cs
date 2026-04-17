namespace NoMercy.Encoder.Distribution;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;

/// <summary>
/// Runs every task locally on the same machine via <see cref="IFfmpegExecutor"/>.
/// Default when no remote workers are configured — matches the single-machine
/// behavior today. Tasks run sequentially so GPU / CPU contention stays bounded;
/// the outer job queue already serializes encodes and we don't want to punch
/// through that by running multiple ffmpeg processes in parallel here.
/// </summary>
public class LocalWorkerDispatcher(IFfmpegExecutor executor, ILogger<LocalWorkerDispatcher> logger)
    : IWorkerDispatcher
{
    public int AvailableWorkerCount => 1;

    public async Task<DispatchResult[]> DispatchAsync(EncodeTask[] tasks, CancellationToken ct)
    {
        DispatchResult[] results = new DispatchResult[tasks.Length];

        for (int i = 0; i < tasks.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            EncodeTask task = tasks[i];

            logger.LogInformation(
                "Dispatching task {TaskId} ({Type}) locally → {Output}",
                task.TaskId,
                task.Type,
                task.OutputPath
            );

            try
            {
                ExecutionResult exec = await executor.ExecuteAsync(
                    task.Command,
                    task.TimeRangeDuration ?? TimeSpan.Zero,
                    null,
                    task.TaskId,
                    ct
                );

                results[i] = new DispatchResult(
                    TaskId: task.TaskId,
                    Success: exec.Success,
                    OutputPath: task.OutputPath,
                    Duration: exec.Duration,
                    Error: exec.Success ? null : ErrorMessage(exec.Error)
                );

                if (!exec.Success)
                {
                    logger.LogWarning(
                        "Local task {TaskId} failed: {Error}",
                        task.TaskId,
                        results[i].Error
                    );
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Local task {TaskId} threw", task.TaskId);
                results[i] = new DispatchResult(
                    TaskId: task.TaskId,
                    Success: false,
                    OutputPath: task.OutputPath,
                    Duration: TimeSpan.Zero,
                    Error: ex.Message
                );
            }
        }

        return results;
    }

    private static string? ErrorMessage(EncodingError? error) =>
        error is null ? null : $"{error.Kind}: {error.Message}";
}
