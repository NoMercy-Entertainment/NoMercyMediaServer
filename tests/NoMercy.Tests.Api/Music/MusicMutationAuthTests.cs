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
/// Locks the AUTH TIER contract on every mutating action across the six music
/// controllers (MusicController, TracksController, AlbumsController,
/// ArtistsController, PlaylistsController). Each test uses ONLY fake ids
/// (Guid.NewGuid()) or a brand-new, self-cleaned row — never a seeded id — so
/// no seeded music-library row is ever mutated or deleted by this suite.
///
/// TestAuthHandler.SecondaryUserId is Allowed=true, Manage=false, Owner=false —
/// it passes [Authorize(Policy="MediaAccess")] (only requires Allowed), but
/// fails [Authorize(Policy="Moderator")] (requires Manage or Owner). Every
/// Moderator-gated action below is exercised with the secondary user
/// specifically to prove that gate actually rejects a non-moderator, not just
/// that the default (Owner+Manage) test identity can call it — the exact gap
/// called out for this batch.
/// </summary>
[Trait(name: "Category", value: "Characterization")]
public class MusicMutationAuthTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _owner;
    private readonly HttpClient _secondary;
    private readonly HttpClient _anonymous;

    public MusicMutationAuthTests(NoMercyApiFactory factory)
    {
        _owner = factory.CreateClient().AsAuthenticated();
        _secondary = factory.CreateClient().AsSecondaryUser();
        _anonymous = factory.CreateClient().AsUnauthenticated();
    }

    private static StringContent JsonBody(object obj) =>
        new(content: JsonSerializer.Serialize(value: obj), encoding: Encoding.UTF8, mediaType: "application/json");

    private static MultipartFormDataContent EmptyMultipart()
    {
        MultipartFormDataContent content = new();
        return content;
    }

    private static void AssertForbiddenProblemDetails(JsonElement root)
    {
        root.GetProperty(propertyName: "status").GetInt32().Should().Be(expected: 403);
        root.GetProperty(propertyName: "title").GetString().Should().Be(expected: "Unauthorized.");
    }

    private static void AssertNotFoundDetail(JsonElement root, string expectedDetailSubstring)
    {
        root.GetProperty(propertyName: "status").GetInt32().Should().Be(expected: 404);
        root.GetProperty(propertyName: "detail").GetString().Should().Contain(expected: expectedDetailSubstring);
    }

    // =========================================================================
    // MusicController — POST /api/v1/music/search/{query}/{type} (class MediaAccess)
    // =========================================================================

    [Fact]
    public async Task TypeSearch_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PostAsync(
            requestUri: "/api/v1/music/search/test/artist",
            content: JsonBody(obj: new { })
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task TypeSearch_SecondaryUser_PassesMediaAccess_ReturnsEmptyPlaceholder()
    {
        HttpResponseMessage response = await _secondary.PostAsync(
            requestUri: "/api/v1/music/search/test/artist",
            content: JsonBody(obj: new { })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty(propertyName: "data").GetArrayLength().Should().Be(expected: 0);
    }

    // =========================================================================
    // TracksController — bare class-level MediaAccess, no per-method tier
    // =========================================================================

    [Fact]
    public async Task TracksLike_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PostAsync(
            requestUri: $"/api/v1/music/tracks/{Guid.NewGuid()}/like",
            content: JsonBody(obj: new { value = true })
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task TracksLike_SecondaryUser_PassesMediaAccess_ReturnsNotFoundForFakeId()
    {
        HttpResponseMessage response = await _secondary.PostAsync(
            requestUri: $"/api/v1/music/tracks/{Guid.NewGuid()}/like",
            content: JsonBody(obj: new { value = true })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        AssertNotFoundDetail(root: doc.RootElement, expectedDetailSubstring: "Track not found");
    }

    [Fact]
    public async Task TracksLyricsOffset_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PatchAsync(
            requestUri: $"/api/v1/music/tracks/{Guid.NewGuid()}/lyrics-offset",
            content: JsonBody(obj: new { offset = 100 })
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task TracksLyricsOffset_SecondaryUser_PassesMediaAccess_ReturnsNotFoundForFakeId()
    {
        HttpResponseMessage response = await _secondary.PatchAsync(
            requestUri: $"/api/v1/music/tracks/{Guid.NewGuid()}/lyrics-offset",
            content: JsonBody(obj: new { offset = (int?)null })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        AssertNotFoundDetail(root: doc.RootElement, expectedDetailSubstring: "Track not found");
    }

    [Fact]
    public async Task TracksPlayback_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PostAsync(
            requestUri: $"/api/v1/music/tracks/{Guid.NewGuid()}/playback",
            content: JsonBody(obj: new { })
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task TracksPlayback_SecondaryUser_PassesMediaAccess_ReturnsNotFoundForFakeId()
    {
        HttpResponseMessage response = await _secondary.PostAsync(
            requestUri: $"/api/v1/music/tracks/{Guid.NewGuid()}/playback",
            content: JsonBody(obj: new { })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        AssertNotFoundDetail(root: doc.RootElement, expectedDetailSubstring: "Track not found");
    }

    // =========================================================================
    // AlbumsController — class-level [Authorize(Policy="MediaAccess")], plus a
    // manual AuthPolicy.IsAllowed() check on Like (same tier: Allowed=true
    // suffices); Rescan / Edit / Cover are [Authorize(Policy="Moderator")].
    // =========================================================================

    [Fact]
    public async Task AlbumsLike_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PostAsync(
            requestUri: $"/api/v1/music/albums/{Guid.NewGuid()}/like",
            content: JsonBody(obj: new { value = true })
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task AlbumsLike_SecondaryUser_PassesAllowedCheck_ReturnsUnprocessableForFakeId()
    {
        HttpResponseMessage response = await _secondary.PostAsync(
            requestUri: $"/api/v1/music/albums/{Guid.NewGuid()}/like",
            content: JsonBody(obj: new { value = true })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty(propertyName: "status").GetInt32().Should().Be(expected: 422);
        doc.RootElement.GetProperty(propertyName: "detail").GetString().Should().Contain(expected: "Albums not found");
    }

    [Fact]
    public async Task AlbumsRescan_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PostAsync(
            requestUri: $"/api/v1/music/albums/{Guid.NewGuid()}/rescan",
            content: JsonBody(obj: new { })
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task AlbumsRescan_SecondaryUser_RejectedByModeratorGate()
    {
        HttpResponseMessage response = await _secondary.PostAsync(
            requestUri: $"/api/v1/music/albums/{Guid.NewGuid()}/rescan",
            content: JsonBody(obj: new { })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Forbidden);
        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        AssertForbiddenProblemDetails(root: doc.RootElement);
    }

    [Fact]
    public async Task AlbumsRescan_Owner_PassesModeratorGate_ReturnsOk()
    {
        // Rescan never reads the repository for the id at all — it always
        // returns "Rescan started" regardless — so a fake id is fully safe here.
        HttpResponseMessage response = await _owner.PostAsync(
            requestUri: $"/api/v1/music/albums/{Guid.NewGuid()}/rescan",
            content: JsonBody(obj: new { })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty(propertyName: "status").GetString().Should().Be(expected: "ok");
        doc.RootElement.GetProperty(propertyName: "message").GetString().Should().Be(expected: "Rescan started");
    }

    [Fact]
    public async Task AlbumsEdit_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PatchAsync(
            requestUri: $"/api/v1/music/albums/{Guid.NewGuid()}",
            content: JsonBody(obj: new { name = "Hijacked" })
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task AlbumsEdit_SecondaryUser_RejectedByModeratorGate_BeforeReachingLookup()
    {
        HttpResponseMessage response = await _secondary.PatchAsync(
            requestUri: $"/api/v1/music/albums/{Guid.NewGuid()}",
            content: JsonBody(obj: new { name = "Hijacked" })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AlbumsCover_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PostAsync(
            requestUri: $"/api/v1/music/albums/{Guid.NewGuid()}/cover",
            content: EmptyMultipart()
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task AlbumsCover_SecondaryUser_RejectedByModeratorGate()
    {
        HttpResponseMessage response = await _secondary.PostAsync(
            requestUri: $"/api/v1/music/albums/{Guid.NewGuid()}/cover",
            content: EmptyMultipart()
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Forbidden);
    }

    // =========================================================================
    // ArtistsController — same class-level MediaAccess/Moderator split as
    // Albums, plus Destroy (Moderator) which this suite only ever calls with a
    // fake id.
    // =========================================================================

    [Fact]
    public async Task ArtistsLike_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PostAsync(
            requestUri: $"/api/v1/music/artists/{Guid.NewGuid()}/like",
            content: JsonBody(obj: new { value = true })
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task ArtistsLike_SecondaryUser_PassesAllowedCheck_ReturnsUnprocessableForFakeId()
    {
        HttpResponseMessage response = await _secondary.PostAsync(
            requestUri: $"/api/v1/music/artists/{Guid.NewGuid()}/like",
            content: JsonBody(obj: new { value = true })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty(propertyName: "detail").GetString().Should().Contain(expected: "Artist not found");
    }

    [Fact]
    public async Task ArtistsRescan_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PostAsync(
            requestUri: $"/api/v1/music/artists/{Guid.NewGuid()}/rescan",
            content: JsonBody(obj: new { })
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task ArtistsRescan_SecondaryUser_RejectedByModeratorGate()
    {
        HttpResponseMessage response = await _secondary.PostAsync(
            requestUri: $"/api/v1/music/artists/{Guid.NewGuid()}/rescan",
            content: JsonBody(obj: new { })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ArtistsRescan_Owner_PassesModeratorGate_ReturnsOk()
    {
        HttpResponseMessage response = await _owner.PostAsync(
            requestUri: $"/api/v1/music/artists/{Guid.NewGuid()}/rescan",
            content: JsonBody(obj: new { })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty(propertyName: "message").GetString().Should().Be(expected: "Rescan started");
    }

    [Fact]
    public async Task ArtistsDestroy_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.DeleteAsync(
            requestUri: $"/api/v1/music/artists/{Guid.NewGuid()}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task ArtistsDestroy_SecondaryUser_RejectedByModeratorGate()
    {
        HttpResponseMessage response = await _secondary.DeleteAsync(
            requestUri: $"/api/v1/music/artists/{Guid.NewGuid()}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ArtistsDestroy_Owner_FakeId_ReturnsOkWithNotFoundMessage()
    {
        // Destroy always responds 200 regardless of whether a row was actually
        // deleted (ExecuteDeleteAsync affecting 0 rows still returns Ok) — this
        // locks that today's contract never surfaces a 404 here, only the
        // message text differs. A fake id proves this without ever touching
        // the seeded ArtistId1 row every other music test depends on.
        HttpResponseMessage response = await _owner.DeleteAsync(
            requestUri: $"/api/v1/music/artists/{Guid.NewGuid()}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty(propertyName: "status").GetString().Should().Be(expected: "ok");
        doc.RootElement.GetProperty(propertyName: "data").GetString().Should().Be(expected: "Artist not found");
    }

    [Fact]
    public async Task ArtistsEdit_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PatchAsync(
            requestUri: $"/api/v1/music/artists/{Guid.NewGuid()}",
            content: JsonBody(obj: new { name = "Hijacked" })
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task ArtistsEdit_SecondaryUser_RejectedByModeratorGate()
    {
        HttpResponseMessage response = await _secondary.PatchAsync(
            requestUri: $"/api/v1/music/artists/{Guid.NewGuid()}",
            content: JsonBody(obj: new { name = "Hijacked" })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ArtistsCover_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PostAsync(
            requestUri: $"/api/v1/music/artists/{Guid.NewGuid()}/cover",
            content: EmptyMultipart()
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task ArtistsCover_SecondaryUser_RejectedByModeratorGate()
    {
        HttpResponseMessage response = await _secondary.PostAsync(
            requestUri: $"/api/v1/music/artists/{Guid.NewGuid()}/cover",
            content: EmptyMultipart()
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Forbidden);
    }

    // =========================================================================
    // PlaylistsController — Create/Edit/Destroy/AddTrack/RemoveTrack are
    // [Authorize(Policy="MediaAccess")] (Allowed=true suffices); Cover is
    // [Authorize(Policy="Moderator")].
    // =========================================================================

    [Fact]
    public async Task PlaylistsCreate_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PostAsync(
            requestUri: "/api/v1/music/playlists",
            content: JsonBody(obj: new { name = "Anonymous Music Playlist" })
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task PlaylistsCreate_SecondaryUser_PassesMediaAccess_CanCreateAndDeleteOwnPlaylist()
    {
        // Proves the MediaAccess gate lets a non-moderator all the way through
        // to a real write, then immediately cleans up via the same identity's
        // own Destroy call so no residual row survives this test.
        string uniqueName = $"Secondary Tier Playlist {Guid.NewGuid()}";

        HttpResponseMessage create = await _secondary.PostAsync(
            requestUri: "/api/v1/music/playlists",
            content: JsonBody(obj: new { name = uniqueName })
        );
        create.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        using JsonDocument createDoc = JsonDocument.Parse(json: await create.Content.ReadAsStringAsync());
        Guid createdId = createDoc.RootElement.GetProperty(propertyName: "data").GetProperty(propertyName: "id").GetGuid();

        HttpResponseMessage delete = await _secondary.DeleteAsync(
            requestUri: $"/api/v1/music/playlists/{createdId}"
        );
        delete.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        using JsonDocument deleteDoc = JsonDocument.Parse(json: await delete.Content.ReadAsStringAsync());
        deleteDoc
            .RootElement.GetProperty(propertyName: "data")
            .GetString()
            .Should()
            .Be(expected: "Playlist deleted successfully");
    }

    [Fact]
    public async Task PlaylistsEdit_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PatchAsync(
            requestUri: $"/api/v1/music/playlists/{Guid.NewGuid()}",
            content: JsonBody(obj: new { name = "Hijacked" })
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task PlaylistsEdit_SecondaryUser_PassesMediaAccess_ReturnsNotFoundForFakeId()
    {
        HttpResponseMessage response = await _secondary.PatchAsync(
            requestUri: $"/api/v1/music/playlists/{Guid.NewGuid()}",
            content: JsonBody(obj: new { name = "Whatever" })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        AssertNotFoundDetail(root: doc.RootElement, expectedDetailSubstring: "Playlist not found");
    }

    [Fact]
    public async Task PlaylistsDestroy_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.DeleteAsync(
            requestUri: $"/api/v1/music/playlists/{Guid.NewGuid()}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task PlaylistsDestroy_SecondaryUser_FakeId_ReturnsOkWithNotFoundMessage()
    {
        // Same always-200 quirk as ArtistsController.Destroy: a delete matching
        // zero rows (id + ownership scoped) still responds 200, only the
        // message text says "not found". Locks it for the MediaAccess tier too.
        HttpResponseMessage response = await _secondary.DeleteAsync(
            requestUri: $"/api/v1/music/playlists/{Guid.NewGuid()}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty(propertyName: "data").GetString().Should().Be(expected: "Playlist not found");
    }

    [Fact]
    public async Task PlaylistsCover_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PostAsync(
            requestUri: $"/api/v1/music/playlists/{Guid.NewGuid()}/cover",
            content: EmptyMultipart()
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task PlaylistsCover_SecondaryUser_RejectedByModeratorGate()
    {
        // Cover is the ONE Playlists mutation gated at Moderator rather than
        // MediaAccess — unlike every sibling mutation on this controller.
        HttpResponseMessage response = await _secondary.PostAsync(
            requestUri: $"/api/v1/music/playlists/{Guid.NewGuid()}/cover",
            content: EmptyMultipart()
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlaylistsAddTrack_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.PostAsync(
            requestUri: $"/api/v1/music/playlists/{Guid.NewGuid()}/tracks",
            content: JsonBody(obj: new { id = Guid.NewGuid() })
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task PlaylistsAddTrack_SecondaryUser_PassesMediaAccess_ReturnsNotFoundForFakePlaylist()
    {
        HttpResponseMessage response = await _secondary.PostAsync(
            requestUri: $"/api/v1/music/playlists/{Guid.NewGuid()}/tracks",
            content: JsonBody(obj: new { id = Guid.NewGuid() })
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        AssertNotFoundDetail(root: doc.RootElement, expectedDetailSubstring: "Playlist not found");
    }

    [Fact]
    public async Task PlaylistsRemoveTrack_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.DeleteAsync(
            requestUri: $"/api/v1/music/playlists/{Guid.NewGuid()}/tracks/{Guid.NewGuid()}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task PlaylistsRemoveTrack_SecondaryUser_PassesMediaAccess_ReturnsNotFoundForFakeIds()
    {
        HttpResponseMessage response = await _secondary.DeleteAsync(
            requestUri: $"/api/v1/music/playlists/{Guid.NewGuid()}/tracks/{Guid.NewGuid()}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
        using JsonDocument doc = JsonDocument.Parse(json: await response.Content.ReadAsStringAsync());
        AssertNotFoundDetail(root: doc.RootElement, expectedDetailSubstring: "Track not found in playlist");
    }
}
