namespace NoMercy.Encoder.V3.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Composition;
using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Execution;
using NoMercy.Encoder.V3.Output;
using NoMercy.Encoder.V3.PostProcess;

public record FinalizeInput(ExecutionResult[] Results, OutputPlan Plan, string OutputDirectory);

public record FinalizeOutput(string OutputPath, long OutputSizeBytes);

public class FinalizeStage(
    IFfmpegExecutor executor,
    EncoderOptions options,
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
            await strategy.FinalizeAsync(input.OutputDirectory, input.Plan, ct);

            // Write chapters.vtt from MediaInfo
            if (context.MediaInfo is not null && context.MediaInfo.Chapters.Count > 0)
            {
                ChapterWriter chapterWriter = new();
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
            FontExtractor fontExtractor = new();
            await fontExtractor.WriteFontManifestAsync(input.OutputDirectory, ct);

            // Assemble thumbnail sprite sheet + write VTT cue file
            if (input.Plan.Thumbnails is not null && context.MediaInfo is not null)
            {
                ThumbnailGenerator thumbGen = new();
                int imageCount = (int)(
                    context.MediaInfo.Duration.TotalSeconds / input.Plan.Thumbnails.IntervalSeconds
                );

                if (imageCount > 0)
                {
                    FfmpegCommand spriteCmd = thumbGen.BuildSpriteCommand(
                        options.FfmpegPathOverride,
                        input.OutputDirectory,
                        input.Plan.Thumbnails,
                        imageCount
                    );

                    ExecutionResult spriteResult = await executor.ExecuteAsync(
                        spriteCmd,
                        TimeSpan.FromSeconds(30),
                        correlationId: context.CorrelationId,
                        ct: ct
                    );

                    if (!spriteResult.Success)
                    {
                        logger.LogWarning(
                            "[{CorrelationId}] Sprite assembly failed — skipping thumbnail VTT",
                            context.CorrelationId
                        );
                    }
                    else
                    {
                        await thumbGen.WriteVttCueFileAsync(
                            input.OutputDirectory,
                            input.Plan.Thumbnails,
                            imageCount,
                            context.MediaInfo.Duration,
                            ct
                        );

                        thumbGen.CleanupIndividualThumbnails(
                            input.OutputDirectory,
                            input.Plan.Thumbnails
                        );

                        logger.LogDebug(
                            "[{CorrelationId}] Sprite assembled: {Count} frames",
                            context.CorrelationId,
                            imageCount
                        );
                    }
                }
            }

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
