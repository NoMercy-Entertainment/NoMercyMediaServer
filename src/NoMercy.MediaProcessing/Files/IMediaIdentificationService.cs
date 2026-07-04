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
using MovieFileLibrary;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.MediaProcessing.Files;

/// <summary>
/// Identifies a parsed media file against TMDB. Null = unidentified (never a drop).
/// </summary>
public interface IMediaIdentificationService
{
    Task<(MovieOrEpisode match, string? imdbId)?> IdentifyAsync(
        MovieFile parsed,
        string libraryType,
        TimeSpan? duration,
        int? overrideTmdbId,
        bool seasonExplicit,
        DateOnly? airDate = null,
        CancellationToken ct = default
    );
}
