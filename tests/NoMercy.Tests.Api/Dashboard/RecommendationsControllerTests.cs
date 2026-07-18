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
[Trait("Category", "DashboardRecommendations")]
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
    [InlineData("movies")]
    [InlineData("tv")]
    [InlineData("anime")]
    public async Task GetRecommendations_ReturnsUnauthorized_WhenAnonymous(string segment)
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            $"/api/v1/dashboard/recommendations/{segment}"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("movies", "recommendations-movies", "Recommended Movies")]
    [InlineData("tv", "recommendations-tv", "Recommended TV Shows")]
    [InlineData("anime", "recommendations-anime", "Recommended Anime")]
    public async Task GetRecommendations_ReturnsGridComponentEnvelope_WhenAuthenticated(
        string segment,
        string expectedComponentId,
        string expectedTitle
    )
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"/api/v1/dashboard/recommendations/{segment}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.TryGetProperty("data", out JsonElement data).Should().BeTrue();
        data.ValueKind.Should().Be(JsonValueKind.Array);
        data.GetArrayLength().Should().Be(1, "each recommendations route wraps a single grid");

        JsonElement envelope = data[0];
        envelope.GetProperty("component").GetString().Should().Be("NMGrid");

        JsonElement props = envelope.GetProperty("props");
        props.GetProperty("id").GetString().Should().Be(expectedComponentId);
        props.GetProperty("title").GetString().Should().Be(expectedTitle);
        props.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetDiagnostics_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            "/api/v1/dashboard/recommendations/diagnostics"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDiagnostics_ReturnsForbidden_WhenAllowedButNotModerator()
    {
        // TestAuthHandler.SecondaryUserId is seeded Allowed=true, Owner=false, Manage=false —
        // it passes MediaAccess but must fail the stricter Moderator policy this route requires.
        HttpResponseMessage response = await _secondaryUser.GetAsync(
            "/api/v1/dashboard/recommendations/diagnostics"
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDiagnostics_ReturnsDiagnosticsFields_WhenModerator()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            "/api/v1/dashboard/recommendations/diagnostics"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.TryGetProperty("libraries", out _).Should().BeTrue();
        root.TryGetProperty("animeByLibraryType", out _).Should().BeTrue();
        root.TryGetProperty("animeByMediaType", out _).Should().BeTrue();
        root.TryGetProperty("totalRecsWithTv", out _).Should().BeTrue();
        root.TryGetProperty("animeRecsByMediaType", out _).Should().BeTrue();
        root.TryGetProperty("totalSimWithTv", out _).Should().BeTrue();
        root.TryGetProperty("animeSimByMediaType", out _).Should().BeTrue();
        root.TryGetProperty("sampleAnimeIds", out _).Should().BeTrue();
        root.TryGetProperty("sampleRecsCount", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetRecommendationDetail_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            $"/api/v1/dashboard/recommendations/movie/{SeededMovieId}"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRecommendationDetail_InvalidType_Returns400()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"/api/v1/dashboard/recommendations/show/{SeededMovieId}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("movie", SeededMovieId)]
    [InlineData("tv", SeededTvId)]
    [InlineData("anime", SeededTvId)]
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
            $"/api/v1/dashboard/recommendations/{type}/{id}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
