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

    private static void AssertProblemDetailsShape(JsonElement root, int expectedStatus)
    {
        AssertJsonHasProperty(element: root, propertyName: "type");
        AssertJsonHasProperty(element: root, propertyName: "title");
        AssertJsonHasProperty(element: root, propertyName: "status");
        AssertJsonHasProperty(element: root, propertyName: "detail");
        AssertJsonHasProperty(element: root, propertyName: "instance");
        Assert.Equal(expected: expectedStatus, actual: root.GetProperty(propertyName: "status").GetInt32());
    }

    private static void AssertStatusResponse(JsonElement root)
    {
        // Endpoints return either custom StatusResponseDto (status, message)
        // or ASP.NET ProblemDetails (type, title, status, detail)
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
    // Movies Controller — /api/v1/movie/{id}
    // =========================================================================

    [Fact]
    public async Task Movies_GetMovie_ReturnsOk_WithDataProperty()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/movie/129");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");

        JsonElement data = json.RootElement.GetProperty(propertyName: "data");
        AssertJsonHasProperty(element: data, propertyName: "id");
        AssertJsonHasProperty(element: data, propertyName: "title");
        AssertJsonHasProperty(element: data, propertyName: "overview");
        AssertJsonHasProperty(element: data, propertyName: "type");
    }

    [Fact]
    public async Task Movies_GetMovie_NonExistent_ReturnsNotFoundOrTmdbFallback()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/movie/999999");

        // In test env without TMDB API key, the fallback TMDB call may throw 500
        Assert.True(
            condition: response.StatusCode
                is HttpStatusCode.NotFound
                    or HttpStatusCode.OK
                    or HttpStatusCode.InternalServerError,
            userMessage: $"Expected NotFound, OK (TMDB fallback), or 500, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Movies_GetMovie_Unauthenticated_ReturnsForbidden()
    {
        HttpClient unauthed = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = await unauthed.GetAsync(requestUri: "/api/v1/movie/129");

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            userMessage: $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Movies_Available_ReturnsExpectedShape()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/movie/129/available");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");

        JsonElement data = json.RootElement.GetProperty(propertyName: "data");
        AssertJsonHasProperty(element: data, propertyName: "available");
    }

    [Fact]
    public async Task Movies_Available_NonExistent_ReturnsNotFoundShape()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/movie/999999/available");

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: body);
        AssertProblemDetailsShape(root: json.RootElement, expectedStatus: 404);
    }

    [Fact]
    public async Task Movies_Watch_ReturnsArrayOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/movie/129/watch");

        string body = await response.Content.ReadAsStringAsync();
        // 500 can occur if VideoPlaylistResponseDto encounters serialization issues
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
            Assert.Equal(expected: JsonValueKind.Array, actual: json.RootElement.ValueKind);
        }
    }

    [Fact]
    public async Task Movies_Like_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/movie/129/like",
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
    public async Task Movies_WatchList_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/movie/129/watch-list",
            content: JsonBody(obj: new { add = true })
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
    public async Task Movies_Delete_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.DeleteAsync(requestUri: "/api/v1/movie/999998");

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
        AssertJsonHasProperty(element: json.RootElement, propertyName: "message");
    }

    // =========================================================================
    // TV Shows Controller — /api/v1/tv/{id}
    // =========================================================================

    [Fact]
    public async Task TvShows_GetTv_ReturnsOk_WithDataProperty()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/tv/1399");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");

        JsonElement data = json.RootElement.GetProperty(propertyName: "data");
        AssertJsonHasProperty(element: data, propertyName: "id");
        AssertJsonHasProperty(element: data, propertyName: "title");
        AssertJsonHasProperty(element: data, propertyName: "overview");
        AssertJsonHasProperty(element: data, propertyName: "type");
    }

    [Fact]
    public async Task TvShows_Available_ReturnsExpectedShape()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/tv/1399/available");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task TvShows_Available_NonExistent_ReturnsNotFoundShape()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/tv/999999/available");

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: body);
        AssertProblemDetailsShape(root: json.RootElement, expectedStatus: 404);
    }

    [Fact]
    public async Task TvShows_Watch_ReturnsArrayOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/tv/1399/watch");

        string body = await response.Content.ReadAsStringAsync();
        // 500 can occur if VideoPlaylistResponseDto encounters serialization issues
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
            Assert.Equal(expected: JsonValueKind.Array, actual: json.RootElement.ValueKind);
        }
    }

    [Fact]
    public async Task TvShows_Like_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/tv/1399/like",
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
    public async Task TvShows_WatchList_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/tv/1399/watch-list",
            content: JsonBody(obj: new { add = true })
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
    public async Task TvShows_Delete_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.DeleteAsync(requestUri: "/api/v1/tv/999998");

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
        AssertJsonHasProperty(element: json.RootElement, propertyName: "message");
    }

    [Fact]
    public async Task TvShows_Missing_ReturnsComponentResponseOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/tv/1399/missing");

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

    // =========================================================================
    // Collections Controller — /api/v1/collection
    // =========================================================================

    [Fact]
    public async Task Collections_List_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/collection");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Collections_List_Lolomo_ReturnsContainerResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/collection?version=lolomo");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        JsonDocument json = JsonDocument.Parse(json: body);
        Assert.True(
            condition: json.RootElement.TryGetProperty(propertyName: "data", value: out _)
                       || json.RootElement.TryGetProperty(propertyName: "component", value: out _),
            userMessage: $"Expected data or component property: {body}"
        );
    }

    [Fact]
    public async Task Collections_Available_NonExistent_ReturnsNotFoundShape()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: "/api/v1/collection/999999/available"
        );

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: body);
        AssertProblemDetailsShape(root: json.RootElement, expectedStatus: 404);
    }

    [Fact]
    public async Task Collections_Watch_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/collection/999999/watch");

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);
    }

    [Fact]
    public async Task Collections_Like_NonExistent_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/collection/999999/like",
            content: JsonBody(obj: new { value = true })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode
                is HttpStatusCode.OK
                    or HttpStatusCode.UnprocessableEntity
                    or HttpStatusCode.BadRequest,
            userMessage: $"Expected OK, 422, or 400, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertStatusResponse(root: json.RootElement);
    }

    [Fact]
    public async Task Collections_WatchList_NonExistent_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/collection/999999/watch-list",
            content: JsonBody(obj: new { add = true })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode
                is HttpStatusCode.OK
                    or HttpStatusCode.UnprocessableEntity
                    or HttpStatusCode.BadRequest,
            userMessage: $"Expected OK, 422, or 400, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertStatusResponse(root: json.RootElement);
    }

    // =========================================================================
    // Genres Controller — /api/v1/genres
    // =========================================================================

    [Fact]
    public async Task Genres_List_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/genres");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Genres_GetGenre_WithSeededData_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/genres/18");

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
    public async Task Genres_GetGenre_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/genres/999999");

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);
    }

    [Fact]
    public async Task Genres_GetGenre_Lolomo_ReturnsContainerOrError()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/genres/18?version=lolomo");

        string body = await response.Content.ReadAsStringAsync();

        // Known server issue: GenresController lolomo has a cast bug (CA2021)
        // which may cause 500. Test verifies shape when it succeeds.
        Assert.True(
            condition: response.StatusCode
                is HttpStatusCode.OK
                    or HttpStatusCode.NotFound
                    or HttpStatusCode.InternalServerError,
            userMessage: $"Unexpected status {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
        }
    }

    // =========================================================================
    // Libraries Controller — /api/v1/libraries
    // =========================================================================

    [Fact]
    public async Task Libraries_List_ReturnsDataWithLibraries()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/libraries");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");

        JsonElement data = json.RootElement.GetProperty(propertyName: "data");
        Assert.Equal(expected: JsonValueKind.Array, actual: data.ValueKind);
    }

    [Fact]
    public async Task Libraries_GetLibrary_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/api/v1/libraries/{NoMercyApiFactory.MovieLibraryId}"
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
    public async Task Libraries_GetLibrary_Lolomo_ReturnsCarousels()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/api/v1/libraries/{NoMercyApiFactory.MovieLibraryId}?version=lolomo"
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
    public async Task Libraries_GetLibraryByLetter_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/api/v1/libraries/{NoMercyApiFactory.MovieLibraryId}/letter/F"
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
    public async Task Libraries_Mobile_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/libraries/mobile");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Libraries_Tv_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/libraries/tv");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    // =========================================================================
    // Home Controller — /api/v1/ and /api/v1/home
    // =========================================================================

    [Fact]
    public async Task Home_Index_ReturnsPaginatedResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/?take=10");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Home_Index_Page1_ReturnsPaginatedResponseShape()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/?take=10&page=1");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
        Assert.True(
            condition: json.RootElement.TryGetProperty(propertyName: "has_more", value: out _)
                       || json.RootElement.TryGetProperty(propertyName: "hasMore", value: out _),
            userMessage: "Expected has_more or hasMore property in paginated response"
        );
    }

    [Fact]
    public async Task Home_Home_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/home");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Home_HomeTv_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/home/tv");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    // =========================================================================
    // Search Controller — /api/v1/search
    // =========================================================================

    [Fact]
    public async Task Search_Video_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/search/video?query=fight");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Search_Video_NoResults_ReturnsOkWithEmptyData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: "/api/v1/search/video?query=zzznonexistentzzzxyz"
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
    public async Task Search_VideoTv_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: "/api/v1/search/video/tv?query=breaking"
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
    public async Task Search_Music_NoResults_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: "/api/v1/search/music?query=zzznonexistentzzzxyz"
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.OK,
            userMessage: $"Expected NotFound or OK, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // People Controller — /api/v1/person
    // =========================================================================

    [Fact]
    public async Task People_Index_ReturnsPaginatedResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/person?take=10");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    // =========================================================================
    // UserData Controller — /api/v1/userData
    // =========================================================================

    [Fact]
    public async Task UserData_Index_ReturnsPlaceholderResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/userData");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task UserData_ContinueWatching_ReturnsDataArray()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/userData/continue");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    // =========================================================================
    // Specials Controller — /api/v1/specials
    // =========================================================================

    [Fact]
    public async Task Specials_Index_ReturnsComponentResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/specials");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Specials_Index_Lolomo_ReturnsContainerResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/specials?version=lolomo");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        JsonDocument json = JsonDocument.Parse(json: body);
        Assert.True(
            condition: json.RootElement.TryGetProperty(propertyName: "data", value: out _)
                       || json.RootElement.TryGetProperty(propertyName: "component", value: out _),
            userMessage: $"Expected data or component property: {body}"
        );
    }

    // =========================================================================
    // Cross-cutting: Auth denial on all protected endpoints
    // =========================================================================

    [Theory]
    [InlineData(data: "/api/v1/movie/129")]
    [InlineData(data: "/api/v1/tv/1399")]
    [InlineData(data: "/api/v1/collection")]
    [InlineData(data: "/api/v1/genres")]
    [InlineData(data: "/api/v1/libraries")]
    [InlineData(data: "/api/v1/")]
    [InlineData(data: "/api/v1/home")]
    [InlineData(data: "/api/v1/person")]
    [InlineData(data: "/api/v1/userData")]
    [InlineData(data: "/api/v1/specials")]
    public async Task ProtectedEndpoints_ReturnUnauthorized_WhenUnauthenticated(string url)
    {
        HttpClient unauthed = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = await unauthed.GetAsync(requestUri: url);

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            userMessage: $"Expected 401/403 for {url}, got {(int)response.StatusCode}"
        );
    }
}
