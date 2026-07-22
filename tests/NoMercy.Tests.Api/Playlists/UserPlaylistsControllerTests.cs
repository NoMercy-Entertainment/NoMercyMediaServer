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

namespace NoMercy.Tests.Api.Playlists;

/// <summary>
/// Covers api/v1/playlists — user-created, ordered, VIDEO-ONLY playlists
/// (movies + tv shows + episodes + specials — never music tracks). Movie 129
/// ("Spirited Away") and episode 62085 ("Pilot", Breaking Bad) are pre-seeded
/// WITH video files (playable); special NoMercyApiFactory.FavoriteSpecialId is
/// a seeded special. See NoMercyApiFactory.SeedMediaData.
///
/// <see cref="Index_DoesNotReturnMusicPlaylist"/> is the leak-regression test:
/// NoMercyApiFactory.PlaylistId1 is a music playlist owned by the same default
/// test user this fixture's "_authed" client represents — proving it never
/// appears here is what distinguishes this slice's separate-container design
/// from the original (rejected) design that reused the music Playlist table.
/// </summary>
[Trait(name: "Category", value: "Playlists")]
public class UserPlaylistsControllerTests : IClassFixture<NoMercyApiFactory>
{
    private const string BaseUrl = "/api/v1/playlists";

    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;
    private readonly HttpClient _secondaryUser;

    public UserPlaylistsControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
        _secondaryUser = factory.CreateClient().AsSecondaryUser();
    }

    private static StringContent JsonBody(object obj) =>
        new(content: JsonSerializer.Serialize(value: obj), encoding: Encoding.UTF8, mediaType: "application/json");

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string url,
        object body
    ) => client.PostAsync(requestUri: url, content: JsonBody(obj: body));

    private static Task<HttpResponseMessage> PatchAsync(
        HttpClient client,
        string url,
        object body
    ) => client.PatchAsync(requestUri: url, content: JsonBody(obj: body));

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, string url, object body) =>
        client.PutAsync(requestUri: url, content: JsonBody(obj: body));

    private async Task<Guid> CreatePlaylistAsync(string name)
    {
        HttpResponseMessage response = await PostAsync(client: _authed, url: BaseUrl, body: new { name });
        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        return doc.RootElement.GetProperty(propertyName: "data").GetProperty(propertyName: "id").GetGuid();
    }

    [Fact]
    public async Task Index_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: BaseUrl);

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Index_ReturnsEnvelopeWithDataArray_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: BaseUrl);

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "playlists index must have a 'data' property");
        data.ValueKind.Should().Be(expected: JsonValueKind.Array);
    }

    [Fact]
    public async Task Index_DoesNotReturnMusicPlaylist()
    {
        // NoMercyApiFactory.PlaylistId1 is a MUSIC playlist owned by the very
        // same user this client authenticates as — it must never surface on
        // the video-only endpoint, proving the two features share no table.
        HttpResponseMessage response = await _authed.GetAsync(requestUri: BaseUrl);
        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty(propertyName: "data")
            .EnumerateArray()
            .Select(selector: p => p.GetProperty(propertyName: "id").GetString())
            .Should()
            .NotContain(unexpected: NoMercyApiFactory.PlaylistId1.ToString());
    }

    [Fact]
    public async Task Show_ReturnsNotFound_ForMusicPlaylistId()
    {
        // Even knowing the music playlist's id, it must 404 on this endpoint —
        // it lives in a different table this controller never queries.
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: $"{BaseUrl}/{NoMercyApiFactory.PlaylistId1}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PostAsync(
            client: _unauthed,
            url: BaseUrl,
            body: new { name = "Anonymous Playlist" }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameMissing()
    {
        HttpResponseMessage response = await PostAsync(client: _authed, url: BaseUrl, body: new { });

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Show_ReturnsNotFound_ForUnknownPlaylist()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: $"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddItem_ReturnsBadRequest_ForInvalidKind()
    {
        Guid playlistId = await CreatePlaylistAsync(name: "Invalid Kind Playlist");

        HttpResponseMessage response = await PostAsync(
            client: _authed,
            url: $"{BaseUrl}/{playlistId}/items",
            body: new { kind = "song", media_id = "129" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddItem_ReturnsBadRequest_ForTrackKind()
    {
        // "track" was a valid kind before this feature was made video-only —
        // it must now be rejected the same as any other unknown kind.
        Guid playlistId = await CreatePlaylistAsync(name: "Track Kind Rejected Playlist");

        HttpResponseMessage response = await PostAsync(
            client: _authed,
            url: $"{BaseUrl}/{playlistId}/items",
            body: new { kind = "track", media_id = NoMercyApiFactory.TrackId1.ToString() }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddItem_ReturnsBadRequest_ForMalformedMediaId()
    {
        Guid playlistId = await CreatePlaylistAsync(name: "Malformed Media Id Playlist");

        HttpResponseMessage response = await PostAsync(
            client: _authed,
            url: $"{BaseUrl}/{playlistId}/items",
            body: new { kind = "movie", media_id = "not-a-number" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddItem_ReturnsNotFound_ForNonexistentMovie()
    {
        Guid playlistId = await CreatePlaylistAsync(name: "Nonexistent Movie Playlist");

        HttpResponseMessage response = await PostAsync(
            client: _authed,
            url: $"{BaseUrl}/{playlistId}/items",
            body: new { kind = "movie", media_id = "999999" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddItem_ReturnsNotFound_WhenPlaylistDoesNotExist()
    {
        HttpResponseMessage response = await PostAsync(
            client: _authed,
            url: $"{BaseUrl}/{Guid.NewGuid()}/items",
            body: new { kind = "movie", media_id = "129" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FullLifecycle_CreateAddItemsGetEditReorderRemoveDelete()
    {
        Guid playlistId = await CreatePlaylistAsync(name: "Video Playlist Test");

        // Add movie (playable), episode (playable), special — in that order.
        (
            await PostAsync(
                client: _authed,
                url: $"{BaseUrl}/{playlistId}/items",
                body: new { kind = "movie", media_id = "129" }
            )
        )
            .StatusCode.Should()
            .Be(expected: HttpStatusCode.OK);

        (
            await PostAsync(
                client: _authed,
                url: $"{BaseUrl}/{playlistId}/items",
                body: new { kind = "episode", media_id = "62085" }
            )
        )
            .StatusCode.Should()
            .Be(expected: HttpStatusCode.OK);

        (
            await PostAsync(
                client: _authed,
                url: $"{BaseUrl}/{playlistId}/items",
                body: new { kind = "special", media_id = NoMercyApiFactory.FavoriteSpecialId.ToString() }
            )
        )
            .StatusCode.Should()
            .Be(expected: HttpStatusCode.OK);

        HttpResponseMessage detail = await _authed.GetAsync(requestUri: $"{BaseUrl}/{playlistId}");
        detail.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        using JsonDocument detailDoc = JsonDocument.Parse(json: await detail.Content.ReadAsStringAsync());
        JsonElement data = detailDoc.RootElement.GetProperty(propertyName: "data");
        data.GetProperty(propertyName: "id").GetGuid().Should().Be(expected: playlistId);

        JsonElement[] items = data.GetProperty(propertyName: "items").EnumerateArray().ToArray();
        items.Should().HaveCount(expected: 3);

        items
            .Select(selector: i => i.GetProperty(propertyName: "kind").GetString())
            .Should()
            .Equal(expected: ["movie", "episode", "special"]);

        JsonElement movieItem = items[0];
        movieItem.GetProperty(propertyName: "media_id").GetString().Should().Be(expected: "129");
        movieItem.GetProperty(propertyName: "title").GetString().Should().Be(expected: "Spirited Away");
        movieItem.GetProperty(propertyName: "play_link").GetString().Should().Be(expected: "/movie/129/watch");

        JsonElement episodeItem = items[1];
        episodeItem.GetProperty(propertyName: "media_id").GetString().Should().Be(expected: "62085");
        episodeItem
            .GetProperty(propertyName: "play_link")
            .GetString()
            .Should()
            .Be(expected: "/tv/1399/watch?season=1&episode=1");

        JsonElement specialItem = items[2];
        specialItem
            .GetProperty(propertyName: "media_id")
            .GetString()
            .Should()
            .Be(expected: NoMercyApiFactory.FavoriteSpecialId.ToString());
        specialItem.GetProperty(propertyName: "title").GetString().Should().Be(expected: "Test Special");
        specialItem
            .GetProperty(propertyName: "play_link")
            .GetString()
            .Should()
            .Be(expected: $"/specials/{NoMercyApiFactory.FavoriteSpecialId}/watch");

        string movieItemId = movieItem.GetProperty(propertyName: "id").GetString()!;
        string episodeItemId = episodeItem.GetProperty(propertyName: "id").GetString()!;
        string specialItemId = specialItem.GetProperty(propertyName: "id").GetString()!;

        // Edit metadata (partial update — only name).
        HttpResponseMessage edit = await PatchAsync(
            client: _authed,
            url: $"{BaseUrl}/{playlistId}",
            body: new { name = "Renamed Video Playlist" }
        );
        edit.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        HttpResponseMessage afterEdit = await _authed.GetAsync(requestUri: $"{BaseUrl}/{playlistId}");
        using JsonDocument afterEditDoc = JsonDocument.Parse(
            json: await afterEdit.Content.ReadAsStringAsync()
        );
        afterEditDoc
            .RootElement.GetProperty(propertyName: "data")
            .GetProperty(propertyName: "name")
            .GetString()
            .Should()
            .Be(expected: "Renamed Video Playlist");

        // Reorder: special, episode, movie.
        HttpResponseMessage reorder = await PutAsync(
            client: _authed,
            url: $"{BaseUrl}/{playlistId}/items/order",
            body: new { ordered_item_ids = new[] { specialItemId, episodeItemId, movieItemId } }
        );
        reorder.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        HttpResponseMessage afterReorder = await _authed.GetAsync(requestUri: $"{BaseUrl}/{playlistId}");
        using JsonDocument afterReorderDoc = JsonDocument.Parse(
            json: await afterReorder.Content.ReadAsStringAsync()
        );
        afterReorderDoc
            .RootElement.GetProperty(propertyName: "data")
            .GetProperty(propertyName: "items")
            .EnumerateArray()
            .Select(selector: i => i.GetProperty(propertyName: "kind").GetString())
            .Should()
            .Equal(expected: ["special", "episode", "movie"]);

        // Remove the special item.
        HttpResponseMessage remove = await _authed.DeleteAsync(
            requestUri: $"{BaseUrl}/{playlistId}/items/{specialItemId}"
        );
        remove.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        HttpResponseMessage afterRemove = await _authed.GetAsync(requestUri: $"{BaseUrl}/{playlistId}");
        using JsonDocument afterRemoveDoc = JsonDocument.Parse(
            json: await afterRemove.Content.ReadAsStringAsync()
        );
        afterRemoveDoc
            .RootElement.GetProperty(propertyName: "data")
            .GetProperty(propertyName: "items")
            .GetArrayLength()
            .Should()
            .Be(expected: 2);

        // Delete the whole playlist — cascades its remaining items.
        HttpResponseMessage delete = await _authed.DeleteAsync(requestUri: $"{BaseUrl}/{playlistId}");
        delete.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        HttpResponseMessage afterDelete = await _authed.GetAsync(requestUri: $"{BaseUrl}/{playlistId}");
        afterDelete.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OwnershipIsolation_SecondaryUser_GetsNotFound_OnPrimaryUsersPlaylist()
    {
        Guid playlistId = await CreatePlaylistAsync(name: "Owner-Only Playlist");

        (await _secondaryUser.GetAsync(requestUri: $"{BaseUrl}/{playlistId}"))
            .StatusCode.Should()
            .Be(expected: HttpStatusCode.NotFound);

        (await PatchAsync(client: _secondaryUser, url: $"{BaseUrl}/{playlistId}", body: new { name = "Hijacked" }))
            .StatusCode.Should()
            .Be(expected: HttpStatusCode.NotFound);

        (
            await PostAsync(
                client: _secondaryUser,
                url: $"{BaseUrl}/{playlistId}/items",
                body: new { kind = "movie", media_id = "129" }
            )
        )
            .StatusCode.Should()
            .Be(expected: HttpStatusCode.NotFound);

        (
            await PutAsync(
                client: _secondaryUser,
                url: $"{BaseUrl}/{playlistId}/items/order",
                body: new { ordered_item_ids = Array.Empty<string>() }
            )
        )
            .StatusCode.Should()
            .Be(expected: HttpStatusCode.NotFound);

        (await _secondaryUser.DeleteAsync(requestUri: $"{BaseUrl}/{playlistId}"))
            .StatusCode.Should()
            .Be(expected: HttpStatusCode.NotFound);

        // None of the secondary user's rejected mutation attempts touched it —
        // the owner can still see it afterward.
        (await _authed.GetAsync(requestUri: $"{BaseUrl}/{playlistId}"))
            .StatusCode.Should()
            .Be(expected: HttpStatusCode.OK);
    }

    [Fact]
    public async Task Index_SecondaryUser_DoesNotSeePrimaryUsersPlaylist()
    {
        Guid playlistId = await CreatePlaylistAsync(name: "Primary-Only Playlist For Index Isolation");

        HttpResponseMessage response = await _secondaryUser.GetAsync(requestUri: BaseUrl);
        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty(propertyName: "data")
            .EnumerateArray()
            .Select(selector: p => p.GetProperty(propertyName: "id").GetString())
            .Should()
            .NotContain(unexpected: playlistId.ToString());
    }
}
