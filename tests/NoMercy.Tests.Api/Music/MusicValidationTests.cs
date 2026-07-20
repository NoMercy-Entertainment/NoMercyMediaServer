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

namespace NoMercy.Tests.Api.Music;

/// <summary>
/// Locks the input-VALIDATION contract (4xx, never 500) on the music
/// controllers' body-parsing branches that run BEFORE any repository write —
/// so every case here is exercised against the real seeded ids (ArtistId1 /
/// AlbumId1 / PlaylistId1) with zero risk of mutating them, because the
/// invalid payload always short-circuits before the update call.
/// </summary>
[Trait("Category", "Characterization")]
public class MusicValidationTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _owner;

    public MusicValidationTests(NoMercyApiFactory factory)
    {
        _owner = factory.CreateClient().AsAuthenticated();
    }

    private static StringContent JsonBody(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    // =========================================================================
    // TracksController.LyricsOffset — explicit range check runs BEFORE the
    // track lookup, so it fires identically for a real or a fake id.
    // =========================================================================

    [Theory]
    [InlineData(30001)]
    [InlineData(-30001)]
    [InlineData(999999)]
    public async Task LyricsOffset_OutOfRange_ReturnsBadRequest_BeforeTrackLookup(int offset)
    {
        HttpResponseMessage response = await _owner.PatchAsync(
            $"/api/v1/music/tracks/{Guid.NewGuid()}/lyrics-offset",
            JsonBody(new { offset })
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("detail")
            .GetString()
            .Should()
            .Contain("-30000")
            .And.Contain("30000");
    }

    [Theory]
    [InlineData(30000)]
    [InlineData(-30000)]
    [InlineData(0)]
    public async Task LyricsOffset_WithinRange_PassesValidation_ReturnsNotFoundForFakeTrack(
        int offset
    )
    {
        HttpResponseMessage response = await _owner.PatchAsync(
            $"/api/v1/music/tracks/{Guid.NewGuid()}/lyrics-offset",
            JsonBody(new { offset })
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================================================
    // AlbumsController.Edit — cover regex validation runs AFTER the album
    // lookup but BEFORE any write, so the real AlbumId1 can be targeted safely.
    // =========================================================================

    [Fact]
    public async Task AlbumsEdit_InvalidCoverFormat_ReturnsBadRequest_NoWrite()
    {
        HttpResponseMessage response = await _owner.PatchAsync(
            $"/api/v1/music/albums/{NoMercyApiFactory.AlbumId1}",
            JsonBody(new { name = "Test Album", cover = "not-a-data-uri" })
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("detail").GetString().Should().Contain("data:image/");
    }

    [Fact]
    public async Task AlbumsEdit_MalformedBase64Payload_ReturnsBadRequest_NoWrite()
    {
        HttpResponseMessage response = await _owner.PatchAsync(
            $"/api/v1/music/albums/{NoMercyApiFactory.AlbumId1}",
            JsonBody(new { name = "Test Album", cover = "data:image/jpeg,not-valid-base64!!!" })
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("detail").GetString().Should().Contain("base64");
    }

    // =========================================================================
    // ArtistsController.Edit — identical regex/base64 validation shape,
    // independently implemented, targeted at the real ArtistId1.
    // =========================================================================

    [Fact]
    public async Task ArtistsEdit_InvalidCoverFormat_ReturnsBadRequest_NoWrite()
    {
        HttpResponseMessage response = await _owner.PatchAsync(
            $"/api/v1/music/artists/{NoMercyApiFactory.ArtistId1}",
            JsonBody(new { cover = "not-a-data-uri" })
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("detail").GetString().Should().Contain("data:image/");
    }

    [Fact]
    public async Task ArtistsEdit_MalformedBase64Payload_ReturnsBadRequest_NoWrite()
    {
        HttpResponseMessage response = await _owner.PatchAsync(
            $"/api/v1/music/artists/{NoMercyApiFactory.ArtistId1}",
            JsonBody(new { cover = "data:image/jpeg,not-valid-base64!!!" })
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("detail").GetString().Should().Contain("base64");
    }

    // =========================================================================
    // PlaylistsController.Edit — same regex validation, MediaAccess-gated
    // (not Moderator), targeted at the real, owner-owned PlaylistId1.
    // =========================================================================

    [Fact]
    public async Task PlaylistsEdit_InvalidCoverFormat_ReturnsBadRequest_NoWrite()
    {
        HttpResponseMessage response = await _owner.PatchAsync(
            $"/api/v1/music/playlists/{NoMercyApiFactory.PlaylistId1}",
            JsonBody(new { name = "Test Playlist", cover = "not-a-data-uri" })
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("detail").GetString().Should().Contain("data:image/");
    }
}
