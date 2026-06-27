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
using NoMercy.Database.Models.Media;

namespace NoMercy.Data.Repositories;

public class EncoderRepository(MediaContext context) : IEncoderRepository
{
    public Task<List<EncoderProfile>> GetEncoderProfilesAsync()
    {
        return context.EncoderProfiles.AsNoTracking().ToListAsync();
    }

    public Task<EncoderProfile?> GetEncoderProfileByIdAsync(Ulid id)
    {
        return context
            .EncoderProfiles.AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.Id == id);
    }

    public Task AddEncoderProfileAsync(EncoderProfile profile)
    {
        return context
            .EncoderProfiles.Upsert(profile)
            .On(l => new { l.Id })
            .WhenMatched(
                (ls, li) =>
                    new()
                    {
                        Id = li.Id,
                        Name = li.Name,
                        Container = li.Container,
                        Param = li.Param,
                    }
            )
            .RunAsync();
    }

    public Task DeleteEncoderProfileAsync(EncoderProfile profile)
    {
        context.EncoderProfiles.Remove(profile);

        return context.SaveChangesAsync();
    }

    public Task<int> GetEncoderProfileCountAsync()
    {
        return context.EncoderProfiles.CountAsync();
    }
}
