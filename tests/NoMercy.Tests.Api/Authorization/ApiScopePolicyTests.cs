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

using NoMercy.Service.Authorization;
using Xunit;

namespace NoMercy.Tests.Api.Authorization;

/// <summary>
/// The "api" policy is now the default policy, so its scope check runs on every
/// bare-[Authorize] endpoint. A real Keycloak token carries ONE space-delimited
/// "scope" claim; a value-equals check would reject it and lock out every client.
/// HasRequiredScope must accept both the single-claim and multi-claim shapes.
/// </summary>
public class ApiScopePolicyTests
{
    [Theory]
    // Real Keycloak shape: a single space-delimited claim.
    [InlineData(true, "openid profile client-roles-nomercy-ui email")]
    [InlineData(true, "openid")]
    [InlineData(true, "profile email")]
    // No openid/profile present.
    [InlineData(false, "email address")]
    [InlineData(false, "")]
    public void HasRequiredScope_SingleClaim(bool expected, string scopeValue)
    {
        Assert.Equal(expected, ApiScopePolicy.HasRequiredScope([scopeValue]));
    }

    [Fact]
    public void HasRequiredScope_MultipleClaims_AlsoAccepted()
    {
        Assert.True(ApiScopePolicy.HasRequiredScope(["openid", "profile"]));
    }

    [Fact]
    public void HasRequiredScope_NoScopeClaims_IsFalse()
    {
        Assert.False(ApiScopePolicy.HasRequiredScope([]));
    }
}
