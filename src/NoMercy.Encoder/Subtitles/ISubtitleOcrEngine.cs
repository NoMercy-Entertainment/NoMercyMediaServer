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
    /// <param name="outputDirectory">
    /// When set, the OCR sidecar is written under
    /// <c>{outputDirectory}/subtitles/{language}.ocr{streamIndex}.{ext}</c> — the
    /// same convention the post-encode library scan already discovers text
    /// subtitle sidecars by. Interpreted against <paramref name="outputStorage"/>
    /// when that is supplied, so it can be the encode's storage-relative output
    /// key rather than a local directory. When null (the default), the sidecar is
    /// written next to <paramref name="inputPath"/>.
    /// </param>
    /// <param name="outputStorage">
    /// The storage <paramref name="outputDirectory"/> is addressed against. Supply
    /// it whenever the encode's destination is a non-local driver: without it the
    /// sidecar is written through the engine's own local storage, and a
    /// storage-relative key then resolves against the server's working directory
    /// instead of the library. Null (the default) keeps the injected storage.
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
    Task<SubtitleTrack> OcrAsync(
        string inputPath,
        int streamIndex,
        string language,
        SubtitleCodecType outputFormat,
        CancellationToken ct,
        string? outputDirectory = null,
        IStorage? sourceStorage = null,
        IStorage? outputStorage = null
    );
}

public record SubtitleTrack(
    string FilePath,
    string Language,
    SubtitleCodecType Format,
    int CueCount
);
