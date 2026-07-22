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
using FluentAssertions;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Media;

/// <summary>
/// Covers GET /api/v1/userData/favorites. NoMercyApiFactory seeds exactly one
/// favorited movie, tv show, collection and special for the default test
/// user — see NoMercyApiFactory.SeedMediaData "Step 8". Coverage for
/// per-user isolation and the empty-favorites case lives in
/// HomeRepositoryFavoritesTests, since the shared test-auth handler here
/// only impersonates a single fixed identity.
/// </summary>
[Trait(name: "Category", value: "UserData")]
public class UserDataFavoritesControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public UserDataFavoritesControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    [Fact]
    public async Task Favorites_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: "/api/v1/userData/favorites");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Favorites_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/userData/favorites");

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
    }

    [Fact]
    public async Task Favorites_ReturnsEnvelopeWithDataProperty()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/userData/favorites");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "favorites response envelope must contain a 'data' property");
        data.ValueKind.Should().Be(expected: JsonValueKind.Array);
    }

    [Fact]
    public async Task Favorites_ReturnsSeededMovieTvCollectionAndSpecial_OrderedByTitle()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/userData/favorites");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");

        string[] seeded = ["Breaking Bad", "Spirited Away", "Test Collection", "Test Special"];

        List<string?> titles = data.EnumerateArray()
            .Select(selector: card => card.GetProperty(propertyName: "title").GetString())
            .ToList();

        // Other test classes sharing this fixture's database legitimately create
        // and self-favorite their own specials (e.g. the dashboard "create empty
        // special" flow), so assert the seeded four are present and in title
        // order relative to each other rather than an exact, closed set.
        titles.Should().Contain(expected: seeded);
        titles.Where(predicate: title => seeded.Contains(value: title)).Should().Equal(expected: seeded);
    }

    [Fact]
    public async Task Favorites_ReturnsExpectedTypePerCard()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/userData/favorites");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");

        Dictionary<string, string?> typeByTitle = data.EnumerateArray()
            .ToDictionary(
                keySelector: card => card.GetProperty(propertyName: "title").GetString()!,
                elementSelector: card => card.GetProperty(propertyName: "type").GetString()
            );

        typeByTitle[key: "Spirited Away"].Should().Be(expected: "movie");
        typeByTitle[key: "Breaking Bad"].Should().Be(expected: "tv");
        typeByTitle[key: "Test Collection"].Should().Be(expected: "collection");
        typeByTitle[key: "Test Special"].Should().Be(expected: "specials");
    }
}
