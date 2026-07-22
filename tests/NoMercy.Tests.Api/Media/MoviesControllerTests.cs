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
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait(name: "Category", value: "MediaMovies")]
public class MoviesControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;
    private readonly HttpClient _secondaryUser;

    private const int SeededMovieId = 129;

    public MoviesControllerTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
        _secondaryUser = factory.CreateClient().AsSecondaryUser();
    }

    private static StringContent JsonBody(object obj) =>
        new(content: JsonSerializer.Serialize(value: obj), encoding: Encoding.UTF8, mediaType: "application/json");

    private Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object body) =>
        client.PostAsync(requestUri: url, content: JsonBody(obj: body));

    [Fact]
    public async Task GetMovie_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: $"/api/v1/movie/{SeededMovieId}");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetMovie_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: $"/api/v1/movie/{SeededMovieId}");

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMovie_ReturnsEnvelopeWithDataObject_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: $"/api/v1/movie/{SeededMovieId}");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "movie response envelope must contain a 'data' property");
        data.ValueKind.Should().Be(expected: JsonValueKind.Object, because: "movie data must be an object");
    }

    [Fact]
    public async Task GetMovie_DataObject_ContainsRequiredClientFields()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: $"/api/v1/movie/{SeededMovieId}");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");

        data.TryGetProperty(propertyName: "id", value: out _).Should().BeTrue(because: "clients read 'id'");
        data.TryGetProperty(propertyName: "title", value: out _).Should().BeTrue(because: "clients read 'title'");
        data.TryGetProperty(propertyName: "overview", value: out _).Should().BeTrue(because: "clients read 'overview'");
    }

    [Fact]
    public async Task GetMovie_ReturnsNotFound_WhenMovieDoesNotExist_AndTmdbFails()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/movie/999999999");

        response
            .StatusCode.Should()
            .BeOneOf(validValues: [HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable]);
    }

    [Fact]
    public async Task GetMovieAvailable_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: $"/api/v1/movie/{SeededMovieId}/available"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetMovieAvailable_ReturnsOkWithAvailableFlag_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: $"/api/v1/movie/{SeededMovieId}/available"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "available response must have a 'data' property");
        data.TryGetProperty(propertyName: "available", value: out JsonElement availableEl)
            .Should()
            .BeTrue(because: "data must contain 'available' boolean");
        availableEl.ValueKind.Should().Be(expected: JsonValueKind.True, because: "seeded movie has a video file");
    }

    [Fact]
    public async Task GetMovieAvailable_ReturnsNotFound_WhenMovieHasNoFile()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/movie/999999999/available");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.NotFound, HttpStatusCode.OK]);
    }

    [Fact]
    public async Task DeleteMovie_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.DeleteAsync(
            requestUri: $"/api/v1/movie/{SeededMovieId}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task DeleteMovie_ReturnsForbidden_WhenSecondaryUserNonModerator()
    {
        // Deleting a movie is irreversible: raised from "MediaAccess" to
        // "Moderator". SecondaryUserId (Allowed=true, Owner=false, Manage=false)
        // must now be rejected, where it previously reached the repository.
        HttpResponseMessage response = await _secondaryUser.DeleteAsync(
            requestUri: $"/api/v1/movie/{SeededMovieId}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteMovie_ReturnsOk_WhenModerator()
    {
        // Uses a non-existent id: MovieRepository.DeleteAsync is a no-op
        // delete-if-present, always returning 200, so this proves the
        // Moderator tier still reaches the repository without disturbing the
        // seeded movie other tests in this class depend on.
        HttpResponseMessage response = await _authed.DeleteAsync(requestUri: "/api/v1/movie/999999999");

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteMovie_PublishesInfoPageGridHomeAndContinueWatchingInvalidation()
    {
        // MovieRepository.DeleteAsync is unconditional (delete-if-present), so
        // the controller must publish the invalidation events regardless of
        // whether the id actually exists — matching DeleteMovie_ReturnsOk_WhenModerator
        // above, this uses a non-existent id so it never disturbs the seeded
        // movie other tests in this class depend on.
        const int deletedId = 777777777;

        IEventBus eventBus = _factory.Services.GetRequiredService<IEventBus>();
        List<LibraryRefreshedEvent> captured = [];
        using IDisposable subscription = eventBus.Subscribe<LibraryRefreshedEvent>(
            handler: (evt, _) =>
            {
                captured.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        HttpResponseMessage response = await _authed.DeleteAsync(requestUri: $"/api/v1/movie/{deletedId}");

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        captured
            .Should()
            .Contain(
                predicate: evt => evt.QueryKey.SequenceEqual(new object?[] { "movie", deletedId.ToString() }),
                because: "the deleted movie's info page must be invalidated"
            );
        captured
            .Should()
            .Contain(
                predicate: evt => evt.QueryKey.SequenceEqual(new object?[] { "libraries" }),
                because: "every library grid must be invalidated (no id -> prefix match)"
            );
        captured
            .Should()
            .Contain(
                predicate: evt => evt.QueryKey.SequenceEqual(new object?[] { "home" }),
                because: "the home page must be invalidated"
            );
        captured
            .Should()
            .Contain(
                predicate: evt => evt.QueryKey.SequenceEqual(new object?[] { "continue-watching" }),
                because: "continue watching must be invalidated"
            );
    }

    [Fact]
    public async Task LikeMovie_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PostJsonAsync(
            client: _unauthed,
            url: $"/api/v1/movie/{SeededMovieId}/like",
            body: new { value = true }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task LikeMovie_ReturnsBadRequest_WhenBodyIsMissing()
    {
        HttpResponseMessage response = await _authed.PostAsync(
            requestUri: $"/api/v1/movie/{SeededMovieId}/like",
            content: new StringContent(content: string.Empty, encoding: Encoding.UTF8, mediaType: "application/json")
        );

        response
            .StatusCode.Should()
            .BeOneOf(validValues: [HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity]);
    }

    [Fact]
    public async Task AddToWatchList_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PostJsonAsync(
            client: _unauthed,
            url: $"/api/v1/movie/{SeededMovieId}/watch-list",
            body: new { add = true }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }
}
