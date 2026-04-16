namespace NoMercy.Encoder.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Output;

public record FinalizeInput(
    ExecutionResult[] Results,
    OutputPlan Plan,
    string OutputDirectory,
    string MediaTitle
);

public record FinalizeOutput(string OutputPath, long OutputSizeBytes);

public class FinalizeStage(
    IChapterWriter chapterWriter,
    IFontExtractor fontExtractor,
    ILogger<FinalizeStage> logger
) : IPipelineStage<FinalizeInput, FinalizeOutput>
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
            await strategy.FinalizeAsync(input.OutputDirectory, input.Plan, input.MediaTitle, ct);

            // Write chapters.vtt from MediaInfo
            if (context.MediaInfo is not null && context.MediaInfo.Chapters.Count > 0)
            {
                await chapterWriter.WriteChaptersAsync(
                    input.OutputDirectory,
                    context.MediaInfo.Chapters,
                    ct
                );

                logger.LogDebug(
                    "[{CorrelationId}] Wrote {Count} chapters to chapters.vtt",
                    context.CorrelationId,
                    context.MediaInfo.Chapters.Count
                );
            }

            // Write fonts.json manifest from previously extracted fonts
            await fontExtractor.WriteFontManifestAsync(input.OutputDirectory, ct);

            // Thumbnail sprite + VTT are produced by the spritevtt muxer
            // in the main FFmpeg command — no post-processing needed.

            long totalSize = Directory
                .GetFiles(input.OutputDirectory, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);

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
