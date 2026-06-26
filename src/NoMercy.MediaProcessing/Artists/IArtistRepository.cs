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

namespace NoMercy.MediaProcessing.Artists;

public interface IArtistRepository
{
    public Task StoreAsync(Artist artist);
    Task LinkToRelease(AlbumArtist insert);
    Task LinkToLibrary(ArtistLibrary insert);
    Task LinkToReleaseGroup(ArtistReleaseGroup insert);
    Task LinkToRecording(ArtistTrack insert);
}
