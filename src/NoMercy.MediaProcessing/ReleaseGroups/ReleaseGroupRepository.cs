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

namespace NoMercy.MediaProcessing.ReleaseGroups;

public class ReleaseGroupRepository(MediaContext context) : IReleaseGroupRepository
{
    public Task Store(ReleaseGroup releaseGroup)
    {
        return context
            .ReleaseGroups.Upsert(releaseGroup)
            .On(e => new { e.Id })
            .WhenMatched(
                (s, i) =>
                    new()
                    {
                        Id = i.Id,
                        Title = i.Title,
                        Description = i.Description,
                        Year = i.Year,
                        LibraryId = i.LibraryId,
                        Cover = i.Cover,
                    }
            )
            .RunAsync();
    }
}
