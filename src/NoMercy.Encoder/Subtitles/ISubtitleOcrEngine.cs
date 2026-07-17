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
using NoMercy.Storage;

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
    /// <param name="sourceStorage">
    /// The storage <paramref name="inputPath"/> is addressed against — mirrors
    /// <see cref="Analysis.IMediaAnalyzer.AnalyzeAsync(string, IStorage, CancellationToken)"/>.
    /// Required whenever the path is relative to a non-local driver (an NFS/S3
    /// library folder): the engine's own injected <see cref="IStorage"/> is
    /// local, so it resolves a driver-relative key against the local filesystem
    /// and the OCR run dies before ffmpeg ever starts. Null (the default) keeps
    /// the injected storage for callers that already hold a resolved local path.
    /// </param>
    /// <param name="sidecar">
    /// Where the sidecar belongs within an encode bundle, and under which name —
    /// see <see cref="OcrSidecarTarget"/>, which is what makes the result pair
    /// with its bitmap track and reach a player. Null (the default) writes it
    /// next to <paramref name="inputPath"/>, for callers spot-checking a file
    /// that is not part of a bundle.
    /// </param>
    Task<SubtitleTrack> OcrAsync(
        string inputPath,
        int streamIndex,
        string language,
        SubtitleCodecType outputFormat,
        CancellationToken ct,
        IStorage? sourceStorage = null,
        OcrSidecarTarget? sidecar = null
    );
}

public record SubtitleTrack(
    string FilePath,
    string Language,
    SubtitleCodecType Format,
    int CueCount
);
