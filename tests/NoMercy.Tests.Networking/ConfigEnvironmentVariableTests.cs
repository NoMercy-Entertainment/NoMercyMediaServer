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

using System.Reflection;
using NoMercy.NmSystem.Information;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// Tests verifying that Config properties load from environment variables
/// and that the client secret has been fully removed (public client with PKCE).
/// </summary>
[Trait("Category", "Unit")]
public class ConfigEnvironmentVariableTests
{
    [Fact]
    public void TokenClientSecret_Removed()
    {
        // TokenClientSecret property should no longer exist — the Keycloak client
        // is now a public client using PKCE, so no client secret is needed.
        PropertyInfo? prop = typeof(Config).GetProperty("TokenClientSecret");
        Assert.Null(prop);
    }

    [Fact]
    public void AuthBaseUrl_HasDefault()
    {
        Assert.False(string.IsNullOrEmpty(Config.AuthBaseUrl));
    }

    [Fact]
    public void ApiBaseUrl_HasDefault()
    {
        Assert.False(string.IsNullOrEmpty(Config.ApiBaseUrl));
    }

    [Fact]
    public void AppBaseUrl_HasDefault()
    {
        Assert.False(string.IsNullOrEmpty(Config.AppBaseUrl));
    }

    [Fact]
    public void ApiServerBaseUrl_DerivedFromApiBaseUrl()
    {
        Assert.Contains("v1/server/", Config.ApiServerBaseUrl);
    }

    [Fact]
    public void AuthBaseDevUrl_Removed()
    {
        PropertyInfo? devUrlProp = typeof(Config).GetProperty("AuthBaseDevUrl");
        Assert.Null(devUrlProp);
    }
}
