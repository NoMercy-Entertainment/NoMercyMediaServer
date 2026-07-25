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
/// Default no-op provider. The encoder registers this so consumers
/// (IOpenSubtitlesAdapter, ISubtitleAcquisitionService) always resolve.
/// The Service host overrides this with the real XML-RPC provider via
/// TryAddSingleton-as-replacement when its own DI runs after the encoder's.
/// Encoder-only test contexts get no-op behavior, which is correct: they
/// don't exercise the network call and shouldn't need provider stubs.
/// </summary>
public class NoOpOpenSubtitlesProvider : IOpenSubtitlesProvider
{
    public bool IsRateLimited => false;

    public Task<IReadOnlyList<OpenSubtitlesSearchResult>> SearchByHashAsync(
        string movieHash,
        long fileSize,
        string[] languages,
        CancellationToken ct,
        bool priority = false
    ) => Task.FromResult<IReadOnlyList<OpenSubtitlesSearchResult>>([]);

    public Task<IReadOnlyList<OpenSubtitlesSearchResult>> SearchByFilenameAsync(
        string filename,
        string[] languages,
        CancellationToken ct,
        bool priority = false
    ) => Task.FromResult<IReadOnlyList<OpenSubtitlesSearchResult>>([]);

    public Task<IReadOnlyList<OpenSubtitlesSearchResult>> SearchByTitleAsync(
        string title,
        int? season,
        int? episode,
        int? year,
        string[] languages,
        CancellationToken ct,
        bool priority = false
    ) => Task.FromResult<IReadOnlyList<OpenSubtitlesSearchResult>>([]);

    public Task<byte[]> DownloadSubtitleAsync(
        string downloadUrl,
        CancellationToken ct,
        bool priority = false
    ) => Task.FromResult(Array.Empty<byte>());
}
