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
    /// <param name="streamIndex">
    /// Position among the file's SUBTITLE streams only — the <c>N</c> in
    /// ffmpeg's <c>[0:s:N]</c>, not the absolute <c>MediaInfo</c>/ffprobe stream
    /// index. For a file whose first subtitle sits at absolute index 3, its
    /// value here is 0. Passing the absolute index makes ffmpeg reject the
    /// filtergraph with "Stream specifier ':s:N' matches no streams" and the
    /// OCR sidecar is never written.
    /// </param>
    /// <param name="outputDirectory">
    /// When set, the OCR sidecar is written under
    /// <c>{outputDirectory}/subtitles/{language}.ocr{streamIndex}.{ext}</c> — the
    /// same convention the post-encode library scan already discovers text
    /// subtitle sidecars by. When null (the default), the sidecar is written
    /// next to <paramref name="inputPath"/> as before.
    /// </param>
    Task<SubtitleTrack> OcrAsync(
        string inputPath,
        int streamIndex,
        string language,
        SubtitleCodecType outputFormat,
        CancellationToken ct,
        string? outputDirectory = null
    );
}

public record SubtitleTrack(
    string FilePath,
    string Language,
    SubtitleCodecType Format,
    int CueCount
);
