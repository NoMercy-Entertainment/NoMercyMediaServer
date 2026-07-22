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
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// Guards against a regression of the "sort" endpoints requiring an implicit
/// [ApiController]-inferred query-string `id` even though the route template
/// carries no `{id}` token. Both /dashboard/libraries/sort and
/// /dashboard/specials/sort take the sort order from the request BODY only —
/// a plain body-only call must never come back as a 400 before the handler
/// even runs.
/// </summary>
[Trait(name: "Category", value: "DashboardLibrarySort")]
public class LibrarySortEndpointsTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public LibrarySortEndpointsTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    private static StringContent JsonBody(object obj) =>
        new(content: JsonSerializer.Serialize(value: obj), encoding: Encoding.UTF8, mediaType: "application/json");

    private Task<HttpResponseMessage> PatchAsync(HttpClient client, string url, object body) =>
        client.PatchAsync(requestUri: url, content: JsonBody(obj: body));

    [Fact]
    public async Task SortLibraries_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PatchAsync(
            client: _unauthed,
            url: "/api/v1/dashboard/libraries/sort",
            body: new
            {
                libraries = new[]
                {
                    new { id = NoMercyApiFactory.MovieLibraryId.ToString(), order = 1 },
                },
            }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task SortLibraries_NoIdQueryString_DoesNotReturnBadRequest()
    {
        HttpResponseMessage response = await PatchAsync(
            client: _authed,
            url: "/api/v1/dashboard/libraries/sort",
            body: new
            {
                libraries = new[]
                {
                    new { id = NoMercyApiFactory.MovieLibraryId.ToString(), order = 1 },
                },
            }
        );

        response.StatusCode.Should().NotBe(unexpected: HttpStatusCode.BadRequest);
        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.OK, HttpStatusCode.NotFound]);
    }

    [Fact]
    public async Task SortLibraries_ValidBody_ReturnsOkEnvelopeWithStatusField()
    {
        HttpResponseMessage response = await PatchAsync(
            client: _authed,
            url: "/api/v1/dashboard/libraries/sort",
            body: new
            {
                libraries = new[]
                {
                    new { id = NoMercyApiFactory.MovieLibraryId.ToString(), order = 5 },
                    new { id = NoMercyApiFactory.TvLibraryId.ToString(), order = 6 },
                },
            }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "status", value: out JsonElement status)
            .Should()
            .BeTrue(because: "sort response must include a 'status' field");
        status.GetString().Should().Be(expected: "ok");
    }

    [Fact]
    public async Task SortSpecials_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PatchAsync(
            client: _unauthed,
            url: "/api/v1/dashboard/specials/sort",
            body: new
            {
                libraries = new[]
                {
                    new { id = NoMercyApiFactory.FavoriteSpecialId.ToString(), order = 1 },
                },
            }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task SortSpecials_NoIdQueryString_DoesNotReturnBadRequest()
    {
        HttpResponseMessage response = await PatchAsync(
            client: _authed,
            url: "/api/v1/dashboard/specials/sort",
            body: new
            {
                libraries = new[]
                {
                    new { id = NoMercyApiFactory.FavoriteSpecialId.ToString(), order = 1 },
                },
            }
        );

        response.StatusCode.Should().NotBe(unexpected: HttpStatusCode.BadRequest);
        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.OK, HttpStatusCode.NotFound]);
    }

    [Fact]
    public async Task SortSpecials_ValidBody_ReturnsOkEnvelopeWithStatusField()
    {
        HttpResponseMessage response = await PatchAsync(
            client: _authed,
            url: "/api/v1/dashboard/specials/sort",
            body: new
            {
                libraries = new[]
                {
                    new { id = NoMercyApiFactory.FavoriteSpecialId.ToString(), order = 2 },
                },
            }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "status", value: out JsonElement status)
            .Should()
            .BeTrue(because: "sort response must include a 'status' field");
        status.GetString().Should().Be(expected: "ok");
    }
}
