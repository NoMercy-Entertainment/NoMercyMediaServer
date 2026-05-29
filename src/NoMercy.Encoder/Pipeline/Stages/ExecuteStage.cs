using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Pipeline.Stages;

public record ExecuteInput(
    FfmpegCommand[] Commands,
    TimeSpan InputDuration,
    IProgressObserver? Progress = null
);

public class ExecuteStage(
    IFfmpegExecutor executor,
    ICheckpointStore checkpointStore,
    ILogger<ExecuteStage> logger
) : IPipelineStage<ExecuteInput, ExecutionResult[]>, IExecutionStage
{
    public string Name => "Execute";

    public async Task<StageResult> ExecuteAsync(
        ExecuteInput input,
        EncodingContext context,
        CancellationToken ct
    )
    {
        logger.LogInformation(
            "[{CorrelationId}] Executing {Count} command(s)",
            context.CorrelationId,
            input.Commands.Length
        );

        List<ExecutionResult> results = [];
        long lastProgressMs = 0;

        for (int i = 0; i < input.Commands.Length; i++)
        {
            FfmpegCommand cmd = input.Commands[i];

            // Only the main encode command (index 0) reports progress.
            // Post-processing commands (subtitles, fonts) are short-lived.
            Action<EncodingProgress>? onProgress = null;
            if (i == 0 && input.Progress is not null)
            {
                onProgress = progress =>
                {
                    lastProgressMs = (long)(progress.CurrentTimeSeconds * 1000);
                    input.Progress.OnProgress(progress);
                };
            }
            else if (i == 0)
            {
                onProgress = progress =>
                {
                    lastProgressMs = (long)(progress.CurrentTimeSeconds * 1000);
                };
            }

            ExecutionResult result = await executor.ExecuteAsync(
                cmd,
                input.InputDuration,
                onProgress: onProgress,
                correlationId: context.CorrelationId,
                ct: ct
            );

            results.Add(result);

            if (!result.Success)
            {
                // Command 0 is the main encode — must succeed.
                // Subsequent commands (subtitle extraction, font extraction, thumbnails)
                // are post-processing — failures are logged but not fatal.
                if (i == 0)
                {
                    EncodingError error =
                        result.Error
                        ?? new EncodingError(
                            EncodingErrorKind.ProcessCrashed,
                            "FFmpeg exited with non-zero code",
                            result.StdErr,
                            Name,
                            true
                        );

                    await WriteCrashCheckpointAsync(context, lastProgressMs, result.StdErr, ct);

                    return new StageFailure(error);
                }

                logger.LogWarning(
                    "[{CorrelationId}] Post-process command {Index} failed (non-fatal): exit={ExitCode}",
                    context.CorrelationId,
                    i,
                    result.ExitCode
                );
            }
        }

        return new StageSuccess<ExecutionResult[]>(results.ToArray());
    }

    private async Task WriteCrashCheckpointAsync(
        EncodingContext context,
        long lastProgressMs,
        string stderrTail,
        CancellationToken ct
    )
    {
        if (string.IsNullOrEmpty(context.OutputDirectory))
            return;

        try
        {
            JobCheckpoint checkpoint = new(
                JobId: context.CorrelationId,
                InputPath: context.InputPath ?? string.Empty,
                OutputDirectory: context.OutputDirectory,
                CompletedGroupIndices: [],
                LastUpdated: DateTime.UtcNow,
                LastProgressMs: lastProgressMs,
                LastFfmpegStderrTail: TailStderr(stderrTail),
                FailedAt: DateTime.UtcNow
            );

            await checkpointStore.SaveAsync(checkpoint, ct);

            logger.LogWarning(
                "[{CorrelationId}] Crash checkpoint saved at {OutputDirectory} — LastProgressMs={Ms}",
                context.CorrelationId,
                context.OutputDirectory,
                lastProgressMs
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "[{CorrelationId}] Failed to save crash checkpoint",
                context.CorrelationId
            );
        }
    }

    private static string TailStderr(string stderr)
    {
        const int maxBytes = 16 * 1024;
        if (string.IsNullOrEmpty(stderr) || stderr.Length <= maxBytes)
            return stderr;

        return stderr[^maxBytes..];
    }
}
