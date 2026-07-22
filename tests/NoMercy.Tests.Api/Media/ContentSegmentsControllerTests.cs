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
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Media;

/// <summary>
/// Contract tests for <c>api/v1/content-segments</c> — the dashboard/player
/// surface for intro/outro/recap/credits timeline annotations. Locks the
/// Moderator-vs-MediaAccess auth split, the paginated-list envelope
/// (including the pageSize clamp), and the Create validation rules.
/// </summary>
[Trait(name: "Category", value: "ContentSegments")]
public class ContentSegmentsControllerTests : IClassFixture<NoMercyApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    // Seeded by NoMercyApiFactory.SeedMediaData.
    private const int SeededMovieId = 129;
    private const int SeededEpisodeId = 62085;

    private Ulid _introSegmentId;
    private Ulid _outroSegmentId;
    private Ulid _episodeSegmentId;

    public ContentSegmentsControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    public async Task InitializeAsync()
    {
        _introSegmentId = Ulid.NewUlid();
        _outroSegmentId = Ulid.NewUlid();
        _episodeSegmentId = Ulid.NewUlid();

        await using MediaContext ctx = new();
        ctx.ContentSegments.AddRange(entities:
            [
                new ContentSegment
                {
                    Id = _introSegmentId,
                    MovieId = SeededMovieId,
                    SegmentType = ContentSegmentType.Intro,
                    StartSeconds = 0,
                    EndSeconds = 90,
                    Source = "detector",
                },
                new ContentSegment
                {
                    Id = _outroSegmentId,
                    MovieId = SeededMovieId,
                    SegmentType = ContentSegmentType.Outro,
                    StartSeconds = 5400,
                    EndSeconds = 5460,
                    Source = "detector",
                },
                new ContentSegment
                {
                    Id = _episodeSegmentId,
                    EpisodeId = SeededEpisodeId,
                    SegmentType = ContentSegmentType.Intro,
                    StartSeconds = 5,
                    EndSeconds = 35,
                    Source = "detector",
                }
            ]
        );
        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using MediaContext ctx = new();
        Ulid[] ids = [_introSegmentId, _outroSegmentId, _episodeSegmentId];
        await ctx.ContentSegments.Where(predicate: s => ids.Contains(s.Id)).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task List_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: "/api/v1/content-segments");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task List_ReturnsEnvelopeWithDataAndMeta_WhenModerator()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/content-segments");

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement root = doc.RootElement;

        root.TryGetProperty(propertyName: "data", value: out JsonElement data).Should().BeTrue();
        data.ValueKind.Should().Be(expected: JsonValueKind.Array);

        root.TryGetProperty(propertyName: "meta", value: out JsonElement meta).Should().BeTrue();
        meta.TryGetProperty(propertyName: "total", value: out _).Should().BeTrue();
        meta.TryGetProperty(propertyName: "pageSize", value: out _).Should().BeTrue();
        meta.TryGetProperty(propertyName: "pageIndex", value: out _).Should().BeTrue();
        meta.TryGetProperty(propertyName: "totalPages", value: out _).Should().BeTrue();

        bool containsSeededIntro = data.EnumerateArray()
            .Any(predicate: e => e.GetProperty(propertyName: "id").GetString() == _introSegmentId.ToString());
        containsSeededIntro.Should().BeTrue(because: "the seeded intro segment must be visible in the list");
    }

    [Fact]
    public async Task List_PageSizeAboveMax_IsClampedTo500()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: "/api/v1/content-segments?pageSize=999999"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.GetProperty(propertyName: "meta").GetProperty(propertyName: "pageSize").GetInt32().Should().Be(expected: 500);
    }

    [Fact]
    public async Task List_PageSizeBelowMin_IsClampedTo1()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: "/api/v1/content-segments?pageSize=0"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.GetProperty(propertyName: "meta").GetProperty(propertyName: "pageSize").GetInt32().Should().Be(expected: 1);
    }

    [Fact]
    public async Task List_FilteredByType_OnlyReturnsThatType()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: "/api/v1/content-segments?type=Outro&pageSize=500"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");

        data.GetArrayLength().Should().BeGreaterThan(expected: 0);
        foreach (JsonElement item in data.EnumerateArray())
            item.GetProperty(propertyName: "segment_type").GetString().Should().Be(expected: "Outro");

        bool containsSeededOutro = data.EnumerateArray()
            .Any(predicate: e => e.GetProperty(propertyName: "id").GetString() == _outroSegmentId.ToString());
        containsSeededOutro.Should().BeTrue();
    }

    [Fact]
    public async Task GetByEpisode_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: $"/api/v1/content-segments/episode/{SeededEpisodeId}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetByEpisode_ReturnsSeededSegment_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: $"/api/v1/content-segments/episode/{SeededEpisodeId}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");

        data.ValueKind.Should().Be(expected: JsonValueKind.Array);
        data.GetArrayLength().Should().Be(expected: 1);

        JsonElement segment = data[index: 0];
        segment.GetProperty(propertyName: "id").GetString().Should().Be(expected: _episodeSegmentId.ToString());
        segment.GetProperty(propertyName: "episode_id").GetInt32().Should().Be(expected: SeededEpisodeId);
        segment.GetProperty(propertyName: "segment_type").GetString().Should().Be(expected: "Intro");
    }

    [Fact]
    public async Task GetByMovie_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: $"/api/v1/content-segments/movie/{SeededMovieId}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetByMovie_ReturnsSeededSegmentsOrderedByStart_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: $"/api/v1/content-segments/movie/{SeededMovieId}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");

        data.GetArrayLength().Should().Be(expected: 2, because: "movie 129 was seeded with an intro and an outro");

        JsonElement first = data[index: 0];
        JsonElement second = data[index: 1];
        first.GetProperty(propertyName: "id").GetString().Should().Be(expected: _introSegmentId.ToString());
        second.GetProperty(propertyName: "id").GetString().Should().Be(expected: _outroSegmentId.ToString());
        first
            .GetProperty(propertyName: "start_seconds")
            .GetDouble()
            .Should()
            .BeLessThan(expected: second.GetProperty(propertyName: "start_seconds").GetDouble());
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenAnonymous()
    {
        object payload = new
        {
            segment_type = "Recap",
            start_seconds = 10.0,
            end_seconds = 20.0,
            movie_id = SeededMovieId,
        };

        HttpResponseMessage response = await _unauthed.PostAsJsonAsync(
            requestUri: "/api/v1/content-segments",
            value: payload
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Create_Valid_PersistsAndReturnsSegment_WhenModerator()
    {
        object payload = new
        {
            segment_type = "Recap",
            start_seconds = 100.0,
            end_seconds = 130.0,
            movie_id = SeededMovieId,
        };

        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            requestUri: "/api/v1/content-segments",
            value: payload
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement root = doc.RootElement;

        root.GetProperty(propertyName: "segment_type").GetString().Should().Be(expected: "Recap");
        root.GetProperty(propertyName: "movie_id").GetInt32().Should().Be(expected: SeededMovieId);
        root.GetProperty(propertyName: "source").GetString().Should().Be(expected: "manual");
        Ulid createdId = Ulid.Parse(base32: root.GetProperty(propertyName: "id").GetString()!);

        try
        {
            await using MediaContext ctx = new();
            ContentSegment? persisted = await ctx
                .ContentSegments.AsNoTracking()
                .FirstOrDefaultAsync(predicate: s => s.Id == createdId);
            persisted.Should().NotBeNull();
            persisted!.StartSeconds.Should().Be(expected: 100.0);
            persisted.EndSeconds.Should().Be(expected: 130.0);
        }
        finally
        {
            await using MediaContext cleanup = new();
            await cleanup.ContentSegments.Where(predicate: s => s.Id == createdId).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Create_EndBeforeStart_Returns400()
    {
        object payload = new
        {
            segment_type = "Intro",
            start_seconds = 50.0,
            end_seconds = 10.0,
            movie_id = SeededMovieId,
        };

        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            requestUri: "/api/v1/content-segments",
            value: payload
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_MissingBothEpisodeAndMovieId_Returns400()
    {
        object payload = new
        {
            segment_type = "Intro",
            start_seconds = 0.0,
            end_seconds = 10.0,
        };

        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            requestUri: "/api/v1/content-segments",
            value: payload
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_BothEpisodeAndMovieIdSet_Returns400()
    {
        object payload = new
        {
            segment_type = "Intro",
            start_seconds = 0.0,
            end_seconds = 10.0,
            movie_id = SeededMovieId,
            episode_id = SeededEpisodeId,
        };

        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            requestUri: "/api/v1/content-segments",
            value: payload
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenAnonymous()
    {
        object payload = new { start_seconds = 1.0 };

        HttpResponseMessage response = await _unauthed.PutAsJsonAsync(
            requestUri: $"/api/v1/content-segments/{_introSegmentId}",
            value: payload
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Update_Valid_FlipsSourceToManual_WhenModerator()
    {
        object payload = new { start_seconds = 2.0, end_seconds = 95.0 };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"/api/v1/content-segments/{_introSegmentId}",
            value: payload
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement root = doc.RootElement;

        root.GetProperty(propertyName: "start_seconds").GetDouble().Should().Be(expected: 2.0);
        root.GetProperty(propertyName: "end_seconds").GetDouble().Should().Be(expected: 95.0);
        root.GetProperty(propertyName: "source").GetString().Should().Be(expected: "manual");

        await using MediaContext ctx = new();
        ContentSegment? persisted = await ctx
            .ContentSegments.AsNoTracking()
            .FirstOrDefaultAsync(predicate: s => s.Id == _introSegmentId);
        persisted.Should().NotBeNull();
        persisted!.Source.Should().Be(expected: "manual");
    }

    [Fact]
    public async Task Update_UnknownId_Returns404()
    {
        object payload = new { start_seconds = 1.0 };
        Ulid unknownId = Ulid.NewUlid();

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"/api/v1/content-segments/{unknownId}",
            value: payload
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_InvalidId_Returns400()
    {
        object payload = new { start_seconds = 1.0 };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: "/api/v1/content-segments/not-a-valid-ulid",
            value: payload
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.DeleteAsync(
            requestUri: $"/api/v1/content-segments/{_outroSegmentId}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Delete_Existing_Returns204AndRemovesRow_WhenModerator()
    {
        HttpResponseMessage response = await _authed.DeleteAsync(
            requestUri: $"/api/v1/content-segments/{_outroSegmentId}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NoContent);

        await using MediaContext ctx = new();
        bool stillExists = await ctx.ContentSegments.AnyAsync(predicate: s => s.Id == _outroSegmentId);
        stillExists.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        Ulid unknownId = Ulid.NewUlid();

        HttpResponseMessage response = await _authed.DeleteAsync(
            requestUri: $"/api/v1/content-segments/{unknownId}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_InvalidId_Returns400()
    {
        HttpResponseMessage response = await _authed.DeleteAsync(
            requestUri: "/api/v1/content-segments/not-a-valid-ulid"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }
}
