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

namespace NoMercy.MediaProcessing.Recordings;

public class RecordingRepository(MediaContext context) : IRecordingRepository
{
    public Task Store(Track recording, bool update = false)
    {
        if (update)
        {
            return context
                .Tracks.Upsert(entity: recording)
                .On(match: e => new { e.Id })
                .WhenMatched(
                    updater: (ts, ti) =>
                        new()
                        {
                            Id = ti.Id,
                            Name = ti.Name,
                            DiscNumber = ti.DiscNumber,
                            TrackNumber = ti.TrackNumber,
                            Date = ti.Date,
                            Folder = ts.Folder,
                            FolderId = ts.FolderId,
                            HostFolder = ts.HostFolder,
                            Duration = ts.Duration,
                            Filename = ts.Filename,
                            Quality = ts.Quality,
                        }
                )
                .RunAsync();
        }

        return context
            .Tracks.Upsert(entity: recording)
            .On(match: e => new { e.Id })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Id = ti.Id,
                        Name = ti.Name,
                        DiscNumber = ti.DiscNumber,
                        TrackNumber = ti.TrackNumber,
                        Date = ti.Date,
                        Folder = ti.Folder,
                        FolderId = ti.FolderId,
                        HostFolder = ti.HostFolder,
                        Duration = ti.Duration,
                        Filename = ti.Filename,
                        Quality = ti.Quality,
                    }
            )
            .RunAsync();
    }

    public Task LinkToRelease(AlbumTrack trackRelease)
    {
        return context
            .AlbumTrack.Upsert(entity: trackRelease)
            .On(match: e => new { e.AlbumId, e.TrackId })
            .WhenMatched(updater: (s, i) => new() { AlbumId = i.AlbumId, TrackId = i.TrackId })
            .RunAsync();
    }

    public Task LinkToArtist(ArtistTrack insert)
    {
        return context
            .ArtistTrack.Upsert(entity: insert)
            .On(match: e => new { e.ArtistId, e.TrackId })
            .WhenMatched(updater: (s, i) => new() { ArtistId = i.ArtistId, TrackId = i.TrackId })
            .RunAsync();
    }

    public Task LinkToLibrary(LibraryTrack libraryTrack)
    {
        return context
            .LibraryTrack.Upsert(entity: new(libraryId: libraryTrack.LibraryId, trackId: libraryTrack.TrackId))
            .On(match: v => new { v.LibraryId, v.TrackId })
            .WhenMatched(updater: (lts, lti) => new() { LibraryId = lti.LibraryId, TrackId = lti.TrackId })
            .RunAsync();
    }

    public Task LinkToLibrary(ArtistLibrary artistLibrary)
    {
        return context
            .ArtistLibrary.Upsert(entity: new(artistId: artistLibrary.ArtistId, libraryId: artistLibrary.LibraryId))
            .On(match: v => new { v.ArtistId, v.LibraryId })
            .WhenMatched(updater: (lts, lti) => new() { ArtistId = lti.ArtistId, LibraryId = lti.LibraryId })
            .RunAsync();
    }

    public async Task<int> UpdateTrackLyricsAsync(Track track, string serializeObject)
    {
        return await context
            .Upsert(entity: track)
            .On(match: v => new { v.Id })
            .WhenMatched(updater: v => new() { _lyrics = serializeObject })
            .RunAsync();
    }
}
