namespace NoMercy.Encoder.V3.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Execution;

public record ExecuteInput(FfmpegCommand[] Commands, TimeSpan InputDuration);

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

        foreach (FfmpegCommand cmd in input.Commands)
        {
            ExecutionResult result = await executor.ExecuteAsync(
                cmd,
                input.InputDuration,
                correlationId: context.CorrelationId,
                ct: ct
            );

            results.Add(result);

            if (!result.Success)
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
        }

        return new StageSuccess<ExecutionResult[]>(results.ToArray());
    }
}
