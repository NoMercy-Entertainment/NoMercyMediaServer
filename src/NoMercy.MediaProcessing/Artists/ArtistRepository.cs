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

namespace NoMercy.MediaProcessing.Artists;

public class ArtistRepository(MediaContext context) : IArtistRepository
{
    public Task StoreAsync(Artist artist)
    {
        return context
            .Artists.Upsert(entity: artist)
            .On(match: e => new { e.Id })
            .WhenMatched(
                updater: (s, i) =>
                    new()
                    {
                        Id = i.Id,
                        Name = i.Name,
                        Disambiguation = i.Disambiguation,
                        Description = i.Description,

                        Folder = i.Folder,
                        HostFolder = i.HostFolder,
                        LibraryId = i.LibraryId,
                        FolderId = i.FolderId,
                    }
            )
            .RunAsync();
    }

    public Task LinkToLibrary(ArtistLibrary artistLibrary)
    {
        return context
            .ArtistLibrary.Upsert(entity: artistLibrary)
            .On(match: e => new { e.ArtistId, e.LibraryId })
            .WhenMatched(updater: (s, i) => new() { ArtistId = i.ArtistId, LibraryId = i.LibraryId })
            .RunAsync();
    }

    public Task LinkToReleaseGroup(ArtistReleaseGroup artistReleaseGroup)
    {
        return context
            .ArtistReleaseGroup.Upsert(entity: artistReleaseGroup)
            .On(match: e => new { e.ArtistId, e.ReleaseGroupId })
            .WhenMatched(
                updater: (s, i) => new() { ArtistId = i.ArtistId, ReleaseGroupId = i.ReleaseGroupId }
            )
            .RunAsync();
    }

    public Task LinkToRelease(AlbumArtist artistRelease)
    {
        return context
            .AlbumArtist.Upsert(entity: artistRelease)
            .On(match: e => new { e.AlbumId, e.ArtistId })
            .WhenMatched(updater: (s, i) => new() { AlbumId = i.AlbumId, ArtistId = i.ArtistId })
            .RunAsync();
    }

    public Task LinkToRecording(ArtistTrack artistRecording)
    {
        return context
            .ArtistTrack.Upsert(entity: artistRecording)
            .On(match: e => new { e.ArtistId, e.TrackId })
            .WhenMatched(updater: (s, i) => new() { ArtistId = i.ArtistId, TrackId = i.TrackId })
            .RunAsync();
    }
}
