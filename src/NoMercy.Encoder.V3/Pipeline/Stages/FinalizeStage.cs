namespace NoMercy.Encoder.V3.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Execution;
using NoMercy.Encoder.V3.Output;

public record FinalizeInput(ExecutionResult[] Results, OutputPlan Plan, string OutputDirectory);

public record FinalizeOutput(string OutputPath, long OutputSizeBytes);

public class FinalizeStage(ILogger<FinalizeStage> logger)
    : IPipelineStage<FinalizeInput, FinalizeOutput>
{
    public string Name => "Finalize";

    public async Task<StageResult> ExecuteAsync(
        FinalizeInput input,
        EncodingContext context,
        CancellationToken ct
    )
    {
        logger.LogInformation("[{CorrelationId}] Finalizing output", context.CorrelationId);

        try
        {
            Directory.CreateDirectory(input.OutputDirectory);

            IOutputStrategy strategy = GetStrategy(input.Plan.Format);
            await strategy.FinalizeAsync(input.OutputDirectory, input.Plan, ct);

            long totalSize = input.Results.Sum(_ => 0L);

            return new StageSuccess<FinalizeOutput>(
                new FinalizeOutput(input.OutputDirectory, totalSize)
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StageFailure(
                new EncodingError(
                    EncodingErrorKind.Unknown,
                    $"Finalization failed: {ex.Message}",
                    null,
                    Name,
                    false
                )
            );
        }
    }

    private static IOutputStrategy GetStrategy(OutputFormat format) =>
        format switch
        {
            OutputFormat.Hls => new HlsOutputStrategy(),
            OutputFormat.Mkv => new MkvOutputStrategy(),
            OutputFormat.Mp4 => new Mp4OutputStrategy(),
            OutputFormat.Dash => new DashOutputStrategy(),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
}
