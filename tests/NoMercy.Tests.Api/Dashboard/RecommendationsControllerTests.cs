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

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// Contract tests for <c>api/v1/dashboard/recommendations</c>. The list routes
/// (movies/tv/anime) are MediaAccess-gated and answer with a single "grid"
/// component envelope; <c>/diagnostics</c> steps up to Moderator; and
/// <c>/{type}/{id}</c> validates the route type before ever touching the real
/// TMDB-backed <see cref="NoMercy.Providers.TMDB.Client.IMovieMetadataProvider"/> /
/// <see cref="NoMercy.Providers.TMDB.Client.ITvShowMetadataProvider"/> — which
/// <see cref="NoMercyApiFactory"/> replaces with loose mocks so these tests never
/// reach the network. Those mocks always resolve null, so the detail route's
/// only network-free, deterministic outcome to assert is the 404 "not found" path.
/// </summary>
[Trait(name: "Category", value: "DashboardRecommendations")]
public class RecommendationsControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;
    private readonly HttpClient _secondaryUser;

    // Seeded by NoMercyApiFactory.SeedMediaData.
    private const int SeededMovieId = 129;
    private const int SeededTvId = 1399;

    public RecommendationsControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
        _secondaryUser = factory.CreateClient().AsSecondaryUser();
    }

    [Theory]
    [InlineData(data: "movies")]
    [InlineData(data: "tv")]
    [InlineData(data: "anime")]
    public async Task GetRecommendations_ReturnsUnauthorized_WhenAnonymous(string segment)
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: $"/api/v1/dashboard/recommendations/{segment}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Theory]
    [InlineData(data: ["movies", "recommendations-movies", "Recommended Movies"])]
    [InlineData(data: ["tv", "recommendations-tv", "Recommended TV Shows"])]
    [InlineData(data: ["anime", "recommendations-anime", "Recommended Anime"])]
    public async Task GetRecommendations_ReturnsGridComponentEnvelope_WhenAuthenticated(
        string segment,
        string expectedComponentId,
        string expectedTitle
    )
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: $"/api/v1/dashboard/recommendations/{segment}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement root = doc.RootElement;

        root.TryGetProperty(propertyName: "data", value: out JsonElement data).Should().BeTrue();
        data.ValueKind.Should().Be(expected: JsonValueKind.Array);
        data.GetArrayLength().Should().Be(expected: 1, because: "each recommendations route wraps a single grid");

        JsonElement envelope = data[index: 0];
        envelope.GetProperty(propertyName: "component").GetString().Should().Be(expected: "NMGrid");

        JsonElement props = envelope.GetProperty(propertyName: "props");
        props.GetProperty(propertyName: "id").GetString().Should().Be(expected: expectedComponentId);
        props.GetProperty(propertyName: "title").GetString().Should().Be(expected: expectedTitle);
        props.GetProperty(propertyName: "items").ValueKind.Should().Be(expected: JsonValueKind.Array);
    }

    [Fact]
    public async Task GetDiagnostics_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: "/api/v1/dashboard/recommendations/diagnostics"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetDiagnostics_ReturnsForbidden_WhenAllowedButNotModerator()
    {
        // TestAuthHandler.SecondaryUserId is seeded Allowed=true, Owner=false, Manage=false —
        // it passes MediaAccess but must fail the stricter Moderator policy this route requires.
        HttpResponseMessage response = await _secondaryUser.GetAsync(
            requestUri: "/api/v1/dashboard/recommendations/diagnostics"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDiagnostics_ReturnsDiagnosticsFields_WhenModerator()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: "/api/v1/dashboard/recommendations/diagnostics"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement root = doc.RootElement;

        root.TryGetProperty(propertyName: "libraries", value: out _).Should().BeTrue();
        root.TryGetProperty(propertyName: "animeByLibraryType", value: out _).Should().BeTrue();
        root.TryGetProperty(propertyName: "animeByMediaType", value: out _).Should().BeTrue();
        root.TryGetProperty(propertyName: "totalRecsWithTv", value: out _).Should().BeTrue();
        root.TryGetProperty(propertyName: "animeRecsByMediaType", value: out _).Should().BeTrue();
        root.TryGetProperty(propertyName: "totalSimWithTv", value: out _).Should().BeTrue();
        root.TryGetProperty(propertyName: "animeSimByMediaType", value: out _).Should().BeTrue();
        root.TryGetProperty(propertyName: "sampleAnimeIds", value: out _).Should().BeTrue();
        root.TryGetProperty(propertyName: "sampleRecsCount", value: out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetRecommendationDetail_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: $"/api/v1/dashboard/recommendations/movie/{SeededMovieId}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetRecommendationDetail_InvalidType_Returns400()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: $"/api/v1/dashboard/recommendations/show/{SeededMovieId}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(data: ["movie", SeededMovieId])]
    [InlineData(data: ["tv", SeededTvId])]
    [InlineData(data: ["anime", SeededTvId])]
    public async Task GetRecommendationDetail_ValidTypeProviderReturnsNull_Returns404(
        string type,
        int id
    )
    {
        // NoMercyApiFactory replaces IMovieMetadataProvider/ITvShowMetadataProvider with
        // loose mocks that always resolve null — this proves the auth + route-type
        // validation + "anime maps onto the tv provider" wiring all reach the service
        // layer correctly and that a null TMDB lookup surfaces as 404, without ever
        // dialing out to the real TMDB API.
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: $"/api/v1/dashboard/recommendations/{type}/{id}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }
}
