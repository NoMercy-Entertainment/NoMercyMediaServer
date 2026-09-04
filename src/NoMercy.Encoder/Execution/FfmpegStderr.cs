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

namespace NoMercy.Encoder.Execution;

/// <summary>
/// Bounds ffmpeg's stderr for logging.
///
/// ffmpeg emits one line per corrupt packet, so a single damaged episode produces
/// thousands of near-identical decoder complaints. Logging that verbatim on failure
/// filled 98% of a live server's log in one sweep and, through a synchronous sink,
/// is the flood the queue's own saturation logging was already written to avoid.
/// The tail is kept rather than the head: ffmpeg reports the fatal reason last.
/// </summary>
public static class FfmpegStderr
{
    private const int MaxLength = 500;

    public static string Tail(string stderr)
    {
        if (string.IsNullOrEmpty(stderr))
            return "<empty>";

        return stderr.Length > MaxLength ? stderr[^MaxLength..] : stderr;
    }
}
