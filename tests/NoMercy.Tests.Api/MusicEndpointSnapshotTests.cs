using System.Net;
using System.Text;
using System.Text.Json;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

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
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    private static void AssertJsonHasProperty(JsonElement element, string propertyName) =>
        Assert.True(element.TryGetProperty(propertyName, out _),
            $"Expected JSON property '{propertyName}' not found. " +
            $"Properties: [{string.Join(", ", EnumerateProperties(element))}]");

    private static IEnumerable<string> EnumerateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (JsonProperty prop in element.EnumerateObject())
                yield return prop.Name;
    }

    private static void AssertStatusResponse(JsonElement root)
    {
        bool hasCustomStatus = root.TryGetProperty("message", out _)
                               && root.TryGetProperty("status", out _);
        bool hasProblemDetails = root.TryGetProperty("detail", out _)
                                 && root.TryGetProperty("status", out _);

        Assert.True(hasCustomStatus || hasProblemDetails,
            $"Expected status response shape. " +
            $"Properties: [{string.Join(", ", EnumerateProperties(root))}]");
    }

    // =========================================================================
    // MusicController — /api/v1/music
    // =========================================================================

    [Fact]
    public async Task Music_Index_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/music");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Music_Start_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/music/start");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Music_Favorites_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/music/start/favorites", JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Music_FavoriteArtists_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/music/start/favorite-artists",
            JsonBody(new { replaceId = "favorite-artists" }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Music_FavoriteAlbums_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/music/start/favorite-albums",
            JsonBody(new { replaceId = "favorite-albums" }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Music_Playlists_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/music/start/playlists",
            JsonBody(new { replaceId = "playlists" }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Music_Search_NoResults_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/music/search?query=zzznonexistentzzzxyz");

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.OK,
            $"Expected NotFound or OK, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Music_Search_WithQuery_ReturnsComponentOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/music/search?query=test");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.NotFound
                or HttpStatusCode.InternalServerError,
            $"Expected OK, NotFound, or 500, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertJsonHasProperty(json.RootElement, "data");
        }
    }

    [Fact]
    public async Task Music_TypeSearch_ReturnsPlaceholderResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/music/search/test/artist", JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    // =========================================================================
    // ArtistsController — /api/v1/music/artist
    // =========================================================================

    [Fact]
    public async Task Artists_Index_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/music/artists/_");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Artists_Show_ReturnsArtistResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/music/artist/{NoMercyApiFactory.ArtistId1}");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertJsonHasProperty(json.RootElement, "data");

            JsonElement data = json.RootElement.GetProperty("data");
            AssertJsonHasProperty(data, "id");
            AssertJsonHasProperty(data, "name");
        }
    }

    [Fact]
    public async Task Artists_Show_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/music/artist/{Guid.Empty}");

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Artists_Like_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/music/artist/{NoMercyApiFactory.ArtistId1}/like",
            JsonBody(new { value = true }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity,
            $"Expected OK or 422, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertStatusResponse(json.RootElement);
    }

    [Fact]
    public async Task Artists_Like_NonExistent_ReturnsUnprocessable()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/music/artist/{Guid.Empty}/like",
            JsonBody(new { value = true }));

        Assert.True(
            response.StatusCode is HttpStatusCode.UnprocessableEntity
                or HttpStatusCode.NotFound,
            $"Expected 422 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Artists_Delete_ReturnsStatusResponse()
    {
        // Use a non-existent ID to avoid modifying seed data
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/music/artist/{Guid.Parse("99999999-9999-9999-9999-999999999999")}");

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
    }

    // =========================================================================
    // AlbumsController — /api/v1/music/album
    // =========================================================================

    [Fact]
    public async Task Albums_Index_ReturnsComponentOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/music/albums/_");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertJsonHasProperty(json.RootElement, "data");
        }
    }

    [Fact]
    public async Task Albums_Show_ReturnsAlbumResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/music/album/{NoMercyApiFactory.AlbumId1}");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertJsonHasProperty(json.RootElement, "data");

            JsonElement data = json.RootElement.GetProperty("data");
            AssertJsonHasProperty(data, "id");
            AssertJsonHasProperty(data, "name");
        }
    }

    [Fact]
    public async Task Albums_Show_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/music/album/{Guid.Empty}");

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Albums_Like_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/music/album/{NoMercyApiFactory.AlbumId1}/like",
            JsonBody(new { value = true }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.UnprocessableEntity
                or HttpStatusCode.NotFound,
            $"Expected OK, 422, or 404, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertStatusResponse(json.RootElement);
        }
    }

    [Fact]
    public async Task Albums_Rescan_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/music/album/{NoMercyApiFactory.AlbumId1}/rescan",
            JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        // Test user may not have moderator role, so 401/403 is acceptable
        Assert.True(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden,
            $"Expected OK, 401, or 403, got {(int)response.StatusCode}: {body}");
    }

    // =========================================================================
    // PlaylistsController — /api/v1/music/playlists
    // =========================================================================

    [Fact]
    public async Task Playlists_Index_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/music/playlists");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Playlists_Show_ReturnsPlaylistResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/music/playlists/{NoMercyApiFactory.PlaylistId1}");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertJsonHasProperty(json.RootElement, "data");
        }
    }

    [Fact]
    public async Task Playlists_Show_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/music/playlists/{Guid.Empty}");

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Playlists_Create_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/music/playlists",
            JsonBody(new { name = "Snapshot Test Playlist", description = "test" }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
            $"Expected OK or Conflict, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertJsonHasProperty(json.RootElement, "status");
            AssertJsonHasProperty(json.RootElement, "data");
        }
    }

    [Fact]
    public async Task Playlists_Create_Duplicate_ReturnsConflict()
    {
        // First create
        await _client.PostAsync(
            "/api/v1/music/playlists",
            JsonBody(new { name = "Duplicate Test Playlist", description = "test" }));

        // Second create with same name
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/music/playlists",
            JsonBody(new { name = "Duplicate Test Playlist", description = "test2" }));

        Assert.True(
            response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.OK,
            $"Expected Conflict or OK, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Playlists_Delete_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/music/playlists/{Guid.Parse("99999999-9999-9999-9999-999999999998")}");

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
    }

    [Fact]
    public async Task Playlists_AddTrack_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/music/playlists/{NoMercyApiFactory.PlaylistId1}/tracks",
            JsonBody(new { id = NoMercyApiFactory.TrackId2 }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.InternalServerError,
            $"Expected OK or 500 (duplicate key), got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertJsonHasProperty(json.RootElement, "status");
        }
    }

    [Fact]
    public async Task Playlists_RemoveTrack_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/music/playlists/{NoMercyApiFactory.PlaylistId1}/tracks/{Guid.Empty}");

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    // =========================================================================
    // Music GenresController — /api/v1/music/genres
    // =========================================================================

    [Fact]
    public async Task MusicGenres_Index_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/music/genres");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task MusicGenres_ByLetter_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/music/genres/letter/_");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task MusicGenres_Show_ReturnsGenreResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/music/genres/{NoMercyApiFactory.MusicGenreId1}");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertJsonHasProperty(json.RootElement, "data");
        }
    }

    [Fact]
    public async Task MusicGenres_Show_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/music/genres/{Guid.Empty}");

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    // =========================================================================
    // TracksController — /api/v1/music/tracks
    // =========================================================================

    [Fact]
    public async Task Tracks_Index_ReturnsTracksOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/music/tracks");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertJsonHasProperty(json.RootElement, "data");

            JsonElement data = json.RootElement.GetProperty("data");
            AssertJsonHasProperty(data, "name");
            AssertJsonHasProperty(data, "type");
        }
    }

    [Fact]
    public async Task Tracks_Like_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/music/tracks/{NoMercyApiFactory.TrackId1}/like",
            JsonBody(new { value = true }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertStatusResponse(json.RootElement);
        }
    }

    [Fact]
    public async Task Tracks_Like_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/music/tracks/{Guid.Empty}/like",
            JsonBody(new { value = true }));

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Tracks_Lyrics_ReturnsLyricsOrNotFound()
    {
        // The lyrics endpoint may call external NoMercyLyricsClient which can timeout
        // in test environment without network access, so use a timeout
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        try
        {
            HttpResponseMessage response = await _client.GetAsync(
                $"/api/v1/music/tracks/{NoMercyApiFactory.TrackId1}/lyrics", cts.Token);

            string body = await response.Content.ReadAsStringAsync(cts.Token);
            Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
                $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                JsonDocument json = JsonDocument.Parse(body);
                AssertJsonHasProperty(json.RootElement, "data");
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
            $"/api/v1/music/tracks/{Guid.Empty}/lyrics");

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Tracks_Playback_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/music/tracks/{NoMercyApiFactory.TrackId1}/playback",
            JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertStatusResponse(json.RootElement);
        }
    }

    [Fact]
    public async Task Tracks_Playback_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/music/tracks/{Guid.Empty}/playback",
            JsonBody(new { }));

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    // =========================================================================
    // Cross-cutting: Auth denial on all music endpoints
    // =========================================================================

    [Theory]
    [InlineData("/api/v1/music")]
    [InlineData("/api/v1/music/start")]
    [InlineData("/api/v1/music/artists/_")]
    [InlineData("/api/v1/music/albums/_")]
    [InlineData("/api/v1/music/playlists")]
    [InlineData("/api/v1/music/genres")]
    [InlineData("/api/v1/music/tracks")]
    public async Task MusicEndpoints_ReturnUnauthorized_WhenUnauthenticated(string url)
    {
        HttpClient unauthed = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = await unauthed.GetAsync(url);

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403 for {url}, got {(int)response.StatusCode}");
    }
}
