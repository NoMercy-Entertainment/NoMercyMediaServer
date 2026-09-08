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

namespace NoMercy.Database.Models.Music;

/// <summary>
/// How a track's audio analysis ended.
/// <para>
/// A track that cannot be analyzed has to stop being picked up. Without a
/// terminal state, a corrupt or unreadable file is selected by every sweep
/// forever and the queue never drains.
/// </para>
/// </summary>
public enum AudioAnalysisState
{
    /// <summary>
    /// No verdict yet. The job writes its verdict in one step, so today a run
    /// that dies leaves no row rather than a Pending one; the sweep treats a
    /// missing row and a Pending row alike, as work still to do.
    /// </summary>
    Pending = 0,

    /// <summary>Analyzed. Individual measurements may still be null.</summary>
    Ok = 1,

    /// <summary>
    /// Analysis ran and failed. Retried only when the analyzer version changes,
    /// on the assumption that a new analyzer may succeed where the old one did not.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Reserved: the file is not analyzable at all — no audio stream, or a
    /// codec the build cannot decode — and would never be retried. The job
    /// cannot yet tell that apart from a failure a newer analyzer might fix,
    /// so it records <see cref="Failed" /> for both and nothing assigns this
    /// state yet.
    /// </summary>
    Unsupported = 3,
}
