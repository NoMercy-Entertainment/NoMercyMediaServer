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
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait(name: "Category", value: "Specials")]
public class SpecialControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public SpecialControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    [Fact]
    public async Task Available_UnknownSpecial_ReturnsNotFound()
    {
        string id = Ulid.NewUlid().ToString();
        HttpResponseMessage response = await _authed.GetAsync(requestUri: $"/api/v1/specials/{id}/available");

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);
    }

    [Fact]
    public async Task Available_Unauthenticated_DoesNotReturnOk()
    {
        string id = Ulid.NewUlid().ToString();
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: $"/api/v1/specials/{id}/available");

        Assert.NotEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }
}
