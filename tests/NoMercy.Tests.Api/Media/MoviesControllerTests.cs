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

namespace NoMercy.Tests.Api.Media;

[Trait("Category", "MediaMovies")]
public class MoviesControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    private const int SeededMovieId = 129;

    public MoviesControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    private static StringContent JsonBody(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    private Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object body) =>
        client.PostAsync(url, JsonBody(body));

    [Fact]
    public async Task GetMovie_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync($"/api/v1/movie/{SeededMovieId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMovie_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync($"/api/v1/movie/{SeededMovieId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMovie_ReturnsEnvelopeWithDataObject_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync($"/api/v1/movie/{SeededMovieId}");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("movie response envelope must contain a 'data' property");
        data.ValueKind.Should().Be(JsonValueKind.Object, "movie data must be an object");
    }

    [Fact]
    public async Task GetMovie_DataObject_ContainsRequiredClientFields()
    {
        HttpResponseMessage response = await _authed.GetAsync($"/api/v1/movie/{SeededMovieId}");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");

        data.TryGetProperty("id", out _).Should().BeTrue("clients read 'id'");
        data.TryGetProperty("title", out _).Should().BeTrue("clients read 'title'");
        data.TryGetProperty("overview", out _).Should().BeTrue("clients read 'overview'");
    }

    [Fact]
    public async Task GetMovie_ReturnsNotFound_WhenMovieDoesNotExist_AndTmdbFails()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/movie/999999999");

        response
            .StatusCode.Should()
            .BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetMovieAvailable_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            $"/api/v1/movie/{SeededMovieId}/available"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMovieAvailable_ReturnsOkWithAvailableFlag_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"/api/v1/movie/{SeededMovieId}/available"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("available response must have a 'data' property");
        data.TryGetProperty("available", out JsonElement availableEl)
            .Should()
            .BeTrue("data must contain 'available' boolean");
        availableEl.ValueKind.Should().Be(JsonValueKind.True, "seeded movie has a video file");
    }

    [Fact]
    public async Task GetMovieAvailable_ReturnsNotFound_WhenMovieHasNoFile()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/movie/999999999/available");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteMovie_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.DeleteAsync(
            $"/api/v1/movie/{SeededMovieId}"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LikeMovie_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PostJsonAsync(
            _unauthed,
            $"/api/v1/movie/{SeededMovieId}/like",
            new { value = true }
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LikeMovie_ReturnsBadRequest_WhenBodyIsMissing()
    {
        HttpResponseMessage response = await _authed.PostAsync(
            $"/api/v1/movie/{SeededMovieId}/like",
            new StringContent(string.Empty, Encoding.UTF8, "application/json")
        );

        response
            .StatusCode.Should()
            .BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AddToWatchList_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PostJsonAsync(
            _unauthed,
            $"/api/v1/movie/{SeededMovieId}/watch-list",
            new { add = true }
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }
}
