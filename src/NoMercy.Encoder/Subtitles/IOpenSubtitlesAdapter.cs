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
/// Encoder-facing adapter for OpenSubtitles. Translates between encoder
/// records and provider DTOs so encoder code never imports provider types.
/// </summary>
public interface IOpenSubtitlesAdapter
{
    Task<IReadOnlyList<SubtitleCandidate>> SearchByHashAsync(
        string movieHash,
        long fileSize,
        string[] languages,
        TimeSpan timeout,
        CancellationToken ct,
        bool trustedOnly = false,
        bool priority = false
    );

    Task<IReadOnlyList<SubtitleCandidate>> SearchByFilenameAsync(
        string filename,
        string[] languages,
        TimeSpan timeout,
        CancellationToken ct,
        bool trustedOnly = false,
        bool priority = false
    );

    Task<IReadOnlyList<SubtitleCandidate>> SearchByTitleAsync(
        string title,
        int? season,
        int? episode,
        int? year,
        string[] languages,
        TimeSpan timeout,
        CancellationToken ct,
        bool trustedOnly = false,
        bool priority = false
    );

    /// <param name="priority">
    /// True for a request someone is waiting on, which jumps the rate-limit queue ahead of any
    /// backlog work. Background sweeps must pass false or they starve playback.
    /// </param>
    Task<byte[]> DownloadAsync(
        SubtitleCandidate candidate,
        CancellationToken ct,
        bool priority = false
    );

    bool IsRateLimited { get; }
}
