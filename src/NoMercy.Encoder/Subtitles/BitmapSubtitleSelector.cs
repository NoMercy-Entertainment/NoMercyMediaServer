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

using NoMercy.Encoder.Analysis;

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// One bitmap subtitle stream to OCR, paired with the index
/// <see cref="ISubtitleOcrEngine.OcrAsync"/> expects.
/// </summary>
/// <param name="SubtitleIndex">
/// Position among the file's subtitle streams — the <c>N</c> in ffmpeg's
/// <c>[0:s:N]</c>. Deliberately NOT <see cref="SubtitleStreamInfo.Index"/>,
/// which is the absolute ffprobe stream index.
/// </param>
public record BitmapSubtitleRef(int SubtitleIndex, SubtitleStreamInfo Stream);

/// <summary>
/// Single source of truth for "which subtitle streams need OCR, and what index
/// identifies them to ffmpeg". Both OCR callers route through this: a caller
/// that derives the index itself has to re-decide whether it means the absolute
/// ffprobe index or the subtitle-relative one, and the two callers answered
/// that differently — one passed <see cref="SubtitleStreamInfo.Index"/>, which
/// ffmpeg rejects with "Stream specifier ':s:N' matches no streams", so no
/// sidecar was ever written for any file whose subtitles sit behind a video and
/// audio track (i.e. essentially all of them).
/// </summary>
public static class BitmapSubtitleSelector
{
    public static IReadOnlyList<BitmapSubtitleRef> Select(
        IReadOnlyList<SubtitleStreamInfo> subtitleStreams
    ) =>
        subtitleStreams
            .Select(selector: (stream, subtitleIndex) => new BitmapSubtitleRef(SubtitleIndex: subtitleIndex, Stream: stream))
            .Where(predicate: entry => SubtitleClassifier.IsBitmapBased(codec: entry.Stream.Codec))
            .ToList();
}
