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

using NoMercy.Database.Models.Music;

namespace NoMercy.MediaProcessing.MusicGenres;

public interface IMusicGenreRepository
{
    public Task Store(MusicGenre musicGenre);
    public Task LinkToArtist(IEnumerable<ArtistMusicGenre> genreArtists);
    public Task LinkToRecording(IEnumerable<MusicGenreTrack> genreRecordings);
    public Task LinkToReleaseGroup(MusicGenreReleaseGroup genreReleaseGroup);
    public Task LinkToRelease(IEnumerable<AlbumMusicGenre> genreReleases);
}
