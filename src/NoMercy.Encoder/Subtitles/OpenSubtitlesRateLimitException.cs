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

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Thrown when the OpenSubtitles API responds with HTTP 429 or signals rate limiting.
/// The adapter catches this and returns an empty result without failing the encode.
/// </summary>
public class OpenSubtitlesRateLimitException : Exception
{
    public OpenSubtitlesRateLimitException()
        : base("OpenSubtitles rate limit exceeded") { }

    public OpenSubtitlesRateLimitException(string message)
        : base(message) { }
}
