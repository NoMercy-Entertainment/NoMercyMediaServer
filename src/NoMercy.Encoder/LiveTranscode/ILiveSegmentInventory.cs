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

namespace NoMercy.Encoder.LiveTranscode;

/// <summary>
/// Reads and clears the on-disk <c>seg_%05d.ts</c> coverage for one live-transcode
/// session's scratch directory. On-disk presence is the source of truth for
/// "already transcoded" — <c>-hls_flags temp_file</c> guarantees a file only ever
/// appears at its final segment name once ffmpeg has finished writing it, so
/// existence alone (no content check) is a valid completeness signal.
/// </summary>
public interface ILiveSegmentInventory
{
    /// <summary>
    /// The set of absolute segment indices currently on disk under
    /// <paramref name="scratchDirectory"/>. A missing or unlistable directory
    /// (the session's first-ever spawn) returns an empty set rather than
    /// throwing.
    /// </summary>
    IReadOnlySet<int> Snapshot(string scratchDirectory);

    /// <summary>
    /// Best-effort deletes every <c>seg_*.ts</c> file under
    /// <paramref name="scratchDirectory"/>. Used when a quality change makes the
    /// existing segments invalid for the new encode — a per-file delete failure
    /// (Windows can refuse to remove a segment an in-flight HTTP response still
    /// holds open) is logged and skipped, never thrown.
    /// </summary>
    void Purge(string scratchDirectory);
}
