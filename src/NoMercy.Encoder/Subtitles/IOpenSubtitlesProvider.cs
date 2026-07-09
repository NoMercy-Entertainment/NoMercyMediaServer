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
/// Normalized search result from an OpenSubtitles-compatible provider.
/// Defined here so the encoder owns the contract; the provider implements it.
/// </summary>
public record OpenSubtitlesSearchResult(
    string Language,
    string? SubRating,
    string? SubDownloadsCnt,
    string? SubFromTrusted,
    string? MovieFPS,
    string? SubDownloadLink,
    string? SubFormat,
    string? MatchedBy,
    string? SubFileName = null,
    string? MovieReleaseName = null,
    string? SubHearingImpaired = null,
    string? UserNickName = null
);

/// <summary>
/// Provider contract for OpenSubtitles search. Defined in the encoder (dependency inversion).
/// The concrete implementation lives in MediaProcessing where both projects are accessible.
/// </summary>
public interface IOpenSubtitlesProvider
{
    Task<IReadOnlyList<OpenSubtitlesSearchResult>> SearchByHashAsync(
        string movieHash,
        long fileSize,
        string[] languages,
        CancellationToken ct
    );

    Task<IReadOnlyList<OpenSubtitlesSearchResult>> SearchByFilenameAsync(
        string filename,
        string[] languages,
        CancellationToken ct
    );

    Task<IReadOnlyList<OpenSubtitlesSearchResult>> SearchByTitleAsync(
        string title,
        int? season,
        int? episode,
        int? year,
        string[] languages,
        CancellationToken ct
    );

    Task<byte[]> DownloadSubtitleAsync(string downloadUrl, CancellationToken ct);

    bool IsRateLimited { get; }
}
