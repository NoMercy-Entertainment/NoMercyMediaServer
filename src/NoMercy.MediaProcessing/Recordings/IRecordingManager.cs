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
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.Recordings;

public interface IRecordingManager
{
    public Task<bool> Store(
        MusicBrainzReleaseAppends releaseAppends,
        MusicBrainzTrack musicBrainzTrack,
        MusicBrainzMedia musicBrainzMedia,
        Folder libraryFolder,
        MediaFolder mediaFolder,
        CoverArtImageManagerManager.CoverPalette? releaseCoverPalette
    );
}
