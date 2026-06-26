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
using System.Text.Json;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait("Category", "Home")]
public class HomeControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public HomeControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    [Fact]
    public async Task Index_Authenticated_ReturnsPaginatedEnvelope()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1?take=10&page=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);

        Assert.True(
            document.RootElement.TryGetProperty("data", out JsonElement data),
            "Home page response must expose a 'data' array"
        );
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
    }

    [Fact]
    public async Task Index_Unauthenticated_DoesNotReturnOk()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1?take=10&page=0");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
