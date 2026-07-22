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

namespace NoMercy.Service.Authorization;

/// <summary>
/// OIDC scope arrives from Keycloak as a SINGLE space-delimited "scope" claim
/// (e.g. "openid profile client-roles-nomercy-ui email"), but some code paths
/// (and older test handlers) emit one claim per scope. A value-equals
/// <c>RequireClaim("scope","openid")</c> silently rejects the real single-claim
/// token, which is why the "api" policy could never be safely wired. This treats
/// both shapes uniformly by splitting every "scope" claim value on whitespace.
/// </summary>
public static class ApiScopePolicy
{
    public static bool HasRequiredScope(ClaimsPrincipal user) =>
        HasRequiredScope(scopeClaimValues: user.FindAll(type: "scope").Select(selector: claim => claim.Value));

    public static bool HasRequiredScope(IEnumerable<string> scopeClaimValues)
    {
        HashSet<string> scopes = scopeClaimValues
            .SelectMany(selector: value =>
                value.Split(
                    separator: ' ',
                    options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
            )
            .ToHashSet(comparer: StringComparer.Ordinal);

        return scopes.Contains(item: "openid") || scopes.Contains(item: "profile");
    }
}
