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
using Microsoft.AspNetCore.Authorization;

namespace NoMercy.Service.Authorization;

/// <summary>Requires the caller to be the server owner.</summary>
public sealed class OwnerRequirement : IAuthorizationRequirement;

/// <summary>Requires the caller to be a moderator (Manage permission) or the owner.</summary>
public sealed class ModeratorRequirement : IAuthorizationRequirement;

/// <summary>Requires the caller to have media access (Allowed permission) or be the owner.</summary>
public sealed class MediaAccessRequirement : IAuthorizationRequirement;
