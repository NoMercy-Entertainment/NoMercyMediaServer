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

namespace NoMercy.Database.Music;

/// <summary>
/// Everything the Picard rules read, gathered in one place so the naming is a pure
/// function of it. The script draws on values that live on the MusicBrainz release
/// rather than on our rows — release type, the credited versus standardized artist,
/// disc and track totals — so the caller assembles this at import time.
/// </summary>
public record MusicNamingContext
{
    public MusicAlbumType AlbumType { get; init; } = MusicAlbumType.Standard;

    public string? AlbumName { get; init; }
    public int? Year { get; init; }

    /// <summary>MusicBrainz id of the album artist; identifies the Various and Unknown buckets.</summary>
    public string? AlbumArtistId { get; init; }

    /// <summary>Sort name, which is what the artist folder and its initial are taken from.</summary>
    public string? AlbumArtistSort { get; init; }

    public string? AlbumArtistPrimary { get; init; }

    public string? TrackTitle { get; init; }
    public string? TrackArtistPrimary { get; init; }
    public string? TrackArtistsCredited { get; init; }
    public string? TrackArtistsAdditional { get; init; }

    public int TrackNumber { get; init; } = 1;
    public int TotalTracks { get; init; } = 1;
    public int DiscNumber { get; init; } = 1;
    public int TotalDiscs { get; init; } = 1;
}
