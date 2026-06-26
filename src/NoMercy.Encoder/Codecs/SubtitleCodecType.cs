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

namespace NoMercy.Encoder.Codecs;

public enum SubtitleCodecType
{
    WebVtt,
    Srt,
    Ass,
    Pgs,

    /// <summary>
    /// Subtitle stream copy (no transcode). Preserves PGS / DVB image
    /// subtitles, ASS typesetting, and SRT timing exactly as the source
    /// holds them — useful for archival MKV outputs where conversion to
    /// WebVTT would lose typesetting or image-sub fidelity. Encoder emits
    /// <c>-c:s copy</c>.
    /// </summary>
    Copy,
}
