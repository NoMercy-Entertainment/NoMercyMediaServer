namespace NoMercy.Encoder.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Errors;
using NoMercy.Storage;

public class AnalyzeStage(IMediaAnalyzer analyzer, IStorage storage, ILogger<AnalyzeStage> logger)
    : IPipelineStage<string, MediaInfo>
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

        if (!storage.Exists(inputPath))
        {
            return new StageFailure(
                new(
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
            EmitSourceQuirkDecisions(info, context);
            return new StageSuccess<MediaInfo>(info);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StageFailure(
                new(
                    EncodingErrorKind.InputCorrupt,
                    $"Failed to analyze: {ex.Message}",
                    null,
                    Name,
                    false
                )
            );
        }
    }

    /// <summary>
    /// Emits a structured <see cref="DecisionLog"/> entry for every
    /// source-side quirk a downstream stage will need to know about.
    /// Effortless principle: nothing here ever blocks the encode — the
    /// dashboard reads the log later if the user is curious why DV got
    /// stripped or why the plan thinned its rungs.
    /// </summary>
    private static void EmitSourceQuirkDecisions(MediaInfo info, EncodingContext context)
    {
        IDecisionLogSink sink = context.DecisionsOrNoOp;

        if (info.DolbyVision is not null)
        {
            sink.Add(
                new DecisionLog(
                    "analyze",
                    "analyze.dv_present",
                    $"Dolby Vision profile {info.DolbyVision.Profile} level {info.DolbyVision.Level} detected.",
                    info.DolbyVision
                )
            );
        }

        foreach (VideoStreamInfo v in info.VideoStreams)
        {
            if (IsVariableFrameRate(v))
            {
                sink.Add(
                    new DecisionLog(
                        "analyze",
                        "analyze.vfr_detected",
                        $"Stream {v.Index} reports variable frame rate (real {v.RealFrameRate:F3} vs avg {v.AverageFrameRate:F3}).",
                        new
                        {
                            v.Index,
                            v.RealFrameRate,
                            v.AverageFrameRate,
                        }
                    )
                );
            }
        }

        int fontAttachments = info.Attachments.Count(a => IsFontMimeType(a.MimeType));
        if (fontAttachments > 0)
        {
            sink.Add(
                new DecisionLog(
                    "analyze",
                    "analyze.attached_fonts",
                    $"{fontAttachments} font attachment(s) present — will be extracted next to subtitles.",
                    new { Count = fontAttachments }
                )
            );
        }

        if (info.Chapters.Count > 0)
        {
            sink.Add(
                new DecisionLog(
                    "analyze",
                    "analyze.chapter_count",
                    $"{info.Chapters.Count} chapter(s) detected.",
                    new { Count = info.Chapters.Count }
                )
            );
        }
    }

    private static bool IsVariableFrameRate(VideoStreamInfo v)
    {
        if (v.RealFrameRate is null || v.AverageFrameRate is null)
            return false;
        // 1% spread is the standard threshold ffmpeg/handbrake use.
        double diff = Math.Abs(v.RealFrameRate.Value - v.AverageFrameRate.Value);
        return diff / Math.Max(v.RealFrameRate.Value, 1.0) > 0.01;
    }

    private static bool IsFontMimeType(string? mime) =>
        mime is not null
        && (
            mime.Contains("font", StringComparison.OrdinalIgnoreCase)
            || mime.Contains("truetype", StringComparison.OrdinalIgnoreCase)
            || mime.Contains("opentype", StringComparison.OrdinalIgnoreCase)
        );
}
