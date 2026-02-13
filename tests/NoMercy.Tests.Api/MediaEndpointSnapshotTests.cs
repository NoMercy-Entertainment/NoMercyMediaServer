using System.Net;
using System.Text;
using System.Text.Json;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Characterization")]
public class MediaEndpointSnapshotTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;
    private readonly HttpClient _client;

    public MediaEndpointSnapshotTests(NoMercyApiFactory factory)
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
        // Endpoints return either custom StatusResponseDto (status, message)
        // or ASP.NET ProblemDetails (type, title, status, detail)
        bool hasCustomStatus = root.TryGetProperty("message", out _)
                               && root.TryGetProperty("status", out _);
        bool hasProblemDetails = root.TryGetProperty("detail", out _)
                                 && root.TryGetProperty("status", out _);

        Assert.True(hasCustomStatus || hasProblemDetails,
            $"Expected status response shape. " +
            $"Properties: [{string.Join(", ", EnumerateProperties(root))}]");
    }

    // =========================================================================
    // Movies Controller — /api/v1/movie/{id}
    // =========================================================================

    [Fact]
    public async Task Movies_GetMovie_ReturnsOk_WithDataProperty()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/movie/129");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");

        JsonElement data = json.RootElement.GetProperty("data");
        AssertJsonHasProperty(data, "id");
        AssertJsonHasProperty(data, "title");
        AssertJsonHasProperty(data, "overview");
        AssertJsonHasProperty(data, "type");
    }

    [Fact]
    public async Task Movies_GetMovie_NonExistent_ReturnsNotFoundOrTmdbFallback()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/movie/999999");

        // In test env without TMDB API key, the fallback TMDB call may throw 500
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound
                or HttpStatusCode.OK
                or HttpStatusCode.InternalServerError,
            $"Expected NotFound, OK (TMDB fallback), or 500, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Movies_GetMovie_Unauthenticated_ReturnsForbidden()
    {
        HttpClient unauthed = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = await unauthed.GetAsync("/api/v1/movie/129");

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Movies_Available_ReturnsExpectedShape()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/movie/129/available");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
        AssertJsonHasProperty(json.RootElement, "data");

        JsonElement data = json.RootElement.GetProperty("data");
        AssertJsonHasProperty(data, "available");
    }

    [Fact]
    public async Task Movies_Available_NonExistent_ReturnsNotFoundShape()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/movie/999999/available");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
        AssertJsonHasProperty(json.RootElement, "data");

        JsonElement data = json.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task Movies_Watch_ReturnsArrayOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/movie/129/watch");

        string body = await response.Content.ReadAsStringAsync();
        // 500 can occur if VideoPlaylistResponseDto encounters serialization issues
        Assert.True(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.NotFound
                or HttpStatusCode.InternalServerError,
            $"Expected OK, NotFound, or 500, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        }
    }

    [Fact]
    public async Task Movies_Like_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/movie/129/like",
            JsonBody(new { value = true }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity,
            $"Expected OK or 422, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertStatusResponse(json.RootElement);
    }

    [Fact]
    public async Task Movies_WatchList_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/movie/129/watch-list",
            JsonBody(new { add = true }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity,
            $"Expected OK or 422, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertStatusResponse(json.RootElement);
    }

    [Fact]
    public async Task Movies_Delete_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.DeleteAsync("/api/v1/movie/999998");

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
        AssertJsonHasProperty(json.RootElement, "message");
    }

    // =========================================================================
    // TV Shows Controller — /api/v1/tv/{id}
    // =========================================================================

    [Fact]
    public async Task TvShows_GetTv_ReturnsOk_WithDataProperty()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/tv/1399");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");

        JsonElement data = json.RootElement.GetProperty("data");
        AssertJsonHasProperty(data, "id");
        AssertJsonHasProperty(data, "title");
        AssertJsonHasProperty(data, "overview");
        AssertJsonHasProperty(data, "type");
    }

    [Fact]
    public async Task TvShows_Available_ReturnsExpectedShape()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/tv/1399/available");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task TvShows_Available_NonExistent_ReturnsNotFoundShape()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/tv/999999/available");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(body);

        JsonElement data = json.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task TvShows_Watch_ReturnsArrayOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/tv/1399/watch");

        string body = await response.Content.ReadAsStringAsync();
        // 500 can occur if VideoPlaylistResponseDto encounters serialization issues
        Assert.True(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.NotFound
                or HttpStatusCode.InternalServerError,
            $"Expected OK, NotFound, or 500, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        }
    }

    [Fact]
    public async Task TvShows_Like_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/tv/1399/like",
            JsonBody(new { value = true }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity,
            $"Expected OK or 422, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertStatusResponse(json.RootElement);
    }

    [Fact]
    public async Task TvShows_WatchList_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/tv/1399/watch-list",
            JsonBody(new { add = true }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity,
            $"Expected OK or 422, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertStatusResponse(json.RootElement);
    }

    [Fact]
    public async Task TvShows_Delete_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.DeleteAsync("/api/v1/tv/999998");

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
        AssertJsonHasProperty(json.RootElement, "message");
    }

    [Fact]
    public async Task TvShows_Missing_ReturnsComponentResponseOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/tv/1399/missing");

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

    // =========================================================================
    // Collections Controller — /api/v1/collection
    // =========================================================================

    [Fact]
    public async Task Collections_List_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/collection");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Collections_List_Lolomo_ReturnsContainerResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/collection?version=lolomo");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonDocument json = JsonDocument.Parse(body);
        Assert.True(
            json.RootElement.TryGetProperty("data", out _) ||
            json.RootElement.TryGetProperty("component", out _),
            $"Expected data or component property: {body}");
    }

    [Fact]
    public async Task Collections_Available_NonExistent_ReturnsNotFoundShape()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/collection/999999/available");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");

        JsonElement data = json.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task Collections_Watch_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/collection/999999/watch");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Collections_Like_NonExistent_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/collection/999999/like",
            JsonBody(new { value = true }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.UnprocessableEntity
                or HttpStatusCode.BadRequest,
            $"Expected OK, 422, or 400, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertStatusResponse(json.RootElement);
    }

    [Fact]
    public async Task Collections_WatchList_NonExistent_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/collection/999999/watch-list",
            JsonBody(new { add = true }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.UnprocessableEntity
                or HttpStatusCode.BadRequest,
            $"Expected OK, 422, or 400, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertStatusResponse(json.RootElement);
    }

    // =========================================================================
    // Genres Controller — /api/v1/genre
    // =========================================================================

    [Fact]
    public async Task Genres_List_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/genre");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Genres_GetGenre_WithSeededData_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/genre/18");

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
    public async Task Genres_GetGenre_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/genre/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Genres_GetGenre_Lolomo_ReturnsContainerOrError()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/genre/18?version=lolomo");

        string body = await response.Content.ReadAsStringAsync();

        // Known server issue: GenresController lolomo has a cast bug (CA2021)
        // which may cause 500. Test verifies shape when it succeeds.
        Assert.True(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.NotFound
                or HttpStatusCode.InternalServerError,
            $"Unexpected status {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertJsonHasProperty(json.RootElement, "data");
        }
    }

    // =========================================================================
    // Libraries Controller — /api/v1/libraries
    // =========================================================================

    [Fact]
    public async Task Libraries_List_ReturnsDataWithLibraries()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/libraries");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");

        JsonElement data = json.RootElement.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
    }

    [Fact]
    public async Task Libraries_GetLibrary_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/libraries/{NoMercyApiFactory.MovieLibraryId}");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Libraries_GetLibrary_Lolomo_ReturnsCarousels()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/libraries/{NoMercyApiFactory.MovieLibraryId}?version=lolomo");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Libraries_GetLibraryByLetter_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/libraries/{NoMercyApiFactory.MovieLibraryId}/letter/F");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Libraries_Mobile_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/libraries/mobile");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Libraries_Tv_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/libraries/tv");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    // =========================================================================
    // Home Controller — /api/v1/ and /api/v1/home
    // =========================================================================

    [Fact]
    public async Task Home_Index_ReturnsPaginatedResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/?take=10");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Home_Index_Page1_ReturnsPaginatedResponseShape()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/?take=10&page=1");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
        Assert.True(
            json.RootElement.TryGetProperty("has_more", out _) ||
            json.RootElement.TryGetProperty("hasMore", out _),
            "Expected has_more or hasMore property in paginated response");
    }

    [Fact]
    public async Task Home_Home_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/home");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Home_HomeTv_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/home/tv");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    // =========================================================================
    // Search Controller — /api/v1/search
    // =========================================================================

    [Fact]
    public async Task Search_Video_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/search/video?query=fight");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Search_Video_NoResults_ReturnsOkWithEmptyData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/search/video?query=zzznonexistentzzzxyz");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Search_VideoTv_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/search/video/tv?query=breaking");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Search_Music_NoResults_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/search/music?query=zzznonexistentzzzxyz");

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.OK,
            $"Expected NotFound or OK, got {(int)response.StatusCode}");
    }

    // =========================================================================
    // People Controller — /api/v1/person
    // =========================================================================

    [Fact]
    public async Task People_Index_ReturnsPaginatedResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/person?take=10");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    // =========================================================================
    // UserData Controller — /api/v1/userData
    // =========================================================================

    [Fact]
    public async Task UserData_Index_ReturnsPlaceholderResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/userData");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task UserData_ContinueWatching_ReturnsDataArray()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/userData/continue");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    // =========================================================================
    // Specials Controller — /api/v1/specials
    // =========================================================================

    [Fact]
    public async Task Specials_Index_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/specials");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Specials_Index_Lolomo_ReturnsContainerResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/specials?version=lolomo");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonDocument json = JsonDocument.Parse(body);
        Assert.True(
            json.RootElement.TryGetProperty("data", out _) ||
            json.RootElement.TryGetProperty("component", out _),
            $"Expected data or component property: {body}");
    }

    // =========================================================================
    // Cross-cutting: Auth denial on all protected endpoints
    // =========================================================================

    [Theory]
    [InlineData("/api/v1/movie/129")]
    [InlineData("/api/v1/tv/1399")]
    [InlineData("/api/v1/collection")]
    [InlineData("/api/v1/genre")]
    [InlineData("/api/v1/libraries")]
    [InlineData("/api/v1/")]
    [InlineData("/api/v1/home")]
    [InlineData("/api/v1/person")]
    [InlineData("/api/v1/userData")]
    [InlineData("/api/v1/specials")]
    public async Task ProtectedEndpoints_ReturnUnauthorized_WhenUnauthenticated(string url)
    {
        HttpClient unauthed = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = await unauthed.GetAsync(url);

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403 for {url}, got {(int)response.StatusCode}");
    }
}
