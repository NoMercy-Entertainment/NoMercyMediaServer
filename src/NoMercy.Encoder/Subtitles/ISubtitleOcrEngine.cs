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

using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Subtitles;

public interface ISubtitleOcrEngine
{
    Task<SubtitleTrack> OcrAsync(
        string inputPath,
        int streamIndex,
        string language,
        SubtitleCodecType outputFormat,
        CancellationToken ct
    );
}

public record SubtitleTrack(
    string FilePath,
    string Language,
    SubtitleCodecType Format,
    int CueCount
);
