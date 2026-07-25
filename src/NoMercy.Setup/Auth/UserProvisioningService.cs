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
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Setup.Auth;

public class UserProvisioningService : IUserProvisioningService
{
    private readonly IDbContextFactory<MediaContext> _mediaContextFactory;

    public UserProvisioningService(IDbContextFactory<MediaContext> mediaContextFactory)
    {
        _mediaContextFactory = mediaContextFactory;
    }

    public async Task ProvisionOwner(User user)
    {
        await using MediaContext mediaContext = await _mediaContextFactory.CreateDbContextAsync();
        await mediaContext
            .Users.Upsert(user)
            .On(x => x.Id)
            .WhenMatched(
                (oldUser, newUser) =>
                    new()
                    {
                        Id = newUser.Id,
                        Name = newUser.Name,
                        Email = newUser.Email,
                        Owner = newUser.Owner,
                        Allowed = newUser.Allowed,
                        AudioTranscoding = newUser.AudioTranscoding,
                        NoTranscoding = newUser.NoTranscoding,
                        VideoTranscoding = newUser.VideoTranscoding,
                        Manage = newUser.Manage,
                    }
            )
            .RunAsync();

        UserCache.Current.AddUser(user);
    }
}
