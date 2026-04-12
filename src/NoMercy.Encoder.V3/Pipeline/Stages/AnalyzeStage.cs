namespace NoMercy.Encoder.V3.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Infrastructure;

public class AnalyzeStage(
    IMediaAnalyzer analyzer,
    IFileSystem fileSystem,
    ILogger<AnalyzeStage> logger
) : IPipelineStage<string, MediaInfo>
{
    public string Name => "Analyze";

    public async Task<StageResult> ExecuteAsync(
        string inputPath,
        EncodingContext context,
        CancellationToken ct
    )
    {
        logger.LogInformation(
            "[{CorrelationId}] Analyzing: {Path}",
            context.CorrelationId,
            inputPath
        );

        if (!fileSystem.FileExists(inputPath))
        {
            return new StageFailure(
                new EncodingError(
                    EncodingErrorKind.InputNotFound,
                    $"Input file not found: {inputPath}",
                    null,
                    Name,
                    false
                )
            );
        }

        try
        {
            MediaInfo info = await analyzer.AnalyzeAsync(inputPath, ct);
            logger.LogInformation(
                "[{CorrelationId}] Analysis complete: {Video}v {Audio}a {Sub}s {Duration}",
                context.CorrelationId,
                info.VideoStreams.Count,
                info.AudioStreams.Count,
                info.SubtitleStreams.Count,
                info.Duration
            );
            return new StageSuccess<MediaInfo>(info);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StageFailure(
                new EncodingError(
                    EncodingErrorKind.InputCorrupt,
                    $"Failed to analyze: {ex.Message}",
                    null,
                    Name,
                    false
                )
            );
        }
    }
}
