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

namespace NoMercy.MediaProcessing.Shows;

/// <summary>
/// A Tv row whose Kitsu-classified media type disagrees with the Type of the
/// Library it is currently filed under.
/// </summary>
public sealed record AnimeClassificationMismatch(
    int TvId,
    string Title,
    string LibraryType,
    string ClassifiedType
);

/// <summary>
/// Detects existing Tv rows that were filed under the wrong library type by
/// re-running every show through the shared, Kitsu-backed
/// <see cref="IMediaTypeClassifier"/>. Detection and reporting only — does
/// not move files or rewrite any row.
/// </summary>
public interface IAnimeClassificationAuditService
{
    Task<IReadOnlyList<AnimeClassificationMismatch>> FindMismatchesAsync(
        CancellationToken ct = default
    );
}
