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

using System.Net;
using NoMercy.Plugins.Network;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginNetworkAllowlistHandlerTests
{
    private static HttpClient Client(params string[] hosts) =>
        new(new PluginNetworkAllowlistHandler(hosts) { InnerHandler = new AlwaysOkHandler() });

    [Fact]
    public async Task Allowed_Host_PassesThrough()
    {
        HttpClient client = Client("*.somafm.com");
        HttpResponseMessage response = await client.GetAsync("https://ice1.somafm.com/groovesalad");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Disallowed_Host_Throws()
    {
        HttpClient client = Client("*.somafm.com");
        await Assert.ThrowsAsync<PluginNetworkDeniedException>(() =>
            client.GetAsync("https://evil.example.com/x")
        );
    }

    [Fact]
    public async Task EmptyAllowlist_DeniesEverything()
    {
        HttpClient client = Client();
        await Assert.ThrowsAsync<PluginNetworkDeniedException>(() =>
            client.GetAsync("https://anything.com/")
        );
    }
}
