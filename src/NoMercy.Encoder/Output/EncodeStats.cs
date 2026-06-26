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

namespace NoMercy.Encoder.Output;

/// <summary>
/// Aggregate encode statistics attached to a completed
/// <see cref="NoMercy.Encoder.Orchestration.EncodingResult"/>. The
/// dashboard renders these in the job summary card — wall-clock time
/// and frame-rate in the performance row, bitrate + size ratio in the
/// output row.
/// </summary>
/// <param name="DurationSeconds">
/// Wall-clock seconds elapsed from the moment the orchestrator handed
/// the job to the strategy until the strategy returned. Derived from
/// <c>Stopwatch</c> measurements already present in the orchestrator.
/// </param>
/// <param name="AvgFps">
/// Average frames-per-second reported by FFmpeg during encoding.
/// Taken from <see cref="NoMercy.Encoder.Execution.ExecutionMetrics.AverageFps"/>
/// when available; 0 when the strategy does not produce FFmpeg metrics
/// (e.g. copy-only jobs).
/// </param>
/// <param name="OutputBitrateKbps">
/// Effective output bitrate in kilobits per second, computed as
/// <c>(OutputBytes * 8) / (DurationSeconds * 1000)</c>. 0 when
/// <see cref="DurationSeconds"/> is zero or no output was produced.
/// </param>
/// <param name="SourceBytes">
/// Size of the source file in bytes at job dispatch time. Used by the
/// dashboard to display the compression ratio column.
/// </param>
/// <param name="OutputBytes">
/// Total bytes across all <see cref="OutputArtifact"/> entries. Derived
/// by summing <see cref="OutputArtifact.SizeBytes"/> — stored here so
/// callers don't need to re-enumerate the artifact list.
/// </param>
public sealed record EncodeStats(
    double DurationSeconds,
    double AvgFps,
    int OutputBitrateKbps,
    long SourceBytes,
    long OutputBytes
);
