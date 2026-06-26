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

using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Output;

namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Top-level result returned by
/// <see cref="NoMercy.Encoder.Orchestration.IEncodingOrchestrator.EncodeAsync"/>.
/// Every return path — success, failure, cancellation — produces one of
/// these records.
///
/// <para>Existing call sites that only read <see cref="Success"/>,
/// <see cref="OutputPath"/>, <see cref="Duration"/>, <see cref="Error"/>,
/// and <see cref="Metrics"/> continue to compile unchanged — those five
/// positional constructor parameters are preserved in full. The enrichment
/// fields below default to safe empties and are populated via
/// object-initialiser syntax at the orchestration layer.</para>
///
/// <para>The dashboard uses <see cref="Status"/> to colour the job card
/// (green / red / grey), <see cref="Artifacts"/> for the file-tree panel,
/// <see cref="Stats"/> for the performance summary row, and
/// <see cref="EnrichedError"/> to deep-link the error chip to the docs
/// page for this failure mode.</para>
/// </summary>
public record EncodingResult(
    bool Success,
    string OutputPath,
    TimeSpan Duration,
    EncodingError? Error,
    EncodingMetrics? Metrics
)
{
    /// <summary>
    /// Terminal state of the job: <c>success</c>, <c>failed</c>, or
    /// <c>cancelled</c>. The dashboard maps these to green / red / grey
    /// job-card badges respectively.
    /// </summary>
    public string Status { get; init; } = "success";

    /// <summary>
    /// Opaque job identifier supplied by the queue layer. Empty string
    /// when the result was produced outside the queue (e.g. direct API
    /// call or test stub). The dashboard uses this to correlate the
    /// result with the originating queue entry.
    /// </summary>
    public string JobId { get; init; } = "";

    /// <summary>
    /// Structured plan snapshot from Phase 1.5 — strategy name, variant
    /// table, hardware bindings, and decision log. Null when the strategy
    /// did not produce a plan projection (e.g. no-op or test stubs). The
    /// dashboard renders this in the collapsible "Plan" section of the
    /// job detail screen.
    /// </summary>
    public PlanResult? Plan { get; init; }

    /// <summary>
    /// Every file written to the output directory, with size, SHA-256,
    /// and MIME type. Empty on failure or cancellation. The dashboard
    /// renders these as a file-tree card with per-row download buttons.
    /// </summary>
    public IReadOnlyList<OutputArtifact> Artifacts { get; init; } = [];

    /// <summary>
    /// Aggregate encode statistics — wall-clock time, average FPS,
    /// effective bitrate, and size ratio. Null on failure or cancellation.
    /// The dashboard renders this in the performance summary row of the
    /// job detail screen.
    /// </summary>
    public EncodeStats? Stats { get; init; }

    /// <summary>
    /// Non-fatal warnings collected during the encode (e.g. source has
    /// variable frame rate, upscaling detected). Empty when no warnings
    /// were raised. The dashboard surfaces these as dismissible warning
    /// chips below the job card header.
    /// </summary>
    public IReadOnlyList<EncoderRule> Warnings { get; init; } = [];

    /// <summary>
    /// Structured error shape when <see cref="Status"/> is <c>failed</c>.
    /// Carries the stable <see cref="EncoderErrorShape.Id"/> used by the
    /// dashboard to deep-link to the docs page for this failure mode, the
    /// human-readable <see cref="EncoderErrorShape.Message"/>, and an
    /// actionable <see cref="EncoderErrorShape.Suggestion"/>. Null on
    /// success or cancellation.
    /// </summary>
    public EncoderErrorShape? EnrichedError { get; init; }
}

/// <summary>
/// Aggregate FFmpeg metrics for a completed encode. Populated from
/// <see cref="NoMercy.Encoder.Execution.ExecutionMetrics"/> by the
/// strategy layer and surfaced via <see cref="EncodingResult.Metrics"/>.
/// </summary>
/// <param name="OutputSizeBytes">Total bytes written to the output path.</param>
/// <param name="AverageSpeed">Encode speed relative to realtime (e.g. 2.5 = 2.5× faster).</param>
/// <param name="AverageFps">Average frames-per-second reported by FFmpeg during the encode.</param>
/// <param name="EncoderUsed">Name of the FFmpeg encoder used (e.g. <c>h264_nvenc</c>, <c>libx264</c>).</param>
/// <param name="GpuUsed">GPU device identifier when hardware acceleration was active; null for software encodes.</param>
public record EncodingMetrics(
    long OutputSizeBytes,
    double AverageSpeed,
    double AverageFps,
    string EncoderUsed,
    string? GpuUsed
);
