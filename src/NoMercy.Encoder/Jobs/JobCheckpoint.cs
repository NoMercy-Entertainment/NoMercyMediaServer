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

namespace NoMercy.Encoder.Jobs;

public record JobCheckpoint(
    string JobId,
    string InputPath,
    string OutputDirectory,
    int[] CompletedGroupIndices,
    DateTime LastUpdated,
    string? StatsFilePath = null,
    bool Pass1Completed = false,
    int LastCompletedSegment = -1,
    string? EncodeMode = null,
    /// <summary>
    /// Identifier of the variant this checkpoint belongs to (e.g. "1080p").
    /// Empty for single-variant or non-ladder encodes. Phase 4.13 of the
    /// encoder-v3 alignment plan added this field so multi-variant ladders
    /// can resume each variant independently.
    /// </summary>
    string VariantId = "",
    /// <summary>
    /// Source-position the encoder reached before failing, expressed in
    /// milliseconds. Resume seeks the source to the previous safe keyframe
    /// before this position to avoid re-encoding completed work.
    /// </summary>
    long LastProgressMs = 0,
    /// <summary>
    /// Final ~16 KB of ffmpeg stderr captured before the failure. Used by
    /// the dashboard to render a "why did this fail?" panel without forcing
    /// the user to dig through log files. Plain text, never structured.
    /// </summary>
    string LastFfmpegStderrTail = "",
    /// <summary>
    /// Wall-clock instant the failure was recorded. Distinct from
    /// <see cref="LastUpdated"/>, which tracks the most recent progress
    /// write. <c>null</c> while the job is still running cleanly.
    /// </summary>
    DateTime? FailedAt = null
);
