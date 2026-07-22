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

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.MediaProcessing.MusicGenres;

public class MusicGenreRepository(MediaContext context) : IMusicGenreRepository
{
    public Task Store(MusicGenre genre)
    {
        return context
            .MusicGenres.Upsert(entity: genre)
            .On(match: v => new { v.Id })
            .WhenMatched(updater: v => new() { Id = v.Id, Name = v.Name })
            .RunAsync();
    }

    public Task LinkToReleaseGroup(MusicGenreReleaseGroup genreReleaseGroup)
    {
        return context
            .MusicGenreReleaseGroup.Upsert(entity: genreReleaseGroup)
            .On(match: e => new { e.GenreId, e.ReleaseGroupId })
            .WhenMatched(updater: (s, i) => new() { GenreId = i.GenreId, ReleaseGroupId = i.ReleaseGroupId })
            .RunAsync();
    }

    public Task LinkToArtist(IEnumerable<ArtistMusicGenre> genreArtists)
    {
        return context
            .ArtistMusicGenre.UpsertRange(entities: genreArtists)
            .On(match: e => new { e.MusicGenreId, e.ArtistId })
            .WhenMatched(updater: (s, i) => new() { MusicGenreId = i.MusicGenreId, ArtistId = i.ArtistId })
            .RunAsync();
    }

    public Task LinkToRelease(IEnumerable<AlbumMusicGenre> genreReleases)
    {
        return context
            .AlbumMusicGenre.UpsertRange(entities: genreReleases)
            .On(match: e => new { e.MusicGenreId, e.AlbumId })
            .WhenMatched(updater: (s, i) => new() { MusicGenreId = i.MusicGenreId, AlbumId = i.AlbumId })
            .RunAsync();
    }

    public Task LinkToRecording(IEnumerable<MusicGenreTrack> genreRecordings)
    {
        return context
            .MusicGenreTrack.UpsertRange(entities: genreRecordings)
            .On(match: e => new { e.GenreId, e.TrackId })
            .WhenMatched(updater: (s, i) => new() { GenreId = i.GenreId, TrackId = i.TrackId })
            .RunAsync();
    }
}
