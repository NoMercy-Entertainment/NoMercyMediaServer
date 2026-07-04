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

namespace NoMercy.Authorization;

/// <summary>
/// Authorization policy decisions for media access, replacing the static
/// IsOwner/IsModerator/IsAllowed methods on ClaimsPrincipalExtensions.
/// </summary>
public interface IMediaAuthorizationPolicy
{
    bool IsOwner(ClaimsPrincipal? principal);
    bool IsModerator(ClaimsPrincipal? principal);
    bool IsAllowed(ClaimsPrincipal? principal);
}
