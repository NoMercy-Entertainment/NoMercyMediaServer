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
using FluentAssertions;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Music;

/// <summary>
/// AlbumsController / ArtistsController / PlaylistsController used to carry a
/// bare class-level [Authorize] on their GET reads — any authenticated
/// principal, allowed or not, could list albums/artists/playlists. The class
/// attribute is now [Authorize(Policy="MediaAccess")], which composes with the
/// controllers' existing method-level [Authorize(Policy="Moderator")]
/// mutations. TestAuthHandler.SecondaryUserId (Allowed=true, Manage=false,
/// Owner=false) proves the read tier still admits any allowed user — this is
/// NOT a tightening for reads relative to what any real seeded user sees
/// (every seeded test user is Allowed=true), it locks that MediaAccess reads
/// remain reachable while an anonymous caller is still rejected.
/// </summary>
[Trait("Category", "Characterization")]
public class MusicReadAuthTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _secondary;
    private readonly HttpClient _anonymous;

    public MusicReadAuthTests(NoMercyApiFactory factory)
    {
        _secondary = factory.CreateClient().AsSecondaryUser();
        _anonymous = factory.CreateClient().AsUnauthenticated();
    }

    [Fact]
    public async Task AlbumsIndex_SecondaryUser_PassesMediaAccess_ReturnsOk()
    {
        HttpResponseMessage response = await _secondary.GetAsync("/api/v1/music/albums/letter/_");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AlbumsIndex_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.GetAsync("/api/v1/music/albums/letter/_");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ArtistsIndex_SecondaryUser_PassesMediaAccess_ReturnsOk()
    {
        HttpResponseMessage response = await _secondary.GetAsync("/api/v1/music/artists/letter/_");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ArtistsIndex_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.GetAsync("/api/v1/music/artists/letter/_");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlaylistsIndex_SecondaryUser_PassesMediaAccess_ReturnsOk()
    {
        HttpResponseMessage response = await _secondary.GetAsync("/api/v1/music/playlists");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PlaylistsIndex_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _anonymous.GetAsync("/api/v1/music/playlists");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }
}
