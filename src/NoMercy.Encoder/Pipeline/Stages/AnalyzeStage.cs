// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Globalization;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Errors;
using NoMercy.Storage;

namespace NoMercy.Encoder.Pipeline.Stages;

public class AnalyzeStage(IMediaAnalyzer analyzer, IStorage storage, ILogger<AnalyzeStage> logger)
    : IPipelineStage<string, MediaInfo>,
        IAnalysisStage
{
    public string Name => "Analyze";

    public async Task<StageResult> ExecuteAsync(
        string inputPath,
        EncodingContext context,
        CancellationToken ct
    )
    {
        logger.LogInformation(
            "[{CorrelationId}] Analyzing: {Path}", [context.CorrelationId, inputPath]
        );

        // Use the per-folder source storage from the job context when available;
        // fall back to the DI-injected singleton for app-state / default installs.
        IStorage effectiveStorage = context.SourceStorage ?? storage;

        if (!effectiveStorage.Exists(inputPath))
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
            MediaInfo info = await analyzer.AnalyzeAsync(inputPath, effectiveStorage, ct);
            logger.LogInformation(
                "[{CorrelationId}] Analysis complete: {Video}v {Audio}a {Sub}s {Duration}", [context.CorrelationId, info.VideoStreams.Count, info.AudioStreams.Count, info.SubtitleStreams.Count, info.Duration]
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
                new(
                    "analyze",
                    "analyze.dv_present",
                    $"Dolby Vision profile {info.DolbyVision.Profile} level {info.DolbyVision.Level} detected.",
                    info.DolbyVision
                )
            );
        }

        foreach (VideoStreamInfo v in info.VideoStreams)
        {
            if (v.IsVariableFrameRate)
            {
                // Message rides the DecisionLog the dashboard reads over the API —
                // keep it period-decimal regardless of host locale.
                sink.Add(
                    new(
                        "analyze",
                        "analyze.vfr_detected",
                        $"Stream {v.Index} reports variable frame rate "
                                 + $"(real {v.RealFrameRate!.Value.ToString("F3", CultureInfo.InvariantCulture)} "
                                 + $"vs avg {v.AverageFrameRate!.Value.ToString("F3", CultureInfo.InvariantCulture)}).",
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
                new(
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
                new(
                    "analyze",
                    "analyze.chapter_count",
                    $"{info.Chapters.Count} chapter(s) detected.",
                    new { info.Chapters.Count }
                )
            );
        }
    }

    private static bool IsFontMimeType(string? mime) =>
        mime is not null
        && (
            mime.Contains("font", StringComparison.OrdinalIgnoreCase)
            || mime.Contains("truetype", StringComparison.OrdinalIgnoreCase)
            || mime.Contains("opentype", StringComparison.OrdinalIgnoreCase)
        );
}
