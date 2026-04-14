namespace NoMercy.Encoder.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Progress;

public record ExecuteInput(
    FfmpegCommand[] Commands,
    TimeSpan InputDuration,
    IProgressObserver? Progress = null
);

public class ExecuteStage(IFfmpegExecutor executor, ILogger<ExecuteStage> logger)
    : IPipelineStage<ExecuteInput, ExecutionResult[]>
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

        for (int i = 0; i < input.Commands.Length; i++)
        {
            FfmpegCommand cmd = input.Commands[i];

            // Only the main encode command (index 0) reports progress.
            // Post-processing commands (subtitles, fonts) are short-lived.
            Action<EncodingProgress>? onProgress =
                i == 0 && input.Progress is not null ? p => input.Progress.OnProgress(p) : null;

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
                    return new StageFailure(
                        result.Error
                            ?? new EncodingError(
                                EncodingErrorKind.ProcessCrashed,
                                "FFmpeg exited with non-zero code",
                                result.StdErr,
                                Name,
                                true
                            )
                    );
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
}
