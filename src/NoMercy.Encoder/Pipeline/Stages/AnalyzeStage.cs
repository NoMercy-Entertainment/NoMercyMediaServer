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
            message: "[{CorrelationId}] Analyzing: {Path}", args: [context.CorrelationId, inputPath]
        );

        // Use the per-folder source storage from the job context when available;
        // fall back to the DI-injected singleton for app-state / default installs.
        IStorage effectiveStorage = context.SourceStorage ?? storage;

        if (!effectiveStorage.Exists(path: inputPath))
        {
            return new StageFailure(
                Error: new(
                    Kind: EncodingErrorKind.InputNotFound,
                    Message: $"Input file not found: {inputPath}",
                    FfmpegStderr: null,
                    StageName: Name,
                    Recoverable: false
                )
            );
        }

        try
        {
            MediaInfo info = await analyzer.AnalyzeAsync(filePath: inputPath, sourceStorage: effectiveStorage, ct: ct);
            logger.LogInformation(
                message: "[{CorrelationId}] Analysis complete: {Video}v {Audio}a {Sub}s {Duration}", args: [context.CorrelationId, info.VideoStreams.Count, info.AudioStreams.Count, info.SubtitleStreams.Count, info.Duration]
            );
            EmitSourceQuirkDecisions(info: info, context: context);
            return new StageSuccess<MediaInfo>(Value: info);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StageFailure(
                Error: new(
                    Kind: EncodingErrorKind.InputCorrupt,
                    Message: $"Failed to analyze: {ex.Message}",
                    FfmpegStderr: null,
                    StageName: Name,
                    Recoverable: false
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
                entry: new(
                    Stage: "analyze",
                    Key: "analyze.dv_present",
                    Message: $"Dolby Vision profile {info.DolbyVision.Profile} level {info.DolbyVision.Level} detected.",
                    Data: info.DolbyVision
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
                    entry: new(
                        Stage: "analyze",
                        Key: "analyze.vfr_detected",
                        Message: $"Stream {v.Index} reports variable frame rate "
                                 + $"(real {v.RealFrameRate!.Value.ToString(format: "F3", provider: CultureInfo.InvariantCulture)} "
                                 + $"vs avg {v.AverageFrameRate!.Value.ToString(format: "F3", provider: CultureInfo.InvariantCulture)}).",
                        Data: new
                        {
                            v.Index,
                            v.RealFrameRate,
                            v.AverageFrameRate,
                        }
                    )
                );
            }
        }

        int fontAttachments = info.Attachments.Count(predicate: a => IsFontMimeType(mime: a.MimeType));
        if (fontAttachments > 0)
        {
            sink.Add(
                entry: new(
                    Stage: "analyze",
                    Key: "analyze.attached_fonts",
                    Message: $"{fontAttachments} font attachment(s) present — will be extracted next to subtitles.",
                    Data: new { Count = fontAttachments }
                )
            );
        }

        if (info.Chapters.Count > 0)
        {
            sink.Add(
                entry: new(
                    Stage: "analyze",
                    Key: "analyze.chapter_count",
                    Message: $"{info.Chapters.Count} chapter(s) detected.",
                    Data: new { info.Chapters.Count }
                )
            );
        }
    }

    private static bool IsFontMimeType(string? mime) =>
        mime is not null
        && (
            mime.Contains(value: "font", comparisonType: StringComparison.OrdinalIgnoreCase)
            || mime.Contains(value: "truetype", comparisonType: StringComparison.OrdinalIgnoreCase)
            || mime.Contains(value: "opentype", comparisonType: StringComparison.OrdinalIgnoreCase)
        );
}
