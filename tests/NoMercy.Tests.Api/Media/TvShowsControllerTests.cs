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
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait("Category", "MediaTvShows")]
public class TvShowsControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;
    private readonly HttpClient _secondaryUser;

    private const int SeededShowId = 1399;

    public TvShowsControllerTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
        _secondaryUser = factory.CreateClient().AsSecondaryUser();
    }

    private static StringContent JsonBody(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    private Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object body) =>
        client.PostAsync(url, JsonBody(body));

    [Fact]
    public async Task GetTv_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync($"/api/v1/tv/{SeededShowId}");

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetTv_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync($"/api/v1/tv/{SeededShowId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTv_ReturnsEnvelopeWithDataObject_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync($"/api/v1/tv/{SeededShowId}");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("TV response envelope must contain a 'data' property");
        data.ValueKind.Should().Be(JsonValueKind.Object, "TV data must be an object");
    }

    [Fact]
    public async Task GetTv_DataObject_ContainsRequiredClientFields()
    {
        HttpResponseMessage response = await _authed.GetAsync($"/api/v1/tv/{SeededShowId}");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");

        data.TryGetProperty("id", out _).Should().BeTrue("clients read 'id'");
        data.TryGetProperty("title", out _).Should().BeTrue("clients read 'title'");
        data.TryGetProperty("overview", out _).Should().BeTrue("clients read 'overview'");
    }

    [Fact]
    public async Task GetTvAvailable_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            $"/api/v1/tv/{SeededShowId}/available"
        );

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetTvAvailable_ReturnsOkWithAvailableFlag_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"/api/v1/tv/{SeededShowId}/available"
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
        availableEl.ValueKind.Should().Be(JsonValueKind.True, "seeded show has a video file");
    }

    [Fact]
    public async Task GetTvWatch_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync($"/api/v1/tv/{SeededShowId}/watch");

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetTvWatch_ReturnsOkWithArray_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync($"/api/v1/tv/{SeededShowId}/watch");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.ValueKind.Should()
            .Be(JsonValueKind.Array, "watch response must be an array");
    }

    [Fact]
    public async Task GetTvMissing_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            $"/api/v1/tv/{SeededShowId}/missing"
        );

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetTvMissing_ReturnsOkWithDataProperty_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync($"/api/v1/tv/{SeededShowId}/missing");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out _)
            .Should()
            .BeTrue("missing episodes response must have a 'data' property");
    }

    [Fact]
    public async Task DeleteTv_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.DeleteAsync($"/api/v1/tv/{SeededShowId}");

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task DeleteTv_ReturnsForbidden_WhenSecondaryUserNonModerator()
    {
        // Deleting a show is irreversible: raised from "MediaAccess" to
        // "Moderator". SecondaryUserId (Allowed=true, Owner=false, Manage=false)
        // must now be rejected, where it previously reached the repository.
        HttpResponseMessage response = await _secondaryUser.DeleteAsync(
            $"/api/v1/tv/{SeededShowId}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteTv_ReturnsOk_WhenModerator()
    {
        // Uses a non-existent id: TvShowRepository.DeleteAsync is a no-op
        // delete-if-present, always returning 200, so this proves the
        // Moderator tier still reaches the repository without disturbing the
        // seeded show other tests in this class depend on.
        HttpResponseMessage response = await _authed.DeleteAsync("/api/v1/tv/999999999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteTv_PublishesInfoPageGridHomeAndContinueWatchingInvalidation()
    {
        // TvShowRepository.DeleteAsync is unconditional (delete-if-present), so
        // the controller must publish the invalidation events regardless of
        // whether the id actually exists — matching DeleteTv_ReturnsOk_WhenModerator
        // above, this uses a non-existent id so it never disturbs the seeded
        // show other tests in this class depend on.
        const int deletedId = 888888888;

        IEventBus eventBus = _factory.Services.GetRequiredService<IEventBus>();
        List<LibraryRefreshedEvent> captured = [];
        using IDisposable subscription = eventBus.Subscribe<LibraryRefreshedEvent>(
            (evt, _) =>
            {
                captured.Add(evt);
                return Task.CompletedTask;
            }
        );

        HttpResponseMessage response = await _authed.DeleteAsync($"/api/v1/tv/{deletedId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        captured
            .Should()
            .Contain(
                evt => evt.QueryKey.SequenceEqual(new object?[] { "tv", deletedId.ToString() }),
                "the deleted show's info page must be invalidated"
            );
        captured
            .Should()
            .Contain(
                evt => evt.QueryKey.SequenceEqual(new object?[] { "libraries" }),
                "every library grid must be invalidated (no id -> prefix match)"
            );
        captured
            .Should()
            .Contain(
                evt => evt.QueryKey.SequenceEqual(new object?[] { "home" }),
                "the home page must be invalidated"
            );
        captured
            .Should()
            .Contain(
                evt => evt.QueryKey.SequenceEqual(new object?[] { "continue-watching" }),
                "continue watching must be invalidated"
            );
    }

    [Fact]
    public async Task LikeTv_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PostJsonAsync(
            _unauthed,
            $"/api/v1/tv/{SeededShowId}/like",
            new { value = true }
        );

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task LikeTv_ReturnsBadRequest_WhenBodyIsMissing()
    {
        HttpResponseMessage response = await _authed.PostAsync(
            $"/api/v1/tv/{SeededShowId}/like",
            new StringContent(string.Empty, Encoding.UTF8, "application/json")
        );

        response
            .StatusCode.Should()
            .BeOneOf([HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity]);
    }

    [Fact]
    public async Task AddToWatchList_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PostJsonAsync(
            _unauthed,
            $"/api/v1/tv/{SeededShowId}/watch-list",
            new { add = true }
        );

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }
}
