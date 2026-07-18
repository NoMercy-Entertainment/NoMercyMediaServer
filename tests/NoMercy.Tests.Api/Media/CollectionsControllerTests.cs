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

[Trait("Category", "MediaCollections")]
public class CollectionsControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;
    private readonly HttpClient _secondaryUser;

    public CollectionsControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
        _secondaryUser = factory.CreateClient().AsSecondaryUser();
    }

    [Fact]
    public async Task GetCollections_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1/collection");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCollections_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/collection");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCollections_ReturnsEnvelopeWithDataProperty_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/collection");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out _)
            .Should()
            .BeTrue("collections response envelope must contain a 'data' property");
    }

    [Fact]
    public async Task GetCollections_WithTakeAndPage_ReturnsOk()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/collection?take=5&page=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCollection_ById_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1/collection/313369");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCollection_ById_DoesNotReturn500_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/collection/313369");

        ((int)response.StatusCode)
            .Should()
            .NotBe(500, "controller must not panic — returns 200 from TMDB or 404 when not found");
    }

    [Fact]
    public async Task GetCollectionAvailable_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            "/api/v1/collection/313369/available"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteCollection_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.DeleteAsync("/api/v1/collection/313369");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteCollection_ReturnsForbidden_WhenSecondaryUserNonModerator()
    {
        // Deleting a collection is irreversible: raised from "MediaAccess" to
        // "Moderator". SecondaryUserId (Allowed=true, Owner=false, Manage=false)
        // must now be rejected, where it previously reached the repository.
        HttpResponseMessage response = await _secondaryUser.DeleteAsync(
            "/api/v1/collection/313369"
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteCollection_ReturnsOk_WhenModerator()
    {
        // Uses a non-existent id: CollectionRepository.DeleteAsync is a no-op
        // delete-if-present, always returning 200, so this proves the
        // Moderator tier still reaches the repository without disturbing any
        // seeded collection other tests in this class depend on.
        HttpResponseMessage response = await _authed.DeleteAsync("/api/v1/collection/999999999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
