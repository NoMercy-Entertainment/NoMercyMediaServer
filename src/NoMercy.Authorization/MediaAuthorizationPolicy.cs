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

using System.Security.Claims;
using NoMercy.Database.Models.Users;

namespace NoMercy.Authorization;

public class MediaAuthorizationPolicy(IUserCache userCache) : IMediaAuthorizationPolicy
{
    public bool IsOwner(ClaimsPrincipal? principal)
    {
        User? owner = userCache.Users.FirstOrDefault(u => u.Owner);
        return owner is not null && principal?.UserId() == owner.Id;
    }

    public bool IsModerator(ClaimsPrincipal? principal) =>
        userCache.Users.Any(u => u.Manage && u.Id == principal.UserId()) || IsOwner(principal);

    public bool IsAllowed(ClaimsPrincipal? principal) =>
        userCache.Users.Any(u => u.Allowed && u.Id == principal.UserId()) || IsOwner(principal);
}
