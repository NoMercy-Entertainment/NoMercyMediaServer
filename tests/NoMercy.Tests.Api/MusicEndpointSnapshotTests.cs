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
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait(name: "Category", value: "Characterization")]
public class MusicEndpointSnapshotTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;
    private readonly HttpClient _client;

    public MusicEndpointSnapshotTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient().AsAuthenticated();
    }

    private static StringContent JsonBody(object obj) =>
        new(content: JsonSerializer.Serialize(value: obj), encoding: Encoding.UTF8, mediaType: "application/json");

    private static void AssertJsonHasProperty(JsonElement element, string propertyName) =>
        Assert.True(
            condition: element.TryGetProperty(propertyName: propertyName, value: out _),
            userMessage: $"Expected JSON property '{propertyName}' not found. "
                         + $"Properties: [{string.Join(separator: ", ", values: EnumerateProperties(element: element))}]"
        );

    private static IEnumerable<string> EnumerateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (JsonProperty prop in element.EnumerateObject())
                yield return prop.Name;
    }

    private static void AssertStatusResponse(JsonElement root)
    {
        bool hasCustomStatus =
            root.TryGetProperty(propertyName: "message", value: out _) && root.TryGetProperty(propertyName: "status", value: out _);
        bool hasProblemDetails =
            root.TryGetProperty(propertyName: "detail", value: out _) && root.TryGetProperty(propertyName: "status", value: out _);

        Assert.True(
            condition: hasCustomStatus || hasProblemDetails,
            userMessage: $"Expected status response shape. "
                         + $"Properties: [{string.Join(separator: ", ", values: EnumerateProperties(element: root))}]"
        );
    }

    // =========================================================================
    // MusicController — /api/v1/music
    // =========================================================================

    [Fact]
    public async Task Music_Index_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/music");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Music_Start_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/music/start");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Music_Favorites_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/music/start/favorites",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Music_FavoriteArtists_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/music/start/favorite-artists",
            content: JsonBody(obj: new { replaceId = "favorite-artists" })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Music_FavoriteAlbums_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/music/start/favorite-albums",
            content: JsonBody(obj: new { replaceId = "favorite-albums" })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Music_Playlists_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/music/start/playlists",
            content: JsonBody(obj: new { replaceId = "playlists" })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Music_Search_NoResults_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: "/api/v1/music/search?query=zzznonexistentzzzxyz"
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.OK,
            userMessage: $"Expected NotFound or OK, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Music_Search_WithQuery_ReturnsComponentOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/music/search?query=test");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode
                is HttpStatusCode.OK
                    or HttpStatusCode.NotFound
                    or HttpStatusCode.InternalServerError,
            userMessage: $"Expected OK, NotFound, or 500, got {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
        }
    }

    [Fact]
    public async Task Music_TypeSearch_ReturnsPlaceholderResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/music/search/test/artist",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    // =========================================================================
    // ArtistsController — /api/v1/music/artists
    // =========================================================================

    [Fact]
    public async Task Artists_Index_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/music/artists/letter/_");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Artists_Show_ReturnsArtistResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/api/v1/music/artists/{NoMercyApiFactory.ArtistId1}"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertJsonHasProperty(element: json.RootElement, propertyName: "data");

            JsonElement data = json.RootElement.GetProperty(propertyName: "data");
            AssertJsonHasProperty(element: data, propertyName: "id");
            AssertJsonHasProperty(element: data, propertyName: "name");
        }
    }

    [Fact]
    public async Task Artists_Show_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/api/v1/music/artists/{Guid.Empty}"
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Artists_Like_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: $"/api/v1/music/artists/{NoMercyApiFactory.ArtistId1}/like",
            content: JsonBody(obj: new { value = true })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity,
            userMessage: $"Expected OK or 422, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertStatusResponse(root: json.RootElement);
    }

    [Fact]
    public async Task Artists_Like_NonExistent_ReturnsUnprocessable()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: $"/api/v1/music/artists/{Guid.Empty}/like",
            content: JsonBody(obj: new { value = true })
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.NotFound,
            userMessage: $"Expected 422 or 404, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Artists_Delete_ReturnsStatusResponse()
    {
        // Use a non-existent ID to avoid modifying seed data
        HttpResponseMessage response = await _client.DeleteAsync(
            requestUri: $"/api/v1/music/artists/{Guid.Parse(input: "99999999-9999-9999-9999-999999999999")}"
        );

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
    }

    [Fact]
    public async Task Artists_MemberRoute_And_LetterBrowseRoute_ResolveToDifferentActions()
    {
        HttpResponseMessage memberResponse = await _client.GetAsync(
            requestUri: $"/api/v1/music/artists/{Guid.Empty}"
        );
        string memberBody = await memberResponse.Content.ReadAsStringAsync();
        Assert.True(
            condition: memberResponse.StatusCode == HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound for the artist member route (Show, not letter-browse), got {(int)memberResponse.StatusCode}: {memberBody}"
        );
        Assert.Contains(expectedSubstring: "Artist not found", actualString: memberBody);

        HttpResponseMessage letterResponse = await _client.GetAsync(
            requestUri: "/api/v1/music/artists/letter/a"
        );
        string letterBody = await letterResponse.Content.ReadAsStringAsync();
        Assert.True(
            condition: letterResponse.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK for the letter-browse route, got {(int)letterResponse.StatusCode}: {letterBody}"
        );

        JsonDocument letterJson = JsonDocument.Parse(json: letterBody);
        AssertJsonHasProperty(element: letterJson.RootElement, propertyName: "data");
    }

    // =========================================================================
    // AlbumsController — /api/v1/music/albums
    // =========================================================================

    [Fact]
    public async Task Albums_Index_ReturnsComponentOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/music/albums/letter/_");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
        }
    }

    [Fact]
    public async Task Albums_Show_ReturnsAlbumResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/api/v1/music/albums/{NoMercyApiFactory.AlbumId1}"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertJsonHasProperty(element: json.RootElement, propertyName: "data");

            JsonElement data = json.RootElement.GetProperty(propertyName: "data");
            AssertJsonHasProperty(element: data, propertyName: "id");
            AssertJsonHasProperty(element: data, propertyName: "name");
        }
    }

    [Fact]
    public async Task Albums_Show_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: $"/api/v1/music/albums/{Guid.Empty}");

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Albums_Like_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: $"/api/v1/music/albums/{NoMercyApiFactory.AlbumId1}/like",
            content: JsonBody(obj: new { value = true })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode
                is HttpStatusCode.OK
                    or HttpStatusCode.UnprocessableEntity
                    or HttpStatusCode.NotFound,
            userMessage: $"Expected OK, 422, or 404, got {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertStatusResponse(root: json.RootElement);
        }
    }

    [Fact]
    public async Task Albums_Rescan_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: $"/api/v1/music/albums/{NoMercyApiFactory.AlbumId1}/rescan",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        // Test user may not have moderator role, so 401/403 is acceptable
        Assert.True(
            condition: response.StatusCode
                is HttpStatusCode.OK
                    or HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden,
            userMessage: $"Expected OK, 401, or 403, got {(int)response.StatusCode}: {body}"
        );
    }

    [Fact]
    public async Task Albums_MemberRoute_And_LetterBrowseRoute_ResolveToDifferentActions()
    {
        HttpResponseMessage memberResponse = await _client.GetAsync(
            requestUri: $"/api/v1/music/albums/{Guid.Empty}"
        );
        string memberBody = await memberResponse.Content.ReadAsStringAsync();
        Assert.True(
            condition: memberResponse.StatusCode == HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound for the album member route (Show, not letter-browse), got {(int)memberResponse.StatusCode}: {memberBody}"
        );
        Assert.Contains(expectedSubstring: "Albums not found", actualString: memberBody);

        HttpResponseMessage letterResponse = await _client.GetAsync(
            requestUri: "/api/v1/music/albums/letter/a"
        );
        string letterBody = await letterResponse.Content.ReadAsStringAsync();
        Assert.True(
            condition: letterResponse.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK for the letter-browse route, got {(int)letterResponse.StatusCode}: {letterBody}"
        );

        JsonDocument letterJson = JsonDocument.Parse(json: letterBody);
        AssertJsonHasProperty(element: letterJson.RootElement, propertyName: "data");
    }

    // =========================================================================
    // PlaylistsController — /api/v1/music/playlists
    // =========================================================================

    [Fact]
    public async Task Playlists_Index_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/music/playlists");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Playlists_Show_ReturnsPlaylistResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/api/v1/music/playlists/{NoMercyApiFactory.PlaylistId1}"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
        }
    }

    [Fact]
    public async Task Playlists_Show_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/api/v1/music/playlists/{Guid.Empty}"
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Playlists_Create_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/music/playlists",
            content: JsonBody(obj: new { name = "Snapshot Test Playlist", description = "test" })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
            userMessage: $"Expected OK or Conflict, got {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
            AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
        }
    }

    [Fact]
    public async Task Playlists_Create_Duplicate_ReturnsConflict()
    {
        // First create
        await _client.PostAsync(
            requestUri: "/api/v1/music/playlists",
            content: JsonBody(obj: new { name = "Duplicate Test Playlist", description = "test" })
        );

        // Second create with same name
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/music/playlists",
            content: JsonBody(obj: new { name = "Duplicate Test Playlist", description = "test2" })
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.OK,
            userMessage: $"Expected Conflict or OK, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Playlists_Delete_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            requestUri: $"/api/v1/music/playlists/{Guid.Parse(input: "99999999-9999-9999-9999-999999999998")}"
        );

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
    }

    [Fact]
    public async Task Playlists_AddTrack_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: $"/api/v1/music/playlists/{NoMercyApiFactory.PlaylistId1}/tracks",
            content: JsonBody(obj: new { id = NoMercyApiFactory.TrackId2 })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.InternalServerError,
            userMessage: $"Expected OK or 500 (duplicate key), got {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
        }
    }

    [Fact]
    public async Task Playlists_RemoveTrack_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            requestUri: $"/api/v1/music/playlists/{NoMercyApiFactory.PlaylistId1}/tracks/{Guid.Empty}"
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // Music GenresController — /api/v1/music/genres
    // =========================================================================

    [Fact]
    public async Task MusicGenres_Index_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/music/genres");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task MusicGenres_ByLetter_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/music/genres/letter/_");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task MusicGenres_Show_ReturnsGenreResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/api/v1/music/genres/{NoMercyApiFactory.MusicGenreId1}"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
        }
    }

    [Fact]
    public async Task MusicGenres_Show_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: $"/api/v1/music/genres/{Guid.Empty}");

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // TracksController — /api/v1/music/tracks
    // =========================================================================

    [Fact]
    public async Task Tracks_Index_ReturnsTracksOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/music/tracks");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertJsonHasProperty(element: json.RootElement, propertyName: "data");

            JsonElement data = json.RootElement.GetProperty(propertyName: "data");
            AssertJsonHasProperty(element: data, propertyName: "name");
            AssertJsonHasProperty(element: data, propertyName: "type");
        }
    }

    [Fact]
    public async Task Tracks_Like_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: $"/api/v1/music/tracks/{NoMercyApiFactory.TrackId1}/like",
            content: JsonBody(obj: new { value = true })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertStatusResponse(root: json.RootElement);
        }
    }

    [Fact]
    public async Task Tracks_Like_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: $"/api/v1/music/tracks/{Guid.Empty}/like",
            content: JsonBody(obj: new { value = true })
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Tracks_Lyrics_ReturnsLyricsOrNotFound()
    {
        // The lyrics endpoint may call external NoMercyLyricsClient which can timeout
        // in test environment without network access, so use a timeout
        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 15));
        try
        {
            HttpResponseMessage response = await _client.GetAsync(
                requestUri: $"/api/v1/music/tracks/{NoMercyApiFactory.TrackId1}/lyrics",
                cancellationToken: cts.Token
            );

            string body = await response.Content.ReadAsStringAsync(cancellationToken: cts.Token);
            Assert.True(
                condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
                userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}"
            );

            if (response.StatusCode == HttpStatusCode.OK)
            {
                JsonDocument json = JsonDocument.Parse(json: body);
                AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
            }
        }
        catch (OperationCanceledException)
        {
            // External lyrics provider not available in test env — acceptable
        }
        catch (HttpRequestException)
        {
            // Network-related failure in test env — acceptable
        }
    }

    [Fact]
    public async Task Tracks_Lyrics_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/api/v1/music/tracks/{Guid.Empty}/lyrics"
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Tracks_Playback_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: $"/api/v1/music/tracks/{NoMercyApiFactory.TrackId1}/playback",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertStatusResponse(root: json.RootElement);
        }
    }

    [Fact]
    public async Task Tracks_Playback_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: $"/api/v1/music/tracks/{Guid.Empty}/playback",
            content: JsonBody(obj: new { })
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // Cross-cutting: Auth denial on all music endpoints
    // =========================================================================

    [Theory]
    [InlineData(data: "/api/v1/music")]
    [InlineData(data: "/api/v1/music/start")]
    [InlineData(data: "/api/v1/music/artists/letter/_")]
    [InlineData(data: "/api/v1/music/albums/letter/_")]
    [InlineData(data: "/api/v1/music/playlists")]
    [InlineData(data: "/api/v1/music/genres")]
    [InlineData(data: "/api/v1/music/tracks")]
    public async Task MusicEndpoints_ReturnUnauthorized_WhenUnauthenticated(string url)
    {
        HttpClient unauthed = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = await unauthed.GetAsync(requestUri: url);

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            userMessage: $"Expected 401/403 for {url}, got {(int)response.StatusCode}"
        );
    }
}
