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

using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.Artists;

public interface IArtistManager
{
    // Task StoreArtistAsync(MusicBrainzArtistAppends artist, Library library, Folder libraryFolder, MediaFolder mediaFolder, MusicBrainzReleaseAppends releaseAppends);

    Task Store(
        ReleaseArtistCredit artistCredit,
        Library library,
        Folder libraryFolder,
        MediaFolder mediaFolder,
        MusicBrainzReleaseAppends releaseAppends
    );

    // Task StoreArtist(MusicBrainzArtistDetails artistCredit, Library library, Folder libraryFolder,MediaFolder mediaFolder, MusicBrainzReleaseAppends releaseAppends);
}
