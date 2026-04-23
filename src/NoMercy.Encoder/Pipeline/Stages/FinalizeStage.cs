namespace NoMercy.Encoder.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Progress;

public record FinalizeInput(
    ExecutionResult[] Results,
    OutputPlan Plan,
    string OutputDirectory,
    string MediaTitle,
    IProgressObserver? Progress = null
);

public record FinalizeOutput(string OutputPath, long OutputSizeBytes);

public class FinalizeStage(
    IChapterWriter chapterWriter,
    IFontExtractor fontExtractor,
    IOutputStrategyFactory outputStrategyFactory,
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

            IOutputStrategy strategy = outputStrategyFactory.Resolve(input.Plan.Format);

            input.Progress?.OnStageStarted("Building Master Playlist");
            await strategy.FinalizeAsync(input.OutputDirectory, input.Plan, input.MediaTitle, ct);
            input.Progress?.OnStageCompleted("Building Master Playlist", TimeSpan.Zero);

            // Write chapters.vtt from MediaInfo
            if (context.MediaInfo is not null && context.MediaInfo.Chapters.Count > 0)
            {
                input.Progress?.OnStageStarted("Extracting chapters");
                await chapterWriter.WriteChaptersAsync(
                    input.OutputDirectory,
                    context.MediaInfo.Chapters,
                    ct
                );
                input.Progress?.OnStageCompleted("Extracting chapters", TimeSpan.Zero);

                logger.LogDebug(
                    "[{CorrelationId}] Wrote {Count} chapters to chapters.vtt",
                    context.CorrelationId,
                    context.MediaInfo.Chapters.Count
                );
            }

            // Write fonts.json manifest from previously extracted fonts
            input.Progress?.OnStageStarted("Extracting fonts");
            await fontExtractor.WriteFontManifestAsync(input.OutputDirectory, ct);
            input.Progress?.OnStageCompleted("Extracting fonts", TimeSpan.Zero);

            // Thumbnail sprite + VTT are produced by the spritevtt muxer
            // in the main FFmpeg command — no post-processing needed.

            long totalSize = Directory
                .GetFiles(input.OutputDirectory, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);

            return new StageSuccess<FinalizeOutput>(
                new(input.OutputDirectory, totalSize)
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StageFailure(
                new(
                    EncodingErrorKind.Unknown,
                    $"Finalization failed: {ex.Message}",
                    null,
                    Name,
                    false
                )
            );
        }
    }
}
