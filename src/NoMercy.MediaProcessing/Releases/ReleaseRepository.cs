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
using NoMercy.NmSystem.Extensions;

namespace NoMercy.MediaProcessing.Releases;

public class ReleaseRepository(MediaContext context) : IReleaseRepository
{
    public Task Store(Album release)
    {
        if (string.IsNullOrEmpty(release.TitleSort))
            release.TitleSort = release.Name.TitleSort();

        return context
            .Albums.Upsert(release)
            .On(e => new { e.Id })
            .WhenMatched(
                (s, i) =>
                    new()
                    {
                        Id = i.Id,
                        Name = i.Name,
                        TitleSort = i.TitleSort,
                        Disambiguation = i.Disambiguation,
                        Description = i.Description,
                        Year = i.Year,
                        Country = i.Country,
                        Tracks = i.Tracks,
                        _colorPalette = i._colorPalette,
                        LibraryId = i.LibraryId,
                        Folder = i.Folder,
                        FolderId = i.FolderId,
                        HostFolder = i.HostFolder,
                        Cover = i.Cover,
                    }
            )
            .RunAsync();
    }

    public Task LinkToReleaseGroup(AlbumReleaseGroup albumReleaseGroup)
    {
        return context
            .AlbumReleaseGroup.Upsert(albumReleaseGroup)
            .On(e => new { e.AlbumId, e.ReleaseGroupId })
            .WhenMatched((s, i) => new() { AlbumId = i.AlbumId, ReleaseGroupId = i.ReleaseGroupId })
            .RunAsync();
    }

    public Task LinkToLibrary(AlbumLibrary albumLibrary)
    {
        return context
            .AlbumLibrary.Upsert(albumLibrary)
            .On(e => new { e.AlbumId, e.LibraryId })
            .WhenMatched((s, i) => new() { AlbumId = i.AlbumId, LibraryId = i.LibraryId })
            .RunAsync();
    }
}
