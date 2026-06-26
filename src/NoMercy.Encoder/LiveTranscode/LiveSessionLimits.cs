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

public class LiveSessionLimits
{
    public int MaxConcurrentSessions { get; set; } = 4;
    public int MaxSessionsPerUser { get; set; } = 2;
    public long MaxSegmentDiskUsageBytes { get; set; } = 1L * 1024 * 1024 * 1024;
    public int SessionTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Minutes of inactivity (no playlist or segment hit) after which the reaper
    /// disposes and cleans the session. Default: 5 minutes.
    /// </summary>
    public int IdleTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Buffer thresholds used by <see cref="BufferManager"/> to decide whether to
    /// suspend, resume, or drop quality on a live transcode. All in seconds of
    /// buffer-ahead measured from the player's reported playhead.
    /// </summary>
    public BufferThresholds Buffer { get; set; } = new();
}

public sealed class BufferThresholds
{
    /// <summary>Above this many seconds ahead, suspend the encoder.</summary>
    public int SuspendAboveSeconds { get; set; } = 30;

    /// <summary>Below this many seconds (while suspended), resume.</summary>
    public int ResumeBelowSeconds { get; set; } = 15;

    /// <summary>Below this many seconds, drop one quality tier.</summary>
    public int DropQualityBelowSeconds { get; set; } = 5;

    /// <summary>Below this many seconds, drop straight to the lowest tier.</summary>
    public int EmergencyDropBelowSeconds { get; set; } = 3;
}
