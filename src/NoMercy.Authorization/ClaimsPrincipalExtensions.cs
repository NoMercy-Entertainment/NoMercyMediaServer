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
/// Stateless reads of identity claims off a <see cref="ClaimsPrincipal"/>.
/// User-cache and authorization-policy concerns live in IUserCache and
/// IMediaAuthorizationPolicy respectively.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal? principal)
    {
        string? userId = principal?.FindFirst(type: ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(input: userId, result: out Guid parsedUserId) ? parsedUserId : Guid.Empty;
    }

    public static string Role(this ClaimsPrincipal? principal) =>
        principal?.FindFirst(type: ClaimTypes.Role)?.Value ?? string.Empty;

    public static string UserName(this ClaimsPrincipal? principal)
    {
        string? nameValue = principal?.FindFirst(type: "name")?.Value;
        if (nameValue is not null)
            return nameValue;

        string given = principal?.FindFirst(type: ClaimTypes.GivenName)?.Value ?? string.Empty;
        string surname = principal?.FindFirst(type: ClaimTypes.Surname)?.Value ?? string.Empty;
        return $"{given} {surname}".Trim();
    }

    public static string Email(this ClaimsPrincipal? principal) =>
        principal?.FindFirst(type: ClaimTypes.Email)?.Value ?? string.Empty;

    public static bool IsSelf(this ClaimsPrincipal? principal, Guid userId) =>
        principal.UserId().Equals(g: userId);
}
