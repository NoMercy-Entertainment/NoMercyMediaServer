namespace NoMercy.Encoder.V3.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Composition;
using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Output;

public record BuildInput(ExecutionPlan Plan, string InputPath, string OutputDirectory);

public class BuildStage(EncoderOptions options, ILogger<BuildStage> logger)
    : IPipelineStage<BuildInput, FfmpegCommand[]>
{
    public string Name => "Build";

    public Task<StageResult> ExecuteAsync(
        BuildInput input,
        EncodingContext context,
        CancellationToken ct
    )
    {
        logger.LogInformation("[{CorrelationId}] Building FFmpeg commands", context.CorrelationId);

        try
        {
            IOutputStrategy strategy = GetStrategy(input.Plan.OutputPlan.Format);
            FfmpegCommandBuilder builder = new();
            builder.AddInput(new InputOptions(input.InputPath));

            strategy.ConfigureOutput(builder, input.Plan.OutputPlan, input.OutputDirectory);

            FfmpegCommand cmd = builder.Build(options.FfmpegPath, input.OutputDirectory);

            logger.LogDebug(
                "[{CorrelationId}] Built command: {Args}",
                context.CorrelationId,
                string.Join(" ", cmd.Arguments)
            );

            return Task.FromResult<StageResult>(new StageSuccess<FfmpegCommand[]>([cmd]));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult<StageResult>(
                new StageFailure(
                    new EncodingError(
                        EncodingErrorKind.Unknown,
                        $"Command build failed: {ex.Message}",
                        null,
                        Name,
                        false
                    )
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
