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

using NoMercy.MediaProcessing.Images;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.ReleaseGroups;

public interface IReleaseGroupManager
{
    public Task Store(
        MusicBrainzReleaseGroup releaseGroup,
        Ulid id,
        CoverArtImageManagerManager.CoverPalette colorPalette
    );
}
