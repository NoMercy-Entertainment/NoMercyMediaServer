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

using NoMercy.Storage;

namespace NoMercy.Encoder.Analysis;

public interface IMediaAnalyzer
{
    Task<MediaInfo> AnalyzeAsync(string filePath, CancellationToken ct = default);

    Task<MediaInfo> AnalyzeAsync(
        string filePath,
        IStorage sourceStorage,
        CancellationToken ct = default
    );

    /// <summary>
    /// Analyze a source, emitting <paramref name="extraInputArgs"/> (e.g. an HTTP
    /// "-headers" auth line) before the input. When <paramref name="filePath"/> is
    /// an http(s) URL it is probed directly; otherwise it falls back to the
    /// scope-validated filesystem path.
    /// </summary>
    Task<MediaInfo> AnalyzeAsync(
        string filePath,
        string[]? extraInputArgs,
        CancellationToken ct = default
    );
}
