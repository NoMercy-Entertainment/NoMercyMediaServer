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

namespace NoMercy.Tests.Api.Media;

[Trait("Category", "MediaLibraries")]
public class LibrariesControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public LibrariesControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    [Fact]
    public async Task GetLibraries_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1/libraries");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetLibraries_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/libraries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetLibraries_ReturnsEnvelopeWithDataArray_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/libraries");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("response envelope must contain a 'data' property");
        data.ValueKind.Should().Be(JsonValueKind.Array, "data must be an array");
    }

    [Fact]
    public async Task GetLibraries_DataItems_ContainRequiredFields()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/libraries");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");
        data.GetArrayLength().Should().BeGreaterThan(0, "seed data includes at least 3 libraries");

        JsonElement firstItem = data.EnumerateArray().First();

        firstItem.TryGetProperty("id", out _).Should().BeTrue("each library must expose 'id'");
        firstItem
            .TryGetProperty("title", out _)
            .Should()
            .BeTrue("each library must expose 'title'");
        firstItem.TryGetProperty("type", out _).Should().BeTrue("each library must expose 'type'");
        firstItem
            .TryGetProperty("order", out _)
            .Should()
            .BeTrue("each library must expose 'order'");
        firstItem.TryGetProperty("link", out _).Should().BeTrue("each library must expose 'link'");
    }

    [Fact]
    public async Task GetLibraries_DataItems_TypeFieldIsKnownMediaType()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/libraries");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");

        string[] knownTypes = ["movie", "tv", "music", "anime"];

        foreach (JsonElement item in data.EnumerateArray())
        {
            string? typeValue = item.GetProperty("type").GetString();
            knownTypes
                .Should()
                .Contain(typeValue, $"library type '{typeValue}' must be a known media type");
        }
    }

    [Fact]
    public async Task GetMobileLibraries_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1/libraries/mobile");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMobileLibraries_ReturnsOkWithComponentEnvelope_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/libraries/mobile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("mobile response must have a 'data' property");
        data.ValueKind.Should().Be(JsonValueKind.Array, "mobile data must be a component array");
    }

    [Fact]
    public async Task GetTvLibraries_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1/libraries/tv");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTvLibraries_ReturnsOkWithComponentEnvelope_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/libraries/tv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("TV response must have a 'data' property");
        data.ValueKind.Should().Be(JsonValueKind.Array, "TV data must be a component array");
    }

    [Fact]
    public async Task GetLibraryById_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            $"/api/v1/libraries/{NoMercyApiFactory.MovieLibraryId}"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetLibraryById_ReturnsOkWithComponentEnvelope_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"/api/v1/libraries/{NoMercyApiFactory.MovieLibraryId}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("library-by-id response must have a 'data' property");
    }

    [Fact]
    public async Task GetLibraryByLetter_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            $"/api/v1/libraries/{NoMercyApiFactory.MovieLibraryId}/letter/A"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetLibraryByLetter_ReturnsOkWithComponentEnvelope_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"/api/v1/libraries/{NoMercyApiFactory.MovieLibraryId}/letter/S"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out _)
            .Should()
            .BeTrue("letter-filtered library response must have a 'data' property");
    }

    // GET /api/v1/libraries/{libraryId}/import-failures — auth pair

    [Fact]
    public async Task GetImportFailures_ReturnsOkWithDataEnvelope_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"/api/v1/libraries/{NoMercyApiFactory.MovieLibraryId}/import-failures"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("data", out JsonElement data)
            .Should()
            .BeTrue("import-failures response envelope must contain a 'data' property");
        data.ValueKind.Should().Be(JsonValueKind.Array, "'data' must be an array");
    }

    [Fact]
    public async Task GetImportFailures_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            $"/api/v1/libraries/{NoMercyApiFactory.MovieLibraryId}/import-failures"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }
}
